namespace GiftLists.Contracts.GiftLists.Events;

/// <summary>
/// Published by the expiry saga when a list's expiry elapses (ARCHITECTURE.md "Sagas: list
/// expiry"; "Event catalogue": GiftLists → Gateway, Reservation). Unlike every other event in
/// this package it has no domain-event counterpart: nothing about the list changes at expiry —
/// expiry is a query-time predicate on <c>ExpiresAt</c>, never a stored flag and never a TTL
/// delete (ARCHITECTURE.md "Auth & sharing") — so no aggregate is loaded and none is mutated.
/// This is a notification that the instant passed, for consumers that want to push it to an open
/// page rather than wait for the next query. Nobody may gate a write on having received it:
/// Reservation checks <c>ExpiresAt</c> on its own projection precisely so a late delivery leaves
/// no window (ARCHITECTURE.md "Auth & sharing").
/// </summary>
public sealed record GiftListExpiredV1(Guid ListId, DateTimeOffset ExpiresAt);
