using GiftLists.Domain.Common;

namespace GiftLists.Domain.GiftLists.Events;

/// <summary>Raised when an owner deletes their gift list. See <c>GiftListCreated</c> for why this never leaves the process directly.</summary>
public sealed record GiftListDeleted(GiftListId ListId, DateTimeOffset DeletedAt) : IDomainEvent;
