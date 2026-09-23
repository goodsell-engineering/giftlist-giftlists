using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

namespace GiftLists.IntegrationTests.Support;

/// <summary>Captures a real, deserialized <typeparamref name="TEvent"/> off a real subscription — see <see cref="EventCapture{TEvent}"/>.</summary>
internal sealed class EventCapturingHandler<TEvent>(EventCapture<TEvent> capture) : IHandleMessages<TEvent>
{
    public Task Handle(TEvent message)
    {
        // GL-45: the real header this event was published with, not merely a value some other
        // part of the pipeline claims to have set.
        MessageContext.Current.Headers.TryGetValue(Headers.CorrelationId, out var correlationId);
        capture.Record(message, correlationId);
        return Task.CompletedTask;
    }
}
