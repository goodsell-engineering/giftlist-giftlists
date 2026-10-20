using GiftLists.Application.GiftLists;
using GiftLists.Contracts.GiftLists;
using GiftLists.Contracts.GiftLists.Events;
using GiftLists.IntegrationTests.Fixtures;
using GiftLists.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace GiftLists.IntegrationTests.GiftLists;

[Collection(GiftListsCollection.Name)]
public sealed class AddGiftItemTests(GiftListsFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddGiftItem_ShouldAppendTheItemInMongo_AndPublishGiftItemAddedV1()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var list = await GiftListSeeding.SeedListAsync(fixture, ownerId);
        var itemId = Guid.NewGuid();
        await using var subscriber = await EventSubscriber<GiftItemAddedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act
        await fixture.RequesterBus.Send(new AddGiftItem(
            list.Id.Value, ownerId, itemId, "Coffee grinder", "Burr, not blade", "https://example.com/grinder"));
        var published = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(list.Id.Value, published.ListId);
        Assert.Equal(itemId, published.ItemId);
        Assert.Equal("Coffee grinder", published.Name);
        Assert.Equal("Burr, not blade", published.Description);
        Assert.Equal("https://example.com/grinder", published.Url);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        var item = Assert.Single(persisted.Items);
        Assert.Equal(itemId, item.Id.Value);
        Assert.Equal("Coffee grinder", item.Name.Value);
        Assert.Equal("Burr, not blade", item.Description?.Value);
        Assert.Equal("https://example.com/grinder", item.Url?.Value);
    }

    [Fact]
    public async Task AddGiftItem_ShouldStoreAndPublishThePriority_WhenOneIsGiven()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var list = await GiftListSeeding.SeedListAsync(fixture, ownerId);
        var itemId = Guid.NewGuid();
        await using var subscriber = await EventSubscriber<GiftItemAddedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act
        await fixture.RequesterBus.Send(new AddGiftItem(list.Id.Value, ownerId, itemId, "Headphones", null, null, Priority: 5));
        var published = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(5, published.Priority);
        Assert.Single(subscriber.Capture.All);
        var raw = await GiftListSeeding.GetRawDocumentAsync(fixture, list.Id);
        Assert.Equal(5, raw["items"].AsBsonArray.Single().AsBsonDocument["priority"].AsInt32);
    }

    [Fact]
    public async Task AddGiftItem_ShouldStoreAndPublish3_WhenNoPriorityIsGiven()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var list = await GiftListSeeding.SeedListAsync(fixture, ownerId);
        await using var subscriber = await EventSubscriber<GiftItemAddedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act
        await fixture.RequesterBus.Send(new AddGiftItem(list.Id.Value, ownerId, Guid.NewGuid(), "Book", null, null, Priority: null));
        var published = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert — the published value is the resolved 3, not null.
        Assert.Equal(3, published.Priority);
        var raw = await GiftListSeeding.GetRawDocumentAsync(fixture, list.Id);
        Assert.Equal(3, raw["items"].AsBsonArray.Single().AsBsonDocument["priority"].AsInt32);
    }

    [Fact]
    public async Task AddGiftItem_ShouldStoreAndPublishNothing_WhenThePriorityIsOutside1To5()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var list = await GiftListSeeding.SeedListAsync(fixture, ownerId);
        await using var subscriber = await EventSubscriber<GiftItemAddedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var logsBefore = fixture.Logs.Entries.Count;
        const string expected = "AddGiftItemRequest failed: giftlist.item_priority_invalid";

        // Act
        await fixture.RequesterBus.Send(new AddGiftItem(list.Id.Value, ownerId, Guid.NewGuid(), "Zero", null, null, Priority: 0));
        await LogAssertions.WaitForWarningAsync(fixture.Logs, logsBefore, expected, TimeSpan.FromSeconds(15));
        var logsAfterFirst = fixture.Logs.Entries.Count;
        await fixture.RequesterBus.Send(new AddGiftItem(list.Id.Value, ownerId, Guid.NewGuid(), "Six", null, null, Priority: 6));
        await LogAssertions.WaitForWarningAsync(fixture.Logs, logsAfterFirst, expected, TimeSpan.FromSeconds(15));
        Assert.Empty((await GiftListSeeding.GetRawDocumentAsync(fixture, list.Id))["items"].AsBsonArray);
        var followUpItemId = Guid.NewGuid();
        await fixture.RequesterBus.Send(new AddGiftItem(list.Id.Value, ownerId, followUpItemId, "Valid", null, null, Priority: 2));
        var followUp = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert — the consumer is alive (the follow-up arrived) and nothing else was published.
        Assert.Equal(followUpItemId, followUp.ItemId);
        Assert.Single(subscriber.Capture.All);
        Assert.Equal(followUpItemId, Assert.Single(
            (await GiftListSeeding.GetRawDocumentAsync(fixture, list.Id))["items"].AsBsonArray).AsBsonDocument["itemId"].AsGuid);
    }
}
