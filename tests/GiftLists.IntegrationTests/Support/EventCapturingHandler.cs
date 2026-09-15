using Rebus.Handlers;

namespace GiftLists.IntegrationTests.Support;

/// <summary>Captures a real, deserialized <typeparamref name="TEvent"/> off a real subscription — see <see cref="EventCapture{TEvent}"/>.</summary>
internal sealed class EventCapturingHandler<TEvent>(EventCapture<TEvent> capture) : IHandleMessages<TEvent>
{
    public Task Handle(TEvent message)
    {
        capture.Record(message);
        return Task.CompletedTask;
    }
}
