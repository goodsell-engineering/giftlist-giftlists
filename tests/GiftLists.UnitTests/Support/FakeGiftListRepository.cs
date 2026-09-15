using BuildingBlocks.Results;
using GiftLists.Application.GiftLists;
using GiftLists.Domain.GiftLists;

namespace GiftLists.UnitTests.Support;

/// <summary>
/// Hand-written in-memory fake (CONVENTIONS.md "Testing" — preferred over a mock for a port used across
/// many interactor tests). Only for <c>GiftLists.UnitTests</c>: the IntegrationTests suite bans
/// in-memory repositories outright, since only a real Mongo collection enforces the unique
/// <c>shareToken</c> index.
/// </summary>
internal sealed class FakeGiftListRepository : IGiftListRepository
{
    private readonly Dictionary<Guid, GiftList> _lists = [];

    /// <summary>Every call this fake received, in order — lets a test assert repository-then-publisher call ordering without a real broker.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Set to the SAME <see cref="CallLog"/> as the publisher fake to assert cross-port call order.</summary>
    public CallLog? SharedLog { get; init; }

    /// <summary>Puts a list directly into the fake's store, bypassing <see cref="AddAsync"/>, so test setup ("a list already exists") is not itself recorded in <see cref="Calls"/>.</summary>
    public void Seed(GiftList list) => _lists[list.Id.Value] = list;

    public Task<GiftList?> FindByIdAsync(GiftListId id, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(FindByIdAsync));
        SharedLog?.Record(nameof(FindByIdAsync));
        _lists.TryGetValue(id.Value, out var found);
        return Task.FromResult(found);
    }

    public Task<Result> AddAsync(GiftList list, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(AddAsync));
        SharedLog?.Record(nameof(AddAsync));
        _lists[list.Id.Value] = list;
        return Task.FromResult(Result.Success());
    }

    public Task UpdateAsync(GiftList list, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(UpdateAsync));
        SharedLog?.Record(nameof(UpdateAsync));
        _lists[list.Id.Value] = list;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(GiftListId id, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(DeleteAsync));
        SharedLog?.Record(nameof(DeleteAsync));
        _lists.Remove(id.Value);
        return Task.CompletedTask;
    }
}
