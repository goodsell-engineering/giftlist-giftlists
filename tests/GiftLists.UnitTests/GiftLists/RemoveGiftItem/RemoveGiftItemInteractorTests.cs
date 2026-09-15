using GiftLists.Application.GiftLists.RemoveGiftItem;
using GiftLists.Domain.GiftLists;
using GiftLists.UnitTests.Support;

namespace GiftLists.UnitTests.GiftLists.RemoveGiftItem;

public sealed class RemoveGiftItemInteractorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = new(Guid.NewGuid());

    private static (GiftList List, GiftItemId ItemId) SeededListWithItem(FakeGiftListRepository repository, OwnerId ownerId)
    {
        var list = GiftList.Create(
            GiftListId.New(), ownerId, new GiftListName("Birthday Wishlist"),
            new ExpiryDate(Now.AddDays(7), Now), new ShareToken(new string('a', ShareToken.Length)), Now);
        var itemId = GiftItemId.New();
        list.AddItem(itemId, new GiftItemName("Coffee grinder"), null, null, Now);
        list.ClearDomainEvents();
        repository.Seed(list);
        return (list, itemId);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenNoListExistsWithTheGivenId()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var interactor = new RemoveGiftItemInteractor(repository, new FakeDomainEventPublisher(), new FakeClock(Now));
        var request = new RemoveGiftItemRequest(Guid.NewGuid(), Owner.Value, Guid.NewGuid());

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
        var (list, itemId) = SeededListWithItem(repository, Owner);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new RemoveGiftItemInteractor(repository, publisher, new FakeClock(Now));
        var request = new RemoveGiftItemRequest(list.Id.Value, Guid.NewGuid(), itemId.Value);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.forbidden", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldReturnItemNotFound_WhenTheItemDoesNotExistOnTheList()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var (list, _) = SeededListWithItem(repository, Owner);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new RemoveGiftItemInteractor(repository, publisher, new FakeClock(Now));
        var request = new RemoveGiftItemRequest(list.Id.Value, Owner.Value, Guid.NewGuid());

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.item_not_found", result.Error.Code);
        Assert.DoesNotContain(nameof(FakeGiftListRepository.UpdateAsync), repository.Calls);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldRemoveItemAndPublish_WhenItemExistsAndRequesterIsOwner()
    {
        // Arrange
        var callLog = new CallLog();
        var repository = new FakeGiftListRepository { SharedLog = callLog };
        var (list, itemId) = SeededListWithItem(repository, Owner);
        var publisher = new FakeDomainEventPublisher { SharedLog = callLog };
        var interactor = new RemoveGiftItemInteractor(repository, publisher, new FakeClock(Now));
        var request = new RemoveGiftItemRequest(list.Id.Value, Owner.Value, itemId.Value);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
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
}
