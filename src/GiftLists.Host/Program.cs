using BuildingBlocks.HealthChecks;
using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using GiftLists.Infrastructure.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// GiftLists' own database/queue (ARCHITECTURE.md "Data model", "Tech stack") — wiring these here, ahead of any
// use case, is what makes "healthy Rebus connection + Mongo connection" (Phase 0 exit
// criterion) an observable fact rather than something scraped out of logs.
builder.Services.AddBuildingBlocksMongo(builder.Configuration, "giftlist");
builder.Services.AddBuildingBlocksRebus(builder.Configuration, "giftlist");
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
