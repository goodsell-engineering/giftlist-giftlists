using GiftLists.Application.GiftLists;
using GiftLists.Domain.GiftLists;
using GiftLists.IntegrationTests.Fixtures;
using GiftLists.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace GiftLists.IntegrationTests.GiftLists;

/// <summary>
/// Exercises <see cref="IGiftListRepository"/> directly against real Mongo (this fixture's own
/// doc comment explains why: no wire-level way to force a specific <c>shareToken</c> collision,
/// and nothing else round-trips <c>GiftListToDocumentMapper</c> for an already-expired list).
/// </summary>
[Collection(GiftListsCollection.Name)]
public sealed class GiftListRepositoryTests(GiftListsFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddAsync_ShouldReturnDuplicate_WhenTheUniqueShareTokenIndexRejectsTheInsert()
    {
        // Arrange — CONVENTIONS.md "Testing": only a real Mongo collection enforces the unique index; an
        // in-memory repository would let both inserts silently succeed. Two lists, same
        // shareToken, different ids/owners — the field the index is actually declared on.
        var now = DateTimeOffset.UtcNow;
        var sharedToken = new ShareToken(GiftListSeeding.RandomShareToken());
        var first = GiftList.Create(
            GiftListId.New(), new OwnerId(Guid.NewGuid()), new GiftListName("First"),
            new ExpiryDate(now.AddDays(7), now), sharedToken, now);
        var second = GiftList.Create(
            GiftListId.New(), new OwnerId(Guid.NewGuid()), new GiftListName("Second"),
            new ExpiryDate(now.AddDays(7), now), sharedToken, now);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var firstSaved = await repository.AddAsync(first, CancellationToken.None);
        Assert.True(firstSaved.IsSuccess);

        // Act
        var secondSaved = await repository.AddAsync(second, CancellationToken.None);

        // Assert
        Assert.True(secondSaved.IsFailure);
        Assert.Equal("giftlist.duplicate", secondSaved.Error.Code);
        var reloadedFirst = await repository.FindByIdAsync(first.Id, CancellationToken.None);
        Assert.NotNull(reloadedFirst);
        var reloadedSecond = await repository.FindByIdAsync(second.Id, CancellationToken.None);
        Assert.Null(reloadedSecond);
    }

    [Fact]
    public async Task FindByIdAsync_ShouldRoundTripEveryField_ThroughRealMongo()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var ownerId = new OwnerId(Guid.NewGuid());
        var name = new GiftListName("Birthday Wishlist");
        var expiry = new ExpiryDate(now.AddDays(7), now);
        var shareToken = new ShareToken(GiftListSeeding.RandomShareToken());
        var list = GiftList.Create(GiftListId.New(), ownerId, name, expiry, shareToken, now);
        list.AddItem(
            GiftItemId.New(),
            new GiftItemName("Coffee grinder"),
            new GiftItemDescription("Burr, not blade"),
            new GiftItemUrl("https://example.com"),
            now);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var saved = await repository.AddAsync(list, CancellationToken.None);
        Assert.True(saved.IsSuccess);

        // Act
        var reloaded = await repository.FindByIdAsync(list.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Equal(list.Id, reloaded.Id);
        Assert.Equal(ownerId, reloaded.OwnerId);
        Assert.Equal(name, reloaded.Name);
        Assert.Equal(expiry.Value, reloaded.Expiry.Value);
        Assert.Equal(shareToken, reloaded.ShareToken);
        // CreatedAt was absent despite this test's name, and it is the other half of what the
        // timestamp normalisation changed — the only field with no round-trip coverage anywhere.
        Assert.Equal(list.CreatedAt, reloaded.CreatedAt);
        var item = Assert.Single(reloaded.Items);
        var originalItem = Assert.Single(list.Items);
        Assert.Equal(originalItem.Id, item.Id);
        Assert.Equal(originalItem.Name, item.Name);
        Assert.Equal(originalItem.Description, item.Description);
        Assert.Equal(originalItem.Url, item.Url);
    }

    [Fact]
    public async Task FindByIdAsync_ShouldRehydrateAnAlreadyExpiredList_WithoutThrowing()
    {
        // Arrange — GiftList.Create/ExpiryDate's validating constructor refuse a past expiry, so
        // the only way an already-expired list exists at all is one that was valid at creation
        // and has since passed; GiftListToDocumentMapper.ToAggregate must use ExpiryDate.Rehydrate
        // (not the validating constructor) to reload it without throwing (ExpiryDate's own doc
        // comment). GiftList.Rehydrate lets this test construct that state directly, through
        // public Domain API, rather than waiting out a real clock.
        var storedAt = DateTimeOffset.UtcNow.AddDays(-30);
        var pastExpiry = storedAt.AddDays(1);
        var alreadyExpired = GiftList.Rehydrate(
            GiftListId.New(),
            new OwnerId(Guid.NewGuid()),
            new GiftListName("Long Expired"),
            ExpiryDate.Rehydrate(pastExpiry),
            new ShareToken(GiftListSeeding.RandomShareToken()),
            storedAt,
            version: 0,
            []);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var saved = await repository.AddAsync(alreadyExpired, CancellationToken.None);
        Assert.True(saved.IsSuccess);

        // Act
        var exception = await Record.ExceptionAsync(
            () => repository.FindByIdAsync(alreadyExpired.Id, CancellationToken.None));

        // Assert
        Assert.Null(exception);
        var reloaded = await repository.FindByIdAsync(alreadyExpired.Id, CancellationToken.None);
        Assert.NotNull(reloaded);
        // Compared against the aggregate's own stored value, not the raw `pastExpiry` local: the
        // claim under test is that a reload equals what was SAVED. ExpiryDate normalises to
        // millisecond precision on the way in (Timestamps.ToStoredPrecision), so comparing to the
        // un-normalised input would be asserting a precision the system deliberately does not keep.
        Assert.Equal(alreadyExpired.Expiry.Value, reloaded.Expiry.Value);
        Assert.True(reloaded.Expiry.HasExpired(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowConcurrencyException_WhenAnotherWriterSavedFirst()
    {
        // Arrange — the deterministic sibling of AddGiftItemConcurrencyTests. That test drives two
        // real commands through the broker and relies on them genuinely overlapping; on a slower or
        // single-CPU agent the handlers can serialise and it passes vacuously, proving nothing. Two
        // aggregates loaded from the same document and saved in sequence force the lost race every
        // time, on any machine (Batch 12 review).
        var now = DateTimeOffset.UtcNow;
        var list = GiftList.Create(
            GiftListId.New(), new OwnerId(Guid.NewGuid()), new GiftListName("Original"),
            new ExpiryDate(now.AddDays(7), now), new ShareToken(GiftListSeeding.RandomShareToken()), now);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        Assert.True((await repository.AddAsync(list, CancellationToken.None)).IsSuccess);

        var winner = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        var loser = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(winner);
        Assert.NotNull(loser);
        winner.Rename(new GiftListName("Winner"), now);
        loser.Rename(new GiftListName("Loser"), now);

        // Act
        await repository.UpdateAsync(winner, CancellationToken.None);
        var exception = await Record.ExceptionAsync(
            () => repository.UpdateAsync(loser, CancellationToken.None));

        // Assert
        Assert.IsType<GiftListConcurrencyException>(exception);
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("Winner", persisted.Name.Value);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowDeletedDuringUpdate_WhenTheListIsGone()
    {
        // Arrange — Mongo reports a lost race and a deleted list identically (MatchedCount == 0),
        // but they are different failures: one clears on redelivery, the other never can. Without
        // this test the two are indistinguishable and the concurrency exception's promise that
        // retrying resolves it is false for half the cases it covers (Batch 12 review).
        var now = DateTimeOffset.UtcNow;
        var list = GiftList.Create(
            GiftListId.New(), new OwnerId(Guid.NewGuid()), new GiftListName("Doomed"),
            new ExpiryDate(now.AddDays(7), now), new ShareToken(GiftListSeeding.RandomShareToken()), now);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        Assert.True((await repository.AddAsync(list, CancellationToken.None)).IsSuccess);

        var loaded = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        loaded.Rename(new GiftListName("Too Late"), now);
        await repository.DeleteAsync(list.Id, CancellationToken.None);

        // Act
        var exception = await Record.ExceptionAsync(
            () => repository.UpdateAsync(loaded, CancellationToken.None));

        // Assert
        Assert.IsType<GiftListDeletedDuringUpdateException>(exception);
    }

    [Fact]
    public async Task UpdateAsync_ShouldSucceed_WhenTheListIsSavedWithoutHavingBeenMutated()
    {
        // Arrange — GiftList.Rehydrate's doc comment promises that reloading and immediately saving
        // without mutating does not manufacture a conflict against itself. While the repository
        // derived the expected stored version as `Version - 1`, that promise was FALSE: zero
        // mutations expected `stored - 1` and always conflicted. No production caller does this
        // today, which is why nothing caught it and why the comment could go on claiming otherwise
        // (Batch 12 review). This test is what makes the promise checkable.
        //
        // It no longer pins that promise via the version guard, and the GL-68 review caught the
        // comment still claiming it did: reverting the guard to the GL-64-era `Version - 1` now
        // leaves this test GREEN, because zero mutations means zero domain events, so ToUpdate
        // returns null and UpdateAsync returns without issuing any Mongo command at all. The
        // promise is now kept by that early return instead, which is what this test actually
        // guards — replace the early return with a throw and it fails. What still distinguishes
        // OriginalVersion from Version - 1 is
        // UpdateAsync_ShouldSucceed_WhenTwoMutationsArePerformedBeforeOneSave alone.
        var now = DateTimeOffset.UtcNow;
        var list = GiftList.Create(
            GiftListId.New(), new OwnerId(Guid.NewGuid()), new GiftListName("Untouched"),
            new ExpiryDate(now.AddDays(7), now), new ShareToken(GiftListSeeding.RandomShareToken()), now);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        Assert.True((await repository.AddAsync(list, CancellationToken.None)).IsSuccess);
        var loaded = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(loaded);

        // Act
        var exception = await Record.ExceptionAsync(
            () => repository.UpdateAsync(loaded, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task UpdateAsync_ShouldSucceed_WhenTwoMutationsArePerformedBeforeOneSave()
    {
        // The other half of the same unstated rule: two mutations before one save expected
        // `stored + 1` and conflicted spuriously. Nothing forbids an interactor doing two things
        // to an aggregate before saving it once; the repository simply assumed none ever would.
        var now = DateTimeOffset.UtcNow;
        var list = GiftList.Create(
            GiftListId.New(), new OwnerId(Guid.NewGuid()), new GiftListName("Original"),
            new ExpiryDate(now.AddDays(7), now), new ShareToken(GiftListSeeding.RandomShareToken()), now);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        Assert.True((await repository.AddAsync(list, CancellationToken.None)).IsSuccess);
        var loaded = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(loaded);

        // Act — two mutations, one save
        loaded.Rename(new GiftListName("Renamed Once"), now);
        loaded.AddItem(GiftItemId.New(), new GiftItemName("Coffee grinder"), null, null, now);
        var exception = await Record.ExceptionAsync(
            () => repository.UpdateAsync(loaded, CancellationToken.None));

        // Assert
        Assert.Null(exception);
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("Renamed Once", persisted.Name.Value);
        Assert.Single(persisted.Items);
    }

    [Fact]
    public async Task UpdateAsync_ShouldSucceedWithoutConflicting_WhenAConcurrentWriterAddedADifferentItem()
    {
        // Arrange — the point of GL-68, and deliberately NOT "both items survive": under the
        // whole-document replace this test was written against, both items DID survive, because
        // the loser threw GiftListConcurrencyException, Rebus redelivered it and the retry
        // reapplied it (AddGiftItemConcurrencyTests asserts exactly that end state and cannot
        // tell the two apart). What this pins is that no conflict happens at all — two adds of
        // DIFFERENT items touch disjoint array elements, so neither writer has any reason to be
        // redone. The throw is the sole cause of the redelivery (GiftListConcurrencyException's
        // own doc comment), so "UpdateAsync did not throw" is precisely "was not retried",
        // observed at the layer that decides it rather than inferred from what ended up stored.
        //
        // Both assertions below are load-bearing and neither is redundant. Assert.Null pins "no
        // conflict": since the GL-68 review removed the idempotent-success early return, the only
        // way UpdateAsync returns normally is a write that actually applied. The item count pins
        // that the write applied to the right thing — a $push that silently dropped one writer's
        // element, or an update that matched but changed nothing, would still not throw.
        //
        // Deterministic on any machine, for the same reason
        // UpdateAsync_ShouldThrowConcurrencyException_WhenAnotherWriterSavedFirst is: two
        // aggregates loaded from one document and saved in sequence force the overlap every run,
        // where two racing wire commands may simply serialise and pass vacuously.
        var now = DateTimeOffset.UtcNow;
        var list = GiftList.Create(
            GiftListId.New(), new OwnerId(Guid.NewGuid()), new GiftListName("Shared"),
            new ExpiryDate(now.AddDays(7), now), new ShareToken(GiftListSeeding.RandomShareToken()), now);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        Assert.True((await repository.AddAsync(list, CancellationToken.None)).IsSuccess);

        var firstWriter = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        var secondWriter = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(firstWriter);
        Assert.NotNull(secondWriter);
        var firstItemId = GiftItemId.New();
        var secondItemId = GiftItemId.New();
        firstWriter.AddItem(firstItemId, new GiftItemName("Coffee grinder"), null, null, now);
        secondWriter.AddItem(secondItemId, new GiftItemName("Board game"), null, null, now);

        // Act
        await repository.UpdateAsync(firstWriter, CancellationToken.None);
        var exception = await Record.ExceptionAsync(
            () => repository.UpdateAsync(secondWriter, CancellationToken.None));

        // Assert
        Assert.Null(exception);
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted.Items.Count);
        Assert.Single(persisted.Items, item => item.Id == firstItemId);
        Assert.Single(persisted.Items, item => item.Id == secondItemId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowConcurrencyException_WhenTheSameItemWasAlreadyAddedConcurrently()
    {
        // Arrange — the deterministic guarantee behind
        // AddGiftItemConcurrencyTests.AddGiftItem_ShouldPublishGiftItemAddedV1Once..., which needs
        // a real race to mean anything. GL-20's guard in AddGiftItemInteractor is a read-then-check
        // and two overlapping deliveries of one message can both pass it; while UpdateAsync
        // replaced the whole document, the version guard rejected the second write. A bare $push
        // has no such guard, so an element-scoped precondition replaces it — but the precondition
        // failing must be reported as a CONFLICT, not quietly absorbed as "the desired state
        // already holds". Storage-wise absorbing it looks right and the list does end up with one
        // item either way; what it loses is the signal, and the interactor publishes
        // unconditionally on the line after this call, so absorbing it means a duplicate
        // GiftItemAddedV1 on the wire carrying a later AddedAt (GL-68 review). Throwing is what
        // keeps the publish from happening at all, exactly as it did before GL-68 — the retry then
        // re-reads, GL-20's guard sees the item, and the redelivery no-ops without publishing.
        //
        // Same client-generated ItemId on both writers is what a redelivery looks like from here.
        var now = DateTimeOffset.UtcNow;
        var list = GiftList.Create(
            GiftListId.New(), new OwnerId(Guid.NewGuid()), new GiftListName("Shared"),
            new ExpiryDate(now.AddDays(7), now), new ShareToken(GiftListSeeding.RandomShareToken()), now);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        Assert.True((await repository.AddAsync(list, CancellationToken.None)).IsSuccess);

        var firstWriter = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        var secondWriter = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(firstWriter);
        Assert.NotNull(secondWriter);
        var itemId = GiftItemId.New();
        firstWriter.AddItem(itemId, new GiftItemName("Coffee grinder"), null, null, now);
        secondWriter.AddItem(itemId, new GiftItemName("Coffee grinder"), null, null, now);

        // Act
        await repository.UpdateAsync(firstWriter, CancellationToken.None);
        var exception = await Record.ExceptionAsync(
            () => repository.UpdateAsync(secondWriter, CancellationToken.None));

        // Assert
        Assert.IsType<GiftListConcurrencyException>(exception);
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        var only = Assert.Single(persisted.Items);
        Assert.Equal(itemId, only.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowConcurrencyException_WhenTheSameItemWasAlreadyRemovedConcurrently()
    {
        // Arrange — the mirror of the same-item add above, and a regression GL-68 introduced in
        // its first revision. $pull is idempotent in storage, so an unguarded duplicate remove
        // "succeeds" having changed nothing; RemoveGiftItemInteractor then publishes a second
        // GiftItemRemovedV1 with its own later RemovedAt. Before GL-68 the version guard failed
        // that write and no second event went out. Requiring the element to still be present
        // restores it: the duplicate conflicts, and its retry stops at the interactor's
        // ItemNotFound check without publishing.
        var now = DateTimeOffset.UtcNow;
        var list = GiftList.Create(
            GiftListId.New(), new OwnerId(Guid.NewGuid()), new GiftListName("Shared"),
            new ExpiryDate(now.AddDays(7), now), new ShareToken(GiftListSeeding.RandomShareToken()), now);
        var itemId = GiftItemId.New();
        list.AddItem(itemId, new GiftItemName("Coffee grinder"), null, null, now);
        list.ClearDomainEvents();

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        Assert.True((await repository.AddAsync(list, CancellationToken.None)).IsSuccess);

        var firstWriter = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        var secondWriter = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(firstWriter);
        Assert.NotNull(secondWriter);
        firstWriter.RemoveItem(itemId, now);
        secondWriter.RemoveItem(itemId, now);

        // Act
        await repository.UpdateAsync(firstWriter, CancellationToken.None);
        var exception = await Record.ExceptionAsync(
            () => repository.UpdateAsync(secondWriter, CancellationToken.None));

        // Assert
        Assert.IsType<GiftListConcurrencyException>(exception);
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Empty(persisted.Items);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenTheAggregateWasMutatedButItsEventsWereAlreadyCleared()
    {
        // Arrange — GL-68 made "save before publishing and clearing the events" a load-bearing
        // precondition of the port for the first time: the events ARE the record of what to write,
        // so an aggregate that arrives here mutated but eventless would otherwise persist nothing
        // at all, silently, and report success. That is the one failure mode this design trades
        // for its concurrency, so it is a bug rather than an expected outcome (CONVENTIONS.md "Errors")
        // and it fails loudly. Every interactor already saves first and publishes second
        // (ARCHITECTURE.md "Event publishing: synchronous"), so nothing does this today — which is exactly why it needs a
        // test rather than a comment.
        var now = DateTimeOffset.UtcNow;
        var list = GiftList.Create(
            GiftListId.New(), new OwnerId(Guid.NewGuid()), new GiftListName("Original"),
            new ExpiryDate(now.AddDays(7), now), new ShareToken(GiftListSeeding.RandomShareToken()), now);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        Assert.True((await repository.AddAsync(list, CancellationToken.None)).IsSuccess);
        var loaded = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        loaded.Rename(new GiftListName("Renamed"), now);
        loaded.ClearDomainEvents();

        // Act
        var exception = await Record.ExceptionAsync(
            () => repository.UpdateAsync(loaded, CancellationToken.None));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("Original", persisted.Name.Value);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenOneSaveBothAddsAndRemovesItems()
    {
        // Arrange — $push and $pull collide on the same 'items' path, so one save cannot carry
        // both. No interactor does this (each mutates one item), but the mapper accepts whatever
        // events the aggregate raised, so the unsupported combination has to fail as a stated bug
        // rather than as a cryptic Mongo write error from deep inside the driver.
        var now = DateTimeOffset.UtcNow;
        var list = GiftList.Create(
            GiftListId.New(), new OwnerId(Guid.NewGuid()), new GiftListName("Original"),
            new ExpiryDate(now.AddDays(7), now), new ShareToken(GiftListSeeding.RandomShareToken()), now);
        var existingItemId = GiftItemId.New();
        list.AddItem(existingItemId, new GiftItemName("Coffee grinder"), null, null, now);
        list.ClearDomainEvents();

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        Assert.True((await repository.AddAsync(list, CancellationToken.None)).IsSuccess);
        var loaded = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(loaded);

        // Act — one save carrying both an add and a remove
        loaded.AddItem(GiftItemId.New(), new GiftItemName("Board game"), null, null, now);
        loaded.RemoveItem(existingItemId, now);
        var exception = await Record.ExceptionAsync(
            () => repository.UpdateAsync(loaded, CancellationToken.None));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }
}
