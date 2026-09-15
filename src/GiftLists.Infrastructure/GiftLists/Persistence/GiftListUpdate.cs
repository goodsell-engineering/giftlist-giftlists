using MongoDB.Driver;

namespace GiftLists.Infrastructure.GiftLists.Persistence;

/// <summary>
/// One Mongo update expressing exactly the changes a <c>GiftList</c> recorded since it was
/// loaded — built by <see cref="GiftListToUpdateMapper"/>, applied by
/// <see cref="GiftListRepository.UpdateAsync"/>.
/// </summary>
/// <param name="Precondition">
/// Everything that must be true of the stored document for this update to apply: always its
/// <c>_id</c>, plus a per-element precondition for each item added or removed, plus the version
/// guard when the update writes a whole field. <see cref="GiftListRepository.UpdateAsync"/>
/// deliberately does not try to work out WHICH clause failed — every one of them failing means
/// the same thing to the caller (another writer got there first; retry against current state),
/// and an earlier revision of this type that carried extra members so the repository could tell
/// them apart used them to absorb one case as a success, which cost a duplicate published event.
/// See that method's own doc comment.
/// </param>
/// <param name="Change">The <c>$push</c>/<c>$pull</c>/<c>$set</c> operators themselves.</param>
internal sealed record GiftListUpdate(
    FilterDefinition<GiftListDocument> Precondition,
    UpdateDefinition<GiftListDocument> Change);
