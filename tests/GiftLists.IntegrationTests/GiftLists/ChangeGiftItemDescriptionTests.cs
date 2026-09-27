using GiftLists.Application.GiftLists;
using GiftLists.Contracts.GiftLists;
using GiftLists.Contracts.GiftLists.Events;
using GiftLists.Domain.GiftLists;
using GiftLists.IntegrationTests.Fixtures;
using GiftLists.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace GiftLists.IntegrationTests.GiftLists;

[Collection(GiftListsCollection.Name)]
public sealed class ChangeGiftItemDescriptionTests(GiftListsFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ChangeGiftItemDescription_ShouldUpdateTheItemInMongo_AndPublishGiftItemDescriptionChangedV1()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId, "Size M");
        await using var subscriber = await EventSubscriber<GiftItemDescriptionChangedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act
        await fixture.RequesterBus.Send(new ChangeGiftItemDescription(list.Id.Value, ownerId, itemId.Value, "Size L, navy blue"));
        var published = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(list.Id.Value, published.ListId);
        Assert.Equal(itemId.Value, published.ItemId);
        Assert.Equal("Size L, navy blue", published.Description);
        // GL-66: every instant this service raises is millisecond-aligned.
        Assert.Equal(0, published.ChangedAt.Ticks % TimeSpan.TicksPerMillisecond);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        var item = Assert.Single(persisted.Items);
        Assert.Equal("Size L, navy blue", item.Description?.Value);
    }

    [Fact]
    public async Task ChangeGiftItemDescription_ShouldStoreNullAndPublishANullDescription_WhenTheValueIsBlank()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId, "Size M");
        await using var subscriber = await EventSubscriber<GiftItemDescriptionChangedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act
        await fixture.RequesterBus.Send(new ChangeGiftItemDescription(list.Id.Value, ownerId, itemId.Value, "   "));
        var published = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Null(published.Description);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        var item = Assert.Single(persisted.Items);
        Assert.Null(item.Description);
    }

    [Fact]
    public async Task ChangeGiftItemDescription_ShouldLeaveTheItemUnchangedAndPublishNothing_WhenTheRequesterIsNotTheOwner()
    {
        // Arrange — "publishes nothing" also sends a follow-up whose event must arrive, so a dead
        // consumer cannot pass as "nothing was published" (dotnet-integration-tests).
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId, "Size M");
        await using var subscriber = await EventSubscriber<GiftItemDescriptionChangedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act
        await fixture.RequesterBus.Send(new ChangeGiftItemDescription(list.Id.Value, Guid.NewGuid(), itemId.Value, "Hijacked"));
        await Task.Delay(TimeSpan.FromSeconds(3));
        await fixture.RequesterBus.Send(new ChangeGiftItemDescription(list.Id.Value, ownerId, itemId.Value, "Size L"));
        var followUp = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal("Size L", followUp.Description);
        Assert.Single(subscriber.Capture.All);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        var item = Assert.Single(persisted.Items);
        Assert.Equal("Size L", item.Description?.Value);
    }

    [Fact]
    public async Task ChangeGiftItemDescription_ShouldApply2000Characters_AndRejectA2001CharacterFollowUp()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        var exactly2000 = new string('a', GiftItemDescription.MaxLength);
        var tooLong = new string('a', GiftItemDescription.MaxLength + 1);
        await using var subscriber = await EventSubscriber<GiftItemDescriptionChangedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act
        await fixture.RequesterBus.Send(new ChangeGiftItemDescription(list.Id.Value, ownerId, itemId.Value, exactly2000));
        var published = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await fixture.RequesterBus.Send(new ChangeGiftItemDescription(list.Id.Value, ownerId, itemId.Value, tooLong));
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(exactly2000, published.Description);
        Assert.Single(subscriber.Capture.All);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        var item = Assert.Single(persisted.Items);
        Assert.Equal(exactly2000, item.Description?.Value);
    }

    [Fact]
    public async Task ChangeGiftItemDescription_ShouldLeaveTheItemUnchangedAndPublishNothing_WhenTheListHasExpired()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(
            fixture, ownerId, "Size M", DateTimeOffset.UtcNow.AddDays(-1));
        await using var subscriber = await EventSubscriber<GiftItemDescriptionChangedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act — send it on the expired list, then a well-formed change on a fresh list to prove
        // the subscriber is alive.
        await fixture.RequesterBus.Send(new ChangeGiftItemDescription(list.Id.Value, ownerId, itemId.Value, "Too late"));
        await Task.Delay(TimeSpan.FromSeconds(3));
        var (otherList, otherItemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        await fixture.RequesterBus.Send(new ChangeGiftItemDescription(otherList.Id.Value, ownerId, otherItemId.Value, "Size L"));
        var followUp = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(otherList.Id.Value, followUp.ListId);
        Assert.Single(subscriber.Capture.All);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        var item = Assert.Single(persisted.Items);
        Assert.Equal("Size M", item.Description?.Value);
    }

    [Fact]
    public async Task ChangeGiftItemDescription_ShouldPublishNothing_WhenTheItemWasRemoved()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId, "Size M");
        await fixture.RequesterBus.Send(new RemoveGiftItem(list.Id.Value, ownerId, itemId.Value));
        await Task.Delay(TimeSpan.FromSeconds(3));
        await using var subscriber = await EventSubscriber<GiftItemDescriptionChangedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act
        await fixture.RequesterBus.Send(new ChangeGiftItemDescription(list.Id.Value, ownerId, itemId.Value, "Too late"));
        await Task.Delay(TimeSpan.FromSeconds(3));
        var (otherList, otherItemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        await fixture.RequesterBus.Send(new ChangeGiftItemDescription(otherList.Id.Value, ownerId, otherItemId.Value, "Size L"));
        var followUp = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(otherList.Id.Value, followUp.ListId);
        Assert.Single(subscriber.Capture.All);
    }

    [Fact]
    public async Task ChangeGiftItemDescription_ShouldPublishOnce_WhenTheSameCommandIsDeliveredTwice()
    {
        // Arrange — a redelivered command (CONVENTIONS.md "Messaging": at-least-once) must not
        // raise a second GiftItemDescriptionChanged for a change that already happened.
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId, "Size M");
        await using var subscriber = await EventSubscriber<GiftItemDescriptionChangedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var command = new ChangeGiftItemDescription(list.Id.Value, ownerId, itemId.Value, "Size L");

        // Act
        await fixture.RequesterBus.Send(command);
        var published = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await fixture.RequesterBus.Send(command);
        // No completion to await for an event that should never come — a bounded settle window,
        // then a follow-up proves the subscriber itself is still alive.
        await Task.Delay(TimeSpan.FromSeconds(5));
        var (otherList, otherItemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        await fixture.RequesterBus.Send(new ChangeGiftItemDescription(otherList.Id.Value, ownerId, otherItemId.Value, "Different"));
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert
        Assert.Equal("Size L", published.Description);
        Assert.Equal(2, subscriber.Capture.All.Count);
        Assert.Single(subscriber.Capture.All, e => e.ListId == list.Id.Value);
    }

    [Fact]
    public async Task ChangeGiftItemDescription_ShouldPublishNothingAndKeepTheStoredVersion_WhenTheTrimmedValueIsUnchanged()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId, "Size M");
        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var before = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(before);
        var versionBefore = before.Version;
        await using var subscriber = await EventSubscriber<GiftItemDescriptionChangedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act — whitespace-padded but the same trimmed value.
        await fixture.RequesterBus.Send(new ChangeGiftItemDescription(list.Id.Value, ownerId, itemId.Value, "  Size M  "));
        await Task.Delay(TimeSpan.FromSeconds(3));
        var (otherList, otherItemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        await fixture.RequesterBus.Send(new ChangeGiftItemDescription(otherList.Id.Value, ownerId, otherItemId.Value, "Different"));
        var followUp = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(otherList.Id.Value, followUp.ListId);
        Assert.Single(subscriber.Capture.All);

        var after = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(after);
        Assert.Equal(versionBefore, after.Version);
        Assert.Equal("Size M", Assert.Single(after.Items).Description?.Value);
    }

    [Fact]
    public async Task ChangeGiftItemDescription_ShouldStoreTheValueWithTheLaterChangedAt_WhenTwoDifferentEditsRace()
    {
        // Arrange — evidence, not proof: see AddGiftItemConcurrencyTests' own doc comment for why
        // a real race against real infrastructure cannot be forced deterministically from here.
        // The deterministic guarantee behind this is
        // GiftListRepositoryTests.UpdateAsync_ShouldThrowConcurrencyException_WhenAnotherWriterChangedTheDescriptionFirst.
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId, "Size M");
        var first = new ChangeGiftItemDescription(list.Id.Value, ownerId, itemId.Value, "Size L");
        var second = new ChangeGiftItemDescription(list.Id.Value, ownerId, itemId.Value, "Size XL");

        // Act — no await between the two Sends, same technique as AddGiftItemConcurrencyTests.
        await Task.WhenAll(
            fixture.RequesterBus.Send(first),
            fixture.RequesterBus.Send(second));
        var persisted = await PollUntilOneOfTheseAppliesAsync(list.Id, "Size L", "Size XL", TimeSpan.FromSeconds(20));

        // Assert
        Assert.NotNull(persisted);
        var item = Assert.Single(persisted.Items);
        Assert.Contains(item.Description?.Value, new[] { "Size L", "Size XL" });
    }

    private async Task<GiftList?> PollUntilOneOfTheseAppliesAsync(
        GiftListId listId, string firstCandidate, string secondCandidate, TimeSpan timeout)
    {
        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var current = await repository.FindByIdAsync(listId, CancellationToken.None);
            var description = current?.Items.FirstOrDefault()?.Description?.Value;
            if (description == firstCandidate || description == secondCandidate)
            {
                return current;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        return await repository.FindByIdAsync(listId, CancellationToken.None);
    }
}
