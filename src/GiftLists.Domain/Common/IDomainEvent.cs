namespace GiftLists.Domain.Common;

/// <summary>
/// Marker for a fact an aggregate raised (ARCHITECTURE.md "Domain events are not integration events" — never leaves the process; an
/// Infrastructure mapper translates it into an integration event). Replaces a bare
/// <c>object</c> on <c>GiftList.DomainEvents</c> so a mapper's exhaustive switch has a closed,
/// typed set to pattern-match against instead of "anything".
/// </summary>
/// <remarks>
/// Declared per-service, in this BCL-only Domain project, rather than in the shared
/// BuildingBlocks package — see <c>Identity.Domain.Common.IDomainEvent</c>'s own doc comment
/// (GL-16) for why: <c>Domain_ShouldNotReferenceBuildingBlocks</c> [AT] (CONVENTIONS.md "Project reference graph")
/// forbids Domain from referencing BuildingBlocks at all, for any reason, so each service's
/// Domain declares its own copy of this marker instead of sharing one.
/// </remarks>
public interface IDomainEvent;
