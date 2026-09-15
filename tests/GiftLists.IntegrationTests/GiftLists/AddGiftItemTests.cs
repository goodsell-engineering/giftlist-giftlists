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
}
