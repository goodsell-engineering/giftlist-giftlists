using GiftLists.Application.GiftLists.AddGiftItem;
using GiftLists.Domain.GiftLists;
using GiftLists.UnitTests.Support;

namespace GiftLists.UnitTests.GiftLists.AddGiftItem;

public sealed class AddGiftItemInteractorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = new(Guid.NewGuid());

    private static GiftList SeededList(FakeGiftListRepository repository, OwnerId ownerId, DateTimeOffset expiresAt)
    {
        var list = GiftList.Create(
            GiftListId.New(), ownerId, new GiftListName("Birthday Wishlist"),
            new ExpiryDate(expiresAt, Now), new ShareToken(new string('a', ShareToken.Length)), Now);
        list.ClearDomainEvents();
        repository.Seed(list);
        return list;
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenNoListExistsWithTheGivenId()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var interactor = new AddGiftItemInteractor(repository, new FakeDomainEventPublisher(), new FakeClock(Now));
        var request = new AddGiftItemRequest(Guid.NewGuid(), Owner.Value, Guid.NewGuid(), "Coffee grinder", null, null);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.not_found", result.Error.Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenRequesterIsNotTheOwner()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var list = SeededList(repository, Owner, Now.AddDays(7));
        var publisher = new FakeDomainEventPublisher();
        var interactor = new AddGiftItemInteractor(repository, publisher, new FakeClock(Now));
        var request = new AddGiftItemRequest(list.Id.Value, Guid.NewGuid(), Guid.NewGuid(), "Coffee grinder", null, null);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.forbidden", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldReturnExpired_WhenTheListHasAlreadyExpired()
    {
        // Arrange — the clock reads a moment after the list's own expiry.
        var repository = new FakeGiftListRepository();
        var list = SeededList(repository, Owner, Now.AddDays(7));
        var publisher = new FakeDomainEventPublisher();
        var afterExpiry = new FakeClock(list.Expiry.Value.AddSeconds(1));
        var interactor = new AddGiftItemInteractor(repository, publisher, afterExpiry);
        var request = new AddGiftItemRequest(list.Id.Value, Owner.Value, Guid.NewGuid(), "Coffee grinder", null, null);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.expired", result.Error.Code);
        Assert.DoesNotContain(nameof(FakeGiftListRepository.UpdateAsync), repository.Calls);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldAddItemAndPublish_WhenListIsNotExpiredAndRequesterIsOwner()
    {
        // Arrange
        var callLog = new CallLog();
        var repository = new FakeGiftListRepository { SharedLog = callLog };
        var list = SeededList(repository, Owner, Now.AddDays(7));
        var publisher = new FakeDomainEventPublisher { SharedLog = callLog };
        var interactor = new AddGiftItemInteractor(repository, publisher, new FakeClock(Now));
        var itemId = Guid.NewGuid();
        var request = new AddGiftItemRequest(list.Id.Value, Owner.Value, itemId, "Coffee grinder", "Burr", "https://example.com");

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(itemId, result.Value.ItemId);
        // The full call sequence, via one CallLog shared by both fakes — not Assert.Contains on the
        // repository plus Assert.Single on the publisher, which is what stood here. That pair shows
        // each happened and can never show the ORDER, so it stayed green with save-then-publish
        // inverted. ARCHITECTURE.md "Event publishing: synchronous"'s ordering applies to all five interactors, and only
        // CreateGiftList had a test that could fail on it (Batch 11 review).
        Assert.Equal(
            [
                nameof(FakeGiftListRepository.FindByIdAsync),
                nameof(FakeGiftListRepository.UpdateAsync),
                nameof(FakeDomainEventPublisher.PublishAsync),
            ],
            callLog.Entries);
    }

    [Fact]
    public async Task Handle_ShouldTreatWhitespaceOnlyDescriptionAndUrlAsAbsent()
    {
        // Arrange — GiftItemDescription/GiftItemUrl's validating constructors reject a blank
        // ARGUMENT outright (ArgumentException.ThrowIfNullOrWhiteSpace), while
        // AddGiftItemValidator's IsValidLength/IsValid predicates treat blank as "no value"
        // (valid) because the field is optional. A validator-approved request with a
        // whitespace-only Description/Url must not then throw here — this interactor's own
        // comment is where that mismatch is reconciled, by normalising blank to null BEFORE
        // either constructor runs.
        var repository = new FakeGiftListRepository();
        var list = SeededList(repository, Owner, Now.AddDays(7));
        var publisher = new FakeDomainEventPublisher();
        var interactor = new AddGiftItemInteractor(repository, publisher, new FakeClock(Now));
        var itemId = Guid.NewGuid();
        var request = new AddGiftItemRequest(list.Id.Value, Owner.Value, itemId, "Coffee grinder", "   ", "   ");

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var item = Assert.Single(list.Items);
        Assert.Null(item.Description);
        Assert.Null(item.Url);
    }

    [Fact]
    public async Task Handle_ShouldAddTheItemOnce_WhenTheSameMessageIsRedelivered()
    {
        // Arrange — delivery is at-least-once and GL-54 was closed without the shared
        // processedMessages inbox, so "safe to run twice" is the ONLY line of defence, not a second
        // one. ItemId is client-generated and stable across redeliveries, so handling the identical
        // request twice must leave one item, not two. GiftList.AddItem appends unconditionally, so
        // without the interactor's guard this produced one list holding the same item twice.
        var repository = new FakeGiftListRepository();
        var list = SeededList(repository, Owner, Now.AddDays(7));
        var publisher = new FakeDomainEventPublisher();
        var interactor = new AddGiftItemInteractor(repository, publisher, new FakeClock(Now));
        var request = new AddGiftItemRequest(
            list.Id.Value, Owner.Value, Guid.NewGuid(), "Coffee grinder", "Burr", "https://example.com");

        // Act
        var first = await interactor.Handle(request, CancellationToken.None);
        var second = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.ItemId, second.Value.ItemId);
        Assert.Single(list.Items);
    }
}
