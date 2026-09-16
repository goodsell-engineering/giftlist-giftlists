using GiftLists.Application.GiftLists;
using GiftLists.Contracts.GiftLists;
using GiftLists.Contracts.GiftLists.Events;
using GiftLists.Domain.GiftLists;
using GiftLists.IntegrationTests.Fixtures;
using GiftLists.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace GiftLists.IntegrationTests.GiftLists;

[Collection(GiftListsCollection.Name)]
public sealed class CreateGiftListTests(GiftListsFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateGiftList_ShouldPersistTheListToMongo_AndPublishGiftListCreatedV1()
    {
        // Arrange — a subscriber wired up exactly the way the Gateway/Reservation would be.
        await using var subscriber = await EventSubscriber<GiftListCreatedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var listId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        // Act
        await fixture.RequesterBus.Send(new CreateGiftList(listId, ownerId, "Birthday Wishlist", expiresAt));
        var published = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(listId, published.ListId);
        Assert.Equal(ownerId, published.OwnerId);
        Assert.Equal("Birthday Wishlist", published.Name);
        // The event carries the instant the system RECORDED, which is the caller's value normalised
        // to millisecond resolution (Timestamps.ToStoredPrecision) — not what the caller typed.
        //
        // Asserted as three properties rather than as Assert.Equal(ToStoredPrecision(expiresAt), ...).
        // That comparison was the obvious way to write this and it was worthless: published.ExpiresAt
        // IS ToStoredPrecision(expiresAt), so it read Assert.Equal(f(x), f(x)) — true for every
        // possible f, green even if ToStoredPrecision returned its argument untouched, rounded, or
        // truncated to whole seconds. It was unfalsifiable green #9, and it was found in review after
        // I wrote it while fixing #8 four lines below. Each assertion here fails under a different
        // mutation of ToStoredPrecision.
        Assert.Equal(0, published.ExpiresAt.Ticks % TimeSpan.TicksPerMillisecond);      // normalised at all
        Assert.True(published.ExpiresAt <= expiresAt);                                  // truncates, never rounds up
        Assert.True(expiresAt - published.ExpiresAt < TimeSpan.FromMilliseconds(1));    // sub-millisecond only
        Assert.Equal(21, published.ShareToken.Length);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var persisted = await repository.FindByIdAsync(new GiftListId(listId), CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(ownerId, persisted.OwnerId.Value);
        Assert.Equal("Birthday Wishlist", persisted.Name.Value);
        Assert.Equal(published.ShareToken, persisted.ShareToken.Value);
        Assert.Empty(persisted.Items);
    }

    [Fact]
    public async Task CreateGiftList_ShouldPersistTheList_AndMakeGiftListCreatedV1ObservableToASubscriber()
    {
        // Arrange — this test does NOT prove save-then-publish ordering, despite what an earlier
        // version of its name and this comment claimed. That claim was checked by mutation and is
        // false: with the ordering fully INVERTED in CreateGiftListInteractor, this test passed
        // 3/3. Under publish-then-save the event still has to cross a real broker, which takes
        // longer than the local Mongo write it was racing, so the write has almost always landed
        // by the time the assertion runs. It was green for a reason unrelated to the rule it named.
        //
        // ARCHITECTURE.md "Event publishing: synchronous"'s ordering is now covered deterministically by
        // CreateGiftListInteractorTests.Handle_ShouldSaveToTheRepository_BeforePublishingTheDomainEvents,
        // which shares one CallLog between the repository and publisher fakes and fails on the
        // inverted order every time.
        //
        // What this test is still worth: end-to-end through a real broker and real Mongo, the
        // command produces BOTH a persisted list and an event a real subscriber actually receives.
        // That is a genuine integration concern the unit test cannot cover.
        await using var subscriber = await EventSubscriber<GiftListCreatedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var listId = Guid.NewGuid();

        // Act
        await fixture.RequesterBus.Send(new CreateGiftList(listId, Guid.NewGuid(), "Ordering Check", DateTimeOffset.UtcNow.AddDays(7)));
        await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var persisted = await repository.FindByIdAsync(new GiftListId(listId), CancellationToken.None);
        Assert.NotNull(persisted);
    }

    /// <summary>
    /// GL-31: proves GENERATION, not merely storage/shape. The two tests above already assert
    /// <c>published.ShareToken.Length == 21</c>, but that alone is satisfied just as well by a
    /// hardcoded 21-character constant returned on every call — it says nothing about whether
    /// <see cref="Infrastructure.Platform.Security.ShareTokenGenerator"/>'s
    /// <c>RandomNumberGenerator</c> source is actually being consulted per list. This test goes
    /// through the real wire path (<see cref="GiftListsFixture.RequesterBus"/>, the real command
    /// handler, the real composition-root-registered <c>IShareTokenGenerator</c> — nothing here is
    /// faked, unlike <see cref="GiftListsFixture.CreateGiftListsScope"/>'s own doc comment
    /// explaining why a wire-level *collision* test is not possible) for two independent lists and
    /// asserts their published tokens differ — the one thing a fixed/non-random implementation
    /// could not produce. The base62/length pattern below is hardcoded rather than read from
    /// <c>GiftLists.Domain.GiftLists.ShareToken</c>'s own constants deliberately — an independent
    /// restatement of the expected shape is worth more here than a comparison that would pass
    /// trivially if that type's own pattern ever changed underneath it.
    /// </summary>
    [Fact]
    public async Task CreateGiftList_ShouldGenerateADistinctRandomShareToken_ForEachList()
    {
        // Arrange
        await using var firstSubscriber = await EventSubscriber<GiftListCreatedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var firstListId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        // Act
        await fixture.RequesterBus.Send(new CreateGiftList(firstListId, ownerId, "First List", expiresAt));
        var firstPublished = await firstSubscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var secondSubscriber = await EventSubscriber<GiftListCreatedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var secondListId = Guid.NewGuid();

        await fixture.RequesterBus.Send(new CreateGiftList(secondListId, ownerId, "Second List", expiresAt));
        var secondPublished = await secondSubscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Matches("^[0-9A-Za-z]{21}$", firstPublished.ShareToken);
        Assert.Matches("^[0-9A-Za-z]{21}$", secondPublished.ShareToken);
        Assert.NotEqual(firstPublished.ShareToken, secondPublished.ShareToken);
    }
}
