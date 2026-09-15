namespace GiftLists.Domain.GiftLists;

/// <summary>
/// Strongly-typed id (CONVENTIONS.md "Domain modelling") for the account that owns a <see cref="GiftList"/>.
/// Wraps the same <see cref="Guid"/> Identity knows as <c>Identity.Domain.Users.UserId</c>, but
/// is its own type rather than a reference to Identity's — GiftLists' <c>Domain</c> references
/// nothing outside the BCL (CONVENTIONS.md "Project reference graph"), and services only ever know each other by
/// opaque id, never by importing one another's aggregate types. Compared for equality against
/// the requesting caller's id to enforce "owner queries require ownership" (ARCHITECTURE.md "Auth & sharing").
/// </summary>
public readonly record struct OwnerId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
