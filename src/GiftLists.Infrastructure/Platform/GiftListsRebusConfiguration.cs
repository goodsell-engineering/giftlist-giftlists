using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Rebus.Config;

namespace GiftLists.Infrastructure.Platform;

/// <summary>
/// What GiftLists layers onto the shared bus setup (<c>AddBuildingBlocksRebus</c>'s
/// <c>configure</c> hook): Mongo-backed saga and timeout storage, which the expiry saga needs and
/// no other service does yet (ARCHITECTURE.md "Messaging": "Persistence for Rebus internals
/// (sagas, timeouts) uses Mongo, since it's already there"). Lives in <c>Platform/</c> because it
/// is framework wiring belonging to no domain (CONVENTIONS.md "Folder structure"), and is passed
/// by the Host rather than written there so the integration fixture builds the bus the same way
/// the Host does (CONVENTIONS.md "Project reference graph": Host is wiring only).
/// </summary>
public static class GiftListsRebusConfiguration
{
    /// <summary>
    /// Both collections sit in this service's own <c>giftlist</c> database beside
    /// <c>giftLists</c> (ARCHITECTURE.md "Data model"). Rebus.MongoDb names a saga collection
    /// after the saga data's type by default; a resolver is passed so the name is a decision
    /// made here, once, rather than a C# type name leaking into Mongo.
    /// </summary>
    public const string SagasCollectionName = "giftListExpirySagas";

    public const string TimeoutsCollectionName = "timeouts";

    /// <summary>
    /// The element name of <c>GiftListExpirySagaData.ListId</c>, the saga's correlation property.
    /// Named here because two places need to agree on it and neither owns the other: Rebus filters
    /// saga lookups on it, and <c>GiftListsInfrastructureServiceCollectionExtensions</c> builds the
    /// unique index over it. A string rather than <c>nameof</c> on the type because the saga data
    /// is internal to another folder and the BSON element name is what actually has to match —
    /// <c>GiftListExpirySagaData</c>'s <c>[BsonElement]</c> attributes pin the same literal, and
    /// its remarks explain why they are not optional.
    /// </summary>
    public const string SagaCorrelationElementName = "ListId";

    public static RebusConfigurer Configure(RebusConfigurer configurer, IServiceProvider serviceProvider)
    {
        var database = serviceProvider.GetRequiredService<IMongoDatabase>();

        return configurer
            // automaticallyCreateIndexes: false — the unique correlation index is created at
            // startup by GiftListsInfrastructureServiceCollectionExtensions.EnsureIndexesAsync
            // instead, beside every other index this service declares (CONVENTIONS.md
            // "Persistence": declared beside the repository, applied at startup). Not a style
            // preference: Rebus.MongoDb creates that index inside a Lazy<Task> on the FIRST saga
            // insert and then awaits that same cached task on every later insert and update — so
            // a Mongo blip at exactly that moment faults the task, and the fault is cached for
            // the life of the process. Expiry would then be dead until a restart (finds and
            // deletes keep working, which is what makes it hard to spot) while the rest of the
            // service recovers on its own. Creating it at startup fails loudly instead, before
            // the service reports healthy, and survives the integration fixture's database drop.
            .Sagas(s => s.StoreInMongoDb(database, _ => SagasCollectionName, automaticallyCreateIndexes: false))
            .Timeouts(t => t.StoreInMongoDb(database, TimeoutsCollectionName));
    }
}
