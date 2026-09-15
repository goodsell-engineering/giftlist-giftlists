using GiftLists.Application.GiftLists.CreateGiftList;
using GiftLists.UnitTests.Support;

namespace GiftLists.UnitTests.GiftLists.CreateGiftList;

/// <summary>
/// The success/failure shape Identity.IntegrationTests' request/reply tests get for free
/// (CONVENTIONS.md "Testing") is unreachable for GiftLists' fire-and-forget commands (ARCHITECTURE.md
/// "Command → event flow") — nothing waits on a reply to observe a <c>Result</c>, so this suite exercises the
/// interactor directly against hand-written fakes instead.
/// </summary>
public sealed class CreateGiftListInteractorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ShouldSaveAndPublish_AndReturnTheNewListsShareToken()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var publisher = new FakeDomainEventPublisher();
        var interactor = new CreateGiftListInteractor(
            repository, publisher, new FakeShareTokenGenerator(), new FakeClock(Now));
        var request = new CreateGiftListRequest(Guid.NewGuid(), Guid.NewGuid(), "Birthday Wishlist", Now.AddDays(7));

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(request.ListId, result.Value.ListId);
        Assert.Equal(request.ExpiresAt, result.Value.ExpiresAt);
        Assert.Equal(21, result.Value.ShareToken.Length);
        Assert.Equal([nameof(FakeGiftListRepository.AddAsync)], repository.Calls);
        Assert.Single(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldSaveToTheRepository_BeforePublishingTheDomainEvents()
    {
        // Arrange — ARCHITECTURE.md "Event publishing: synchronous": save first, publish second, because an event describing
        // a write that never happened is worse than a write nobody heard about. One CallLog shared
        // by BOTH fakes is what makes the order observable; asserting on repository.Calls and
        // publisher.PublishedBatches separately can only show that each happened, which is what the
        // test above does and why its name overstated it.
        //
        // This lives here rather than in IntegrationTests deliberately. The integration test that
        // named this rule observed the published event and then read Mongo, and it passes 3/3 with
        // the ordering inverted: the event has to cross a real broker, which is slower than the
        // local write it was racing. Ordering inside one method is a deterministic fact and belongs
        // in a deterministic test, not one that has to out-race a network hop to notice a bug.
        var callLog = new CallLog();
        var repository = new FakeGiftListRepository { SharedLog = callLog };
        var publisher = new FakeDomainEventPublisher { SharedLog = callLog };
        var interactor = new CreateGiftListInteractor(
            repository, publisher, new FakeShareTokenGenerator(), new FakeClock(Now));
        var request = new CreateGiftListRequest(Guid.NewGuid(), Guid.NewGuid(), "Birthday Wishlist", Now.AddDays(7));

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(
            [nameof(FakeGiftListRepository.AddAsync), nameof(FakeDomainEventPublisher.PublishAsync)],
            callLog.Entries);
    }

    [Fact]
    public async Task Handle_ShouldReturnTheRepositoryFailure_WhenAddAsyncFails()
    {
        // Arrange — a duplicate id/shareToken collision (GiftListErrors.Duplicate) is the
        // repository's job to detect (its own doc comment); the interactor only has to propagate
        // it rather than publish anything for a write that never happened.
        var repository = new FailingGiftListRepository();
        var publisher = new FakeDomainEventPublisher();
        var interactor = new CreateGiftListInteractor(
            repository, publisher, new FakeShareTokenGenerator(), new FakeClock(Now));
        var request = new CreateGiftListRequest(Guid.NewGuid(), Guid.NewGuid(), "Birthday Wishlist", Now.AddDays(7));

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.duplicate", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
    }
}
