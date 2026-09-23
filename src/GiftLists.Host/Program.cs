using BuildingBlocks.HealthChecks;
using BuildingBlocks.Logging;
using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using GiftLists.Infrastructure.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// GL-45: scope rendering for the console provider WebApplication.CreateBuilder already
// registers — without this, CorrelationIdIncomingStep's own logger scope pushes correctly but
// silently, since Microsoft.Extensions.Logging's simple console formatter defaults
// IncludeScopes to false.
builder.Services.AddBuildingBlocksLogging();

// GiftLists' own database/queue (ARCHITECTURE.md "Data model", "Tech stack") — wiring these here, ahead of any
// use case, is what makes "healthy Rebus connection + Mongo connection" (Phase 0 exit
// criterion) an observable fact rather than something scraped out of logs.
builder.Services.AddBuildingBlocksMongo(builder.Configuration, "giftlist");
// GL-41: the expiry saga's Mongo-backed saga/timeout storage is layered on here, from
// Infrastructure, so this file stays wiring only.
builder.Services.AddBuildingBlocksRebus(builder.Configuration, "giftlist", GiftListsRebusConfiguration.Configure);
builder.Services.AddBuildingBlocksHealthChecks(builder.Configuration);

// GL-20: GiftList aggregate, create/rename/delete list and add/remove item use cases, and their
// Rebus handlers. Everything below this line is GiftLists' own composition root, in
// GiftLists.Infrastructure — Host itself contains no business wiring beyond this one call
// (CONVENTIONS.md "Project reference graph").
builder.Services.AddGiftListsInfrastructure();

var app = builder.Build();

// The unique shareToken index (ARCHITECTURE.md "Data model") is a correctness requirement, not an
// optimisation — applied once at startup rather than left to be inferred from application code.
await GiftListsInfrastructureServiceCollectionExtensions.EnsureIndexesAsync(app.Services, CancellationToken.None);

// GL-41: the expiry saga runs on this service's own GiftListCreatedV1/ExpiryChangedV1/DeletedV1,
// read back off the giftlist queue like any other subscriber (ARCHITECTURE.md "Sagas: list expiry").
await GiftListsInfrastructureServiceCollectionExtensions.SubscribeToOwnEventsAsync(app.Services, CancellationToken.None);

// Liveness: only "is the process up and answering HTTP". Deliberately checks nothing
// external — a RabbitMQ/Mongo blip must not make Docker kill an otherwise-healthy container
// (ARCHITECTURE.md "Why workers still need a little HTTP").
app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: the Mongo + RabbitMQ checks BuildingBlocks registered above, tagged "ready".
// This is the endpoint compose's healthcheck targets, because it's the one that should gate
// `depends_on: condition: service_healthy` for anything waiting on GiftLists.
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.Run();
