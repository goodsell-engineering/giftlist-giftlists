using BuildingBlocks.Messaging;
using BuildingBlocks.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Config;

namespace GiftLists.IntegrationTests.Support;

/// <summary>
/// A subscriber wired up exactly the way the Gateway/Reservation would be in production: it asks
/// Rebus to subscribe to <typeparamref name="TEvent"/> by .NET type, and only receives anything
/// at all if the publisher's wire type name still matches what the subscription was registered
/// under — the only way to prove Rebus's own type-name-based routing/deserialization
/// (CONVENTIONS.md "Testing") actually works. An in-memory transport would hand the object straight to
/// a handler and never touch this. Extracted from the single-use-site version in
/// <c>Identity.IntegrationTests.Users.UserEventPublishingTests</c> (GL-16) because this suite
/// needs the same shape for every one of GiftLists' five integration events, not just one.
/// </summary>
internal sealed class EventSubscriber<TEvent> : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly string _queueName;
    private readonly string _rabbitMqConnectionString;

    private EventSubscriber(IHost host, string queueName, EventCapture<TEvent> capture, string rabbitMqConnectionString)
    {
        _host = host;
        _queueName = queueName;
        Capture = capture;
        _rabbitMqConnectionString = rabbitMqConnectionString;
    }

    public EventCapture<TEvent> Capture { get; }

    public static async Task<EventSubscriber<TEvent>> StartAsync(string rabbitMqConnectionString)
    {
        var capture = new EventCapture<TEvent>();
        var queueName = $"giftlist-tests-sub.{Guid.NewGuid():N}";
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [RebusConfigurationExtensions.ConnectionStringConfigKey] = rabbitMqConnectionString,
        });
        builder.Services.AddSingleton(capture);
        builder.Services.AddBuildingBlocksRebus(builder.Configuration, queueName);
        builder.Services.AddRebusHandler<EventCapturingHandler<TEvent>>();
        var host = builder.Build();
        await host.StartAsync();
        await host.Services.GetRequiredService<IBus>().Subscribe<TEvent>();
        return new EventSubscriber<TEvent>(host, queueName, capture, rabbitMqConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.Services.GetRequiredService<IBus>().Unsubscribe<TEvent>();
        await _host.StopAsync();
        _host.Dispose();
        await QueueCleanup.DeleteAsync(_rabbitMqConnectionString, _queueName);
    }
}
