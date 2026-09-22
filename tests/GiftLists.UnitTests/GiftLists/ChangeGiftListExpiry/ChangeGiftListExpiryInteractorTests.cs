using GiftLists.Application.GiftLists.ChangeGiftListExpiry;
using GiftLists.Domain.GiftLists;
using GiftLists.UnitTests.Support;

namespace GiftLists.UnitTests.GiftLists.ChangeGiftListExpiry;

public sealed class ChangeGiftListExpiryInteractorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OriginalExpiresAt = Now.AddDays(7);
    private static readonly DateTimeOffset NewExpiresAt = Now.AddDays(30);
    private static readonly OwnerId Owner = new(Guid.NewGuid());

    private static GiftList SeededList(FakeGiftListRepository repository, OwnerId ownerId, DateTimeOffset expiresAt)
    {
        var list = GiftList.Create(
            GiftListId.New(), ownerId, new GiftListName("Wishlist"),
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
        var interactor = new ChangeGiftListExpiryInteractor(repository, new FakeDomainEventPublisher(), new FakeClock(Now));
        var request = new ChangeGiftListExpiryRequest(Guid.NewGuid(), Owner.Value, NewExpiresAt);

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
        var list = SeededList(repository, Owner, OriginalExpiresAt);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ChangeGiftListExpiryInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftListExpiryRequest(list.Id.Value, Guid.NewGuid(), NewExpiresAt);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.forbidden", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldMoveTheExpiryAndPublish_WhenRequesterIsTheOwner()
    {
        // Arrange
        var callLog = new CallLog();
        var repository = new FakeGiftListRepository { SharedLog = callLog };
        var list = SeededList(repository, Owner, OriginalExpiresAt);
        var publisher = new FakeDomainEventPublisher { SharedLog = callLog };
        var interactor = new ChangeGiftListExpiryInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftListExpiryRequest(list.Id.Value, Owner.Value, NewExpiresAt);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(NewExpiresAt, result.Value.ExpiresAt);
        Assert.Equal(NewExpiresAt, list.Expiry.Value);
        Assert.Single(publisher.PublishedBatches);
        // Save-then-publish ordering, via the one CallLog both fakes share — see
        // RenameGiftListInteractorTests for why the pair of per-fake assertions is not enough.
        Assert.Equal(
            [
                nameof(FakeGiftListRepository.FindByIdAsync),
                nameof(FakeGiftListRepository.UpdateAsync),
                nameof(FakeDomainEventPublisher.PublishAsync),
            ],
            callLog.Entries);
    }

    [Fact]
    public async Task Handle_ShouldNeitherSaveNorPublish_WhenTheExpiryIsAlreadyWhereTheRequestAsks()
    {
        // Arrange — a redelivered command (CONVENTIONS.md "Messaging": at-least-once) must not
        // raise a second GiftListExpiryChanged for a change that already happened.
        var repository = new FakeGiftListRepository();
        var list = SeededList(repository, Owner, NewExpiresAt);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ChangeGiftListExpiryInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftListExpiryRequest(list.Id.Value, Owner.Value, NewExpiresAt);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(NewExpiresAt, result.Value.ExpiresAt);
        Assert.Empty(publisher.PublishedBatches);
        Assert.DoesNotContain(nameof(FakeGiftListRepository.UpdateAsync), repository.Calls);
    }

    [Fact]
    public async Task Handle_ShouldMoveTheExpiry_WhenTheListHasAlreadyExpired()
    {
        // Arrange — reviving an expired list is allowed; only the NEW expiry must be in the
        // future (GiftList.ChangeExpiry's own doc comment).
        var createdAt = Now.AddDays(-30);
        var repository = new FakeGiftListRepository();
        var list = GiftList.Rehydrate(
            GiftListId.New(), Owner, new GiftListName("Wishlist"),
            ExpiryDate.Rehydrate(Now.AddDays(-1)), new ShareToken(new string('a', ShareToken.Length)),
            createdAt, version: 0, []);
        repository.Seed(list);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ChangeGiftListExpiryInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftListExpiryRequest(list.Id.Value, Owner.Value, NewExpiresAt);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(list.Expiry.HasExpired(Now));
        Assert.Single(publisher.PublishedBatches);
    }
}
