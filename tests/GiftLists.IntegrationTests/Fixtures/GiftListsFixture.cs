using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using GiftLists.Contracts.GiftLists;
using GiftLists.Infrastructure.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using GiftLists.IntegrationTests.Support;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Routing.TypeBased;

namespace GiftLists.IntegrationTests.Fixtures;

/// <summary>
/// Builds the real GiftLists composition root — the exact same
/// <c>AddBuildingBlocksMongo</c>/<c>AddBuildingBlocksRebus</c>/<c>AddGiftListsInfrastructure</c>
/// calls <c>GiftLists.Host</c>'s <c>Program.cs</c> makes — against the containers from
/// <see cref="InfrastructureFixture"/>, plus one "requester" bus standing in for the Gateway, the
/// only other thing that ever sends a GiftLists command in production. Tests therefore enter
/// through the real Rebus handlers (CONVENTIONS.md "Testing"'s "entered at its real entry point"), never
/// by calling an interactor directly — except <see cref="CreateGiftListsScope"/>, kept for the
/// handful of cases the wire-level tests genuinely cannot reach deterministically (mirrors
/// <c>Identity.IntegrationTests.Fixtures.IdentityFixture.CreateIdentityScope</c>'s own rationale,
/// GL-57 review): GiftLists' commands are fire-and-forget (ARCHITECTURE.md "Command → event flow"), so there is no
/// reply to observe a rejected command through, and forcing a real shareToken collision means
/// controlling the exact random value <see cref="Infrastructure.Platform.Security.ShareTokenGenerator"/>
/// would otherwise generate.
/// </summary>
public sealed class GiftListsFixture : IAsyncLifetime
{
    private const string DatabaseName = "giftlist";
    private const string GiftListsQueueName = "giftlist";

    /// <summary>Mirrors the internal <c>GiftListRepository.CollectionName</c> — not accessible from here, kept in sync by hand.</summary>
    public const string GiftListsCollectionName = "giftLists";

    private readonly InfrastructureFixture _infrastructure = new();
    private IHost _giftListsHost = null!;
    private IHost _requesterHost = null!;

    public IMongoDatabase Database { get; private set; } = null!;

    public IBus RequesterBus => _requesterHost.Services.GetRequiredService<IBus>();

    public string RabbitMqConnectionString => _infrastructure.RabbitMqConnectionString;

    /// <summary>A scope into GiftLists' own container — see this type's own doc comment for why.</summary>
    /// <summary>Log entries written by the GiftLists host — see <see cref="LogCapture"/>.</summary>
    public LogCapture Logs { get; } = new();

    public IServiceScope CreateGiftListsScope() => _giftListsHost.Services.CreateScope();

    public async Task InitializeAsync()
    {
        await _infrastructure.InitializeAsync();

        var giftListsConfig = new Dictionary<string, string?>
        {
            [RebusConfigurationExtensions.ConnectionStringConfigKey] = _infrastructure.RabbitMqConnectionString,
            [MongoConfigurationExtensions.ConnectionStringConfigKey] = _infrastructure.MongoConnectionString,
        };
        var giftListsBuilder = Host.CreateApplicationBuilder();
        giftListsBuilder.Logging.ClearProviders();
        // Kept after ClearProviders so the only provider is this one — test output stays quiet
        // while assertions about what was logged remain possible.
        giftListsBuilder.Logging.AddProvider(Logs);
        giftListsBuilder.Configuration.AddInMemoryCollection(giftListsConfig);
        giftListsBuilder.Services.AddBuildingBlocksMongo(giftListsBuilder.Configuration, DatabaseName);
        giftListsBuilder.Services.AddBuildingBlocksRebus(giftListsBuilder.Configuration, GiftListsQueueName);
        giftListsBuilder.Services.AddGiftListsInfrastructure();
        _giftListsHost = giftListsBuilder.Build();
        await _giftListsHost.StartAsync();
        await GiftListsInfrastructureServiceCollectionExtensions.EnsureIndexesAsync(
            _giftListsHost.Services, CancellationToken.None);

        Database = _giftListsHost.Services.GetRequiredService<IMongoDatabase>();

        var requesterBuilder = Host.CreateApplicationBuilder();
        requesterBuilder.Logging.ClearProviders();
        requesterBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [RebusConfigurationExtensions.ConnectionStringConfigKey] = _infrastructure.RabbitMqConnectionString,
        });
        requesterBuilder.Services.AddBuildingBlocksRebus(
            requesterBuilder.Configuration,
            $"giftlist-tests.{Guid.NewGuid():N}",
            configure: (configurer, _) => configurer.Routing(r => r.TypeBased()
                .Map<CreateGiftList>(GiftListsQueueName)
                .Map<RenameGiftList>(GiftListsQueueName)
                .Map<DeleteGiftList>(GiftListsQueueName)
                .Map<AddGiftItem>(GiftListsQueueName)
                .Map<RemoveGiftItem>(GiftListsQueueName)));
        _requesterHost = requesterBuilder.Build();
        await _requesterHost.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _requesterHost.StopAsync();
        _requesterHost.Dispose();
        await _giftListsHost.StopAsync();
        _giftListsHost.Dispose();
        await _infrastructure.DisposeAsync();
    }

    /// <summary>
    /// CONVENTIONS.md "Testing": isolate by dropping the database between tests, never by restarting a
    /// container. Re-applies the unique shareToken index afterwards — dropping the database drops
    /// it too, and a test relying on it running right after a reset would otherwise pass for the
    /// wrong reason.
    /// </summary>
    public async Task ResetAsync()
    {
        await Database.Client.DropDatabaseAsync(DatabaseName);
        await GiftListsInfrastructureServiceCollectionExtensions.EnsureIndexesAsync(
            _giftListsHost.Services, CancellationToken.None);
    }
}
