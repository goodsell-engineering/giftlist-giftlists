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

    public static RebusConfigurer Configure(RebusConfigurer configurer, IServiceProvider serviceProvider)
    {
        var database = serviceProvider.GetRequiredService<IMongoDatabase>();

        return configurer
            .Sagas(s => s.StoreInMongoDb(database, _ => SagasCollectionName))
            .Timeouts(t => t.StoreInMongoDb(database, TimeoutsCollectionName));
    }
}
