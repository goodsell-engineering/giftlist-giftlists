using GiftLists.Application.GiftLists.RenameGiftList;
using GiftLists.Domain.GiftLists;
using GiftLists.UnitTests.Support;

namespace GiftLists.UnitTests.GiftLists.RenameGiftList;

public sealed class RenameGiftListInteractorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = new(Guid.NewGuid());

    private static GiftList SeededList(FakeGiftListRepository repository, OwnerId ownerId)
    {
        var list = GiftList.Create(
            GiftListId.New(), ownerId, new GiftListName("Old Name"),
            new ExpiryDate(Now.AddDays(7), Now), new ShareToken(new string('a', ShareToken.Length)), Now);
        list.ClearDomainEvents();
        repository.Seed(list);
        return list;
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenNoListExistsWithTheGivenId()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var interactor = new RenameGiftListInteractor(repository, new FakeDomainEventPublisher(), new FakeClock(Now));
        var request = new RenameGiftListRequest(Guid.NewGuid(), Owner.Value, "New Name");

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
        var list = SeededList(repository, Owner);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new RenameGiftListInteractor(repository, publisher, new FakeClock(Now));
        var nonOwnerId = Guid.NewGuid();
        var request = new RenameGiftListRequest(list.Id.Value, nonOwnerId, "New Name");

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.forbidden", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldRenameAndPublish_WhenRequesterIsTheOwner()
    {
        // Arrange
        var callLog = new CallLog();
        var repository = new FakeGiftListRepository { SharedLog = callLog };
        var list = SeededList(repository, Owner);
        var publisher = new FakeDomainEventPublisher { SharedLog = callLog };
        var interactor = new RenameGiftListInteractor(repository, publisher, new FakeClock(Now));
        var request = new RenameGiftListRequest(list.Id.Value, Owner.Value, "New Name");

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value.Name);
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
