# giftlist-giftlists

GiftList service: lists, items, expiry, share tokens. Owns the `giftlist` database and the
`giftlist` Rebus queue (both singular — CONVENTIONS.md "Persistence").

```
src/GiftLists.Domain          the GiftList aggregate and its items; references nothing
src/GiftLists.Application     use cases and ports; Domain + BuildingBlocks only
src/GiftLists.Infrastructure  Mongo, Rebus handlers, the domain-to-integration event mapper
src/GiftLists.Host            composition root; wiring only
src/GiftLists.Contracts       THE PUBLISHED PACKAGE (see below)
```

This repo also holds the **canonical `Architecture/` test suite**, under
`tests/GiftLists.UnitTests/Architecture`. Edit it here and nowhere else; propagate with
`giftlist-devenv/scripts/sync-arch-tests.sh`.

## `GiftLists.Contracts`

GiftLists' entire public wire surface, and nothing else: **the commands it accepts and the events
it publishes** (ARCHITECTURE.md "Contracts: each service owns and publishes its own").

| Kind | Types |
|---|---|
| Commands it accepts | `CreateGiftList`, `RenameGiftList`, `DeleteGiftList`, `AddGiftItem`, `RemoveGiftItem` |
| Events it publishes | `GiftListCreatedV1`, `GiftListRenamedV1`, `GiftListDeletedV1`, `GiftItemAddedV1`, `GiftItemRemovedV1` |

What is deliberately **not** in here: the `GiftList` aggregate, its domain events (`GiftListCreated`
without the `V1`), `ShareToken`, `GiftListDocument`, `IGiftListRepository`. Domain events are not
integration events — the aggregate raises one and an Infrastructure mapper emits the other, and
publishing a domain type is banned outright (CONVENTIONS.md "Messaging"). A persistence document
in the package would make the Mongo driver a dependency of every consumer.

Consumed today by `giftlist-gateway`, which projects every one of these events into its read
model, and by `giftlist-reservations` from Phase 4, which genuinely consumes them to know what is
reservable.

The package references nothing — never Domain, never BuildingBlocks (CONVENTIONS.md "Project
reference graph"). Type names and namespaces are part of the wire format, because Rebus routes
and deserializes on the .NET type name; renaming one is a **major** bump even if the shape is
unchanged (ARCHITECTURE.md "What 'breaking' means for a message contract").

## Directory.Build.props is a copy, and it is checked

`net10.0`, `LangVersion latest`, nullable on, warnings as errors, implicit usings — set once in
`Directory.Build.props` at this repo's root, inherited by every project. No `.csproj` sets
`TargetFramework` itself (CONVENTIONS.md "Target framework").

Before the split there was one such file, at the monorepo root, and MSBuild's directory walk gave
every service the same values. MSBuild does not walk out of a repo, so each .NET repo now has its
own copy — and copies drift. **Do not hand-edit this one.** Edit the canonical copy in
`giftlist-buildingblocks`, then run `giftlist-devenv/scripts/sync-repo-roots.sh`, which rewrites
every copy and regenerates the `repo-root-files.sha256` manifest beside each.

Two tests fail if you edit it in place, and they check different things:

- `RepoRootFileSyncTests` — this copy is byte-identical to the canonical one.
- `TargetFrameworkTests.DirectoryBuildProps_ShouldMatchConventionsVerbatim` — the content is the
  block CONVENTIONS.md documents. Every repo can agree on a wrong file; this is what catches it.

The `Architecture/` suite under `tests/*.UnitTests/` is governed the same way: canonical copy in
`giftlist-giftlists`, propagated by `giftlist-devenv/scripts/sync-arch-tests.sh`, pinned by
`architecture-tests.sha256` and `ArchitectureTestSyncTests`.

## Where this repo sits

Seven repos under `goodsell-engineering`, cloned as siblings (ARCHITECTURE.md "Repository
layout"):

```
giftlist/
  local-feed/              <- .nupkg and .tgz files land here; not a git repo
  giftlist-devenv/         <- docker compose, make up, the sync scripts
  giftlist-buildingblocks/
  giftlist-gateway/
  giftlist-identity/
  giftlist-giftlists/
  giftlist-reservations/
  giftlist-web/
```

Design documents (`ARCHITECTURE.md`, `CONVENTIONS.md`) live in the workspace repository, not in
any of the seven: they govern all of them, a home inside one is invisible to the other six, and
seven copies is exactly the drift they warn about. Comments here cite them by document and
heading text, never by section number (CONVENTIONS.md "Citing the rules").

## What does not work yet, and whose job it is

Restore needs the local folder feed, and the pieces that wire it up are the next three issues:

| Missing | Issue |
|---|---|
| `../local-feed` and a `nuget.config` per repo pointing at it alongside nuget.org | GL-26 |
| Semantic versioning discipline and consumer pinning | GL-27 |
| `make pack-all` (dependency-ordered, refuses to overwrite a version already in the feed) and `clone-all.sh` | GL-28 |
| Compose mounting the feed into the containers | GL-29 |
| Per-repo CI (build and test only; there is nowhere to publish to) | GL-30 |

Until GL-26 lands, restore by naming the feed on the command line:

```
dotnet restore <Solution>.sln -s https://api.nuget.org/v3/index.json -s ../local-feed
dotnet build <Solution>.sln --no-restore
dotnet test tests/*.UnitTests/*.UnitTests.csproj
```

**A warning for whoever writes GL-26's `nuget.config`.** nuget.org already serves unrelated
packages called `BuildingBlocks` (at 1.0.0) and `Identity.Contracts` (at 1.0.14). A consumer
configured with both nuget.org and the folder feed can satisfy an exact version from either
source, so listing the feed as a second `<add key>` is not enough — use `packageSourceMapping`
to bind these ids to the local feed. The 0.1.0 starting version was picked because it exists on
neither id upstream, which narrows the window but does not close it.
