using BuildingBlocks.Testing;
using Testcontainers.MongoDb;
using Testcontainers.RabbitMq;

namespace GiftLists.IntegrationTests.Fixtures;

/// <summary>
/// The two real, containerised dependencies (CONVENTIONS.md "Testing"), started once for the whole
/// assembly — never per test, never restarted between tests. Per-test isolation is the
/// responsibility of <see cref="GiftListsFixture"/> (drop the database, give every test its own
/// Rebus queues), not this fixture. Mirrors <c>Identity.IntegrationTests.Fixtures.InfrastructureFixture</c> (GL-16)
/// in every configuration property except <see cref="SuiteLabel"/>, which must differ.
/// </summary>
public sealed class InfrastructureFixture : IAsyncLifetime
{
    private static readonly bool Reuse = Environment.GetEnvironmentVariable("CI") != "true";

    /// <summary>
    /// GL-92: every container this suite starts carries this label, and no other suite's does.
    /// Testcontainers derives its reuse hash from the container's configuration, labels included,
    /// so without it this fixture is byte-for-byte the same configuration as
    /// Identity.IntegrationTests' — and <c>.WithReuse(true)</c> then hands both suites the *same*
    /// Mongo and the *same* broker. Observed, not theorised: one unlabelled mongo:7 and one
    /// unlabelled rabbitmq:3.13-management were serving GiftLists and Identity together, so each
    /// suite's per-test "drop the database" was dropping the other's data, and both suites' Rebus
    /// hosts were binding onto one broker where a shared queue name round-robins messages between
    /// them. The GL-18 review had already fixed exactly this between Gateway and Identity; the
    /// fix was never applied to this pair, which is why GL-92 exists.
    ///
    /// The value is the assembly name's prefix, lower-cased — deliberately the plural project
    /// name, not the singular database/queue name of CONVENTIONS.md "Persistence", because
    /// <c>SuiteLabelRuleTests</c> derives it mechanically from the .csproj and fails if they
    /// disagree. That derivation, not a distinctness check, is what makes the rule enforceable at
    /// all: there is no root solution file, only one per service directory, so a rule comparing
    /// this repo's suites to each other already compares a set of one today — GL-25 only makes
    /// that harder to notice.
    /// </summary>
    private const string SuiteLabel = "giftlists";

    // GL-93: ReliableReadiness replaces the builders' default wait strategies, which read
    // container history rather than probing the live server and so are not safe across the
    // restarts a `.WithReuse(true)` container accumulates locally — see its doc comment.
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7")
        .WithReuse(Reuse)
        .WithLabel("giftlist.suite", SuiteLabel)
        .WithReliableWaitStrategy()
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3.13-management")
        .WithReuse(Reuse)
        .WithLabel("giftlist.suite", SuiteLabel)
        .WithReliableWaitStrategy()
        .Build();

    public string MongoConnectionString => _mongo.GetConnectionString();

    public string RabbitMqConnectionString => _rabbitMq.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mongo.StartReliablyAsync(SuiteLabel), _rabbitMq.StartReliablyAsync(SuiteLabel));
    }

    public async Task DisposeAsync()
    {
        await _mongo.DisposeAsync().AsTask();
        await _rabbitMq.DisposeAsync().AsTask();
    }
}
