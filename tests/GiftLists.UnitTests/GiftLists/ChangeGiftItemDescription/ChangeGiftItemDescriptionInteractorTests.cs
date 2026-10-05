using GiftLists.Application.GiftLists.ChangeGiftItemDescription;
using GiftLists.Domain.GiftLists;
using GiftLists.UnitTests.Support;

namespace GiftLists.UnitTests.GiftLists.ChangeGiftItemDescription;

public sealed class ChangeGiftItemDescriptionInteractorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = new(Guid.NewGuid());

    private static (GiftList List, GiftItemId ItemId) SeededListWithItem(
        FakeGiftListRepository repository, OwnerId ownerId, GiftItemDescription? description = null, DateTimeOffset? expiresAt = null)
    {
        var list = GiftList.Create(
            GiftListId.New(), ownerId, new GiftListName("Birthday Wishlist"),
            new ExpiryDate(expiresAt ?? Now.AddDays(7), Now), new ShareToken(new string('a', ShareToken.Length)), Now);
        var itemId = GiftItemId.New();
        list.AddItem(itemId, new GiftItemName("Coffee grinder"), description, null, Now);
        list.ClearDomainEvents();
        repository.Seed(list);
        return (list, itemId);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenNoListExistsWithTheGivenId()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var interactor = new ChangeGiftItemDescriptionInteractor(repository, new FakeDomainEventPublisher(), new FakeClock(Now));
        var request = new ChangeGiftItemDescriptionRequest(Guid.NewGuid(), Owner.Value, Guid.NewGuid(), "Size M");

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.not_found", result.Error.Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbiddenAndPublishNothing_WhenRequesterIsNotTheOwner()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var (list, itemId) = SeededListWithItem(repository, Owner);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ChangeGiftItemDescriptionInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftItemDescriptionRequest(list.Id.Value, Guid.NewGuid(), itemId.Value, "Size M");

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.forbidden", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldReturnItemNotFound_WhenTheItemIsNotOnTheList()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var (list, _) = SeededListWithItem(repository, Owner);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ChangeGiftItemDescriptionInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftItemDescriptionRequest(list.Id.Value, Owner.Value, Guid.NewGuid(), "Size M");

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.item_not_found", result.Error.Code);
        Assert.DoesNotContain(nameof(FakeGiftListRepository.UpdateAsync), repository.Calls);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldReturnExpiredAndPublishNothing_WhenTheListHasExpired()
    {
        // Arrange — Rehydrate, not Create, because ExpiryDate's validating constructor refuses a
        // past date, mirroring ChangeGiftListExpiryInteractorTests' "already expired" seeding.
        var repository = new FakeGiftListRepository();
        var list = GiftList.Rehydrate(
            GiftListId.New(), Owner, new GiftListName("Wishlist"),
            ExpiryDate.Rehydrate(Now.AddDays(-1)), new ShareToken(new string('a', ShareToken.Length)),
            Now.AddDays(-30), version: 0, []);
        var itemId = GiftItemId.New();
        list.AddItem(itemId, new GiftItemName("Coffee grinder"), null, null, Now.AddDays(-30));
        repository.Seed(list);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ChangeGiftItemDescriptionInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftItemDescriptionRequest(list.Id.Value, Owner.Value, itemId.Value, "Size M");

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.expired", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldSaveNothingAndPublishNothing_WhenTheTrimmedValueEqualsTheCurrentOne()
    {
        // Arrange — a redelivered command (CONVENTIONS.md "Messaging": at-least-once) must not
        // raise a second GiftItemDescriptionChanged for a change that already happened. Compared
        // null-safely and by value, so leading/trailing whitespace does not defeat the guard.
        var repository = new FakeGiftListRepository();
        var (list, itemId) = SeededListWithItem(repository, Owner, new GiftItemDescription("Size M"));
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ChangeGiftItemDescriptionInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftItemDescriptionRequest(list.Id.Value, Owner.Value, itemId.Value, "  Size M  ");

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(publisher.PublishedBatches);
        Assert.DoesNotContain(nameof(FakeGiftListRepository.UpdateAsync), repository.Calls);
    }

    [Fact]
    public async Task Handle_ShouldSaveNothingAndPublishNothing_WhenClearingAnAlreadyEmptyDescription()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var (list, itemId) = SeededListWithItem(repository, Owner, description: null);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ChangeGiftItemDescriptionInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftItemDescriptionRequest(list.Id.Value, Owner.Value, itemId.Value, "   ");

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(publisher.PublishedBatches);
        Assert.DoesNotContain(nameof(FakeGiftListRepository.UpdateAsync), repository.Calls);
    }

    [Fact]
    public async Task Handle_ShouldSaveThenPublishOneEvent_WhenTheValueChanges()
    {
        // Arrange
        var callLog = new CallLog();
        var repository = new FakeGiftListRepository { SharedLog = callLog };
        var (list, itemId) = SeededListWithItem(repository, Owner, new GiftItemDescription("Size M"));
        var publisher = new FakeDomainEventPublisher { SharedLog = callLog };
        var interactor = new ChangeGiftItemDescriptionInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftItemDescriptionRequest(list.Id.Value, Owner.Value, itemId.Value, "Size L");

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Size L", Assert.Single(list.Items).Description?.Value);
        Assert.Single(publisher.PublishedBatches);
        // Save-then-publish ordering, via the one CallLog both fakes share — see
        // RemoveGiftItemInteractorTests for why the pair of per-fake assertions is not enough.
        Assert.Equal(
            [
                nameof(FakeGiftListRepository.FindByIdAsync),
                nameof(FakeGiftListRepository.UpdateAsync),
                nameof(FakeDomainEventPublisher.PublishAsync),
            ],
            callLog.Entries);
    }
}
