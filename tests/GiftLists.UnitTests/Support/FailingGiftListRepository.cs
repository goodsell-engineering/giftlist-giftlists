using BuildingBlocks.Results;
using GiftLists.Application.GiftLists;
using GiftLists.Domain.GiftLists;

namespace GiftLists.UnitTests.Support;

/// <summary>Hand-written fake whose <see cref="AddAsync"/> always reports the unique-index collision a real Mongo write would (<see cref="GiftListErrors.Duplicate"/>'s own doc comment) — the write-failure path <see cref="FakeGiftListRepository"/> cannot itself provoke.</summary>
internal sealed class FailingGiftListRepository : IGiftListRepository
{
    public Task<GiftList?> FindByIdAsync(GiftListId id, CancellationToken cancellationToken) =>
        Task.FromResult<GiftList?>(null);

    public Task<Result> AddAsync(GiftList list, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(GiftListErrors.Duplicate));

    public Task UpdateAsync(GiftList list, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DeleteAsync(GiftListId id, CancellationToken cancellationToken) => Task.CompletedTask;
}
