namespace GiftLists.Application.GiftLists.DeleteGiftList;

/// <summary>No fields to carry — success is the whole answer. An empty record rather than <c>Result</c> (non-generic) so this use case's <c>Handle</c> still matches the one shape every interactor implements (CONVENTIONS.md "Use cases").</summary>
public sealed record DeleteGiftListResponse;
