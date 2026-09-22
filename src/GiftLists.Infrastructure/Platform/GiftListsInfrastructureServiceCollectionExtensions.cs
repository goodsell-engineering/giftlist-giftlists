using GiftLists.Application.Common;
using GiftLists.Application.GiftLists;
using GiftLists.Application.GiftLists.AddGiftItem;
using GiftLists.Application.GiftLists.ChangeGiftListExpiry;
using GiftLists.Application.GiftLists.CreateGiftList;
using GiftLists.Application.GiftLists.DeleteGiftList;
using GiftLists.Application.GiftLists.RenameGiftList;
using GiftLists.Application.GiftLists.RemoveGiftItem;
using GiftLists.Infrastructure.GiftLists.Messaging;
using GiftLists.Infrastructure.GiftLists.Persistence;
using GiftLists.Contracts.GiftLists.Events;
using GiftLists.Infrastructure.Platform.Security;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Rebus.Bus;
using Rebus.Config;

namespace GiftLists.Infrastructure.Platform;

/// <summary>
/// GiftLists' composition root, called once from <c>GiftLists.Host</c>'s <c>Program.cs</c>. Host
/// itself contains no wiring beyond the call to this method (CONVENTIONS.md "Project reference graph"); everything below
/// is grouped by domain (<c>GiftLists/...</c>) rather than by technical category, same as
/// production code (CONVENTIONS.md "Folder structure") — <c>Platform/</c> holds only this aggregator, which
/// belongs to no single domain.
/// </summary>
public static class GiftListsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddGiftListsInfrastructure(this IServiceCollection services)
    {
        AddGiftLists(services);
        return services;
    }

    /// <summary>
    /// Applies startup-time infrastructure that needs a live connection — today, just the unique
    /// <c>shareToken</c> index (ARCHITECTURE.md "Data model"). Called once from <c>Program.cs</c> after the
    /// host is built, mirroring how Mongo/Rebus health checks are wired ahead of any use case.
    /// </summary>
    public static async Task EnsureIndexesAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var database = serviceProvider.GetRequiredService<IMongoDatabase>();
        await GiftListRepository.EnsureIndexesAsync(database, cancellationToken);
        await EnsureExpirySagaIndexesAsync(database, cancellationToken);
    }

    /// <summary>
    /// The unique index on the expiry saga's correlation property — the one Rebus.MongoDb would
    /// otherwise create lazily on first insert (see <c>GiftListsRebusConfiguration</c>, which
    /// turns that off and says why). Same shape Rebus uses: unique, ascending, on the correlation
    /// property's own element name, which is what its own lookup filters on. Uniqueness is the
    /// saga's one-instance-per-list guarantee, not an optimisation: two rows for one list would
    /// mean two timeouts and two <c>GiftListExpiredV1</c>s.
    /// </summary>
    private static Task EnsureExpirySagaIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var sagas = database.GetCollection<BsonDocument>(GiftListsRebusConfiguration.SagasCollectionName);
        var correlationIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending(GiftListsRebusConfiguration.SagaCorrelationElementName),
            new CreateIndexOptions { Unique = true, Name = "listId_unique" });

        return sagas.Indexes.CreateOneAsync(correlationIndex, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Subscribes this service to the three of its OWN events the expiry saga runs on
    /// (ARCHITECTURE.md "Sagas: list expiry"). Called once from <c>Program.cs</c> after the host
    /// is built, the same placement as <see cref="EnsureIndexesAsync"/> and as
    /// <c>Reservations.Infrastructure.Platform.ReservationsInfrastructureServiceCollectionExtensions.SubscribeToGiftListsEventsAsync</c>.
    /// Three, not seven: a subscribed event with no handler on this queue is a dispatch failure
    /// that lands in the error queue, so only what <c>GiftListExpirySaga</c> handles is bound.
    /// </summary>
    public static async Task SubscribeToOwnEventsAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var bus = serviceProvider.GetRequiredService<IBus>();
        await bus.Subscribe<GiftListCreatedV1>();
        await bus.Subscribe<GiftListExpiryChangedV1>();
        await bus.Subscribe<GiftListDeletedV1>();
    }

    private static void AddGiftLists(IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IShareTokenGenerator, ShareTokenGenerator>();
        services.AddScoped<IGiftListRepository, GiftListRepository>();
        services.AddScoped<IDomainEventPublisher, GiftListEventPublisher>();

        services.AddScoped<IValidator<CreateGiftListRequest>, CreateGiftListValidator>();
        services.AddScoped<IInteractor<CreateGiftListRequest, CreateGiftListResponse>, CreateGiftListInteractor>();

        services.AddScoped<IValidator<RenameGiftListRequest>, RenameGiftListValidator>();
        services.AddScoped<IInteractor<RenameGiftListRequest, RenameGiftListResponse>, RenameGiftListInteractor>();

        services.AddScoped<IValidator<ChangeGiftListExpiryRequest>, ChangeGiftListExpiryValidator>();
        services.AddScoped<IInteractor<ChangeGiftListExpiryRequest, ChangeGiftListExpiryResponse>, ChangeGiftListExpiryInteractor>();

        services.AddScoped<IValidator<DeleteGiftListRequest>, DeleteGiftListValidator>();
        services.AddScoped<IInteractor<DeleteGiftListRequest, DeleteGiftListResponse>, DeleteGiftListInteractor>();

        services.AddScoped<IValidator<AddGiftItemRequest>, AddGiftItemValidator>();
        services.AddScoped<IInteractor<AddGiftItemRequest, AddGiftItemResponse>, AddGiftItemInteractor>();

        services.AddScoped<IValidator<RemoveGiftItemRequest>, RemoveGiftItemValidator>();
        services.AddScoped<IInteractor<RemoveGiftItemRequest, RemoveGiftItemResponse>, RemoveGiftItemInteractor>();

        // One open-generic decorator pair, applied to every IInteractor<,> registered above,
        // rather than a hand-written decorator per use case — see
        // GiftLists.Application.Common.IInteractor's doc comment for why the specifically-named
        // ports (ICreateGiftList, ...) themselves cannot be the decoration target. Validation,
        // then Logging, in that order in every service (CONVENTIONS.md "Use cases") — Logging is
        // therefore the outermost decorator and also observes a validation failure, not just a
        // business one.
        services.Decorate(typeof(IInteractor<,>), typeof(Validating<,>));
        services.Decorate(typeof(IInteractor<,>), typeof(Logging<,>));

        services.AddRebusHandler<CreateGiftListHandler>();
        services.AddRebusHandler<RenameGiftListHandler>();
        services.AddRebusHandler<ChangeGiftListExpiryHandler>();
        services.AddRebusHandler<DeleteGiftListHandler>();
        services.AddRebusHandler<AddGiftItemHandler>();
        services.AddRebusHandler<RemoveGiftItemHandler>();

        // GL-41: the expiry saga is a Rebus handler too, resolved per message like the ones
        // above; its Mongo-backed saga/timeout storage is GiftListsRebusConfiguration's job and
        // its subscriptions are SubscribeToOwnEventsAsync's.
        services.AddRebusHandler<GiftListExpirySaga>();
    }
}
