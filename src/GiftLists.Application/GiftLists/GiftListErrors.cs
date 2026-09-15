using BuildingBlocks.Results;

namespace GiftLists.Application.GiftLists;

/// <summary>
/// Stable, machine-readable error codes for the GiftLists use cases (CONVENTIONS.md "Errors"), all
/// <c>giftlist.&lt;code&gt;</c> — two segments, applied uniformly. Where two use cases reject the
/// same thing (e.g. a missing/malformed id), they share one code and one <see cref="Error"/>
/// rather than each inventing their own.
/// </summary>
public static class GiftListErrors
{
    public static readonly Error NotFound = new(
        "giftlist.not_found", "No gift list exists with this id.", ErrorKind.NotFound);

    /// <summary>
    /// "You may not do this" (403), not "log in again" (401) — the caller is authenticated, just
    /// not the owner (ARCHITECTURE.md "Auth & sharing" — "owner queries require JWT + ownership check").
    /// </summary>
    public static readonly Error Forbidden = new(
        "giftlist.forbidden", "You do not own this gift list.", ErrorKind.Forbidden);

    /// <summary>
    /// The exact code name CONVENTIONS.md "Errors" uses as its own worked example of the
    /// <c>&lt;service&gt;.&lt;code&gt;</c> scheme. <see cref="ErrorKind.Conflict"/>: the request
    /// conflicts with the list's current (expired) state, matching
    /// <c>GiftLists.Domain.GiftLists.GiftList.AddItem</c>'s own invariant
    /// (CONVENTIONS.md "Domain modelling").
    /// </summary>
    public static readonly Error Expired = new(
        "giftlist.expired", "This gift list has expired.", ErrorKind.Conflict);

    public static readonly Error ItemNotFound = new(
        "giftlist.item_not_found", "No item exists with this id on this gift list.", ErrorKind.NotFound);

    /// <summary>Shared across every request field that is a required id and arrived as <see cref="Guid.Empty"/> — the same semantic regardless of which field or which use case caught it.</summary>
    public static readonly Error InvalidId = new(
        "giftlist.invalid_id", "A required identifier was missing or invalid.", ErrorKind.Validation);

    public static readonly Error NameInvalid = new(
        "giftlist.name_invalid", "Enter a name.", ErrorKind.Validation);

    public static readonly Error ExpiryInvalid = new(
        "giftlist.expiry_invalid", "Expiry must be in the future.", ErrorKind.Validation);

    public static readonly Error ItemNameInvalid = new(
        "giftlist.item_name_invalid", "Enter an item name.", ErrorKind.Validation);

    /// <summary>GL-74: "absolute http/https only" — see <c>GiftLists.Domain.GiftLists.GiftItemUrl</c>'s own doc comment for the full list of vectors this rejects.</summary>
    public static readonly Error ItemUrlInvalid = new(
        "giftlist.item_url_invalid", "Enter a valid http or https URL.", ErrorKind.Validation);

    /// <summary>GL-74's length-bound decision — see <c>GiftLists.Domain.GiftLists.GiftItemDescription</c>'s own doc comment.</summary>
    public static readonly Error ItemDescriptionInvalid = new(
        "giftlist.item_description_invalid",
        "Description is too long.",
        ErrorKind.Validation);

    /// <summary>The safety net behind <see cref="IGiftListRepository.AddAsync"/>'s unique-index write — see that member's own doc comment.</summary>
    public static readonly Error Duplicate = new(
        "giftlist.duplicate",
        "A gift list with this id or share token already exists.",
        ErrorKind.Conflict);
}
