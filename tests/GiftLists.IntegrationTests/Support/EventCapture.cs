using System.Collections.Concurrent;

namespace GiftLists.IntegrationTests.Support;

/// <summary>Registered as a singleton in a subscriber's own DI container so a test can await what its handler received. Mirrors <c>Identity.IntegrationTests.Support.EventCapture{TEvent}</c> (GL-16).</summary>
internal sealed class EventCapture<TEvent>
{
    private readonly ConcurrentQueue<TEvent> _all = new();

    public TaskCompletionSource<TEvent> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Every event received, not just the first. <see cref="Completion"/> answers "did this event
    /// arrive", which is all five of the per-event publishing tests need; it cannot answer "did it
    /// arrive TWICE", which is what a duplicate-publish regression looks like from the outside
    /// (GL-68 review). Both are served from the one handler rather than by standing up a second
    /// way to watch the bus.
    /// </summary>
    public IReadOnlyCollection<TEvent> All => _all.ToArray();

    public void Record(TEvent received)
    {
        _all.Enqueue(received);
        Completion.TrySetResult(received);
    }
}
