using MongoDB.Bson.Serialization.Attributes;
using Rebus.Sagas;

namespace GiftLists.Infrastructure.GiftLists.Messaging;

/// <summary>
/// The persisted state of one <see cref="GiftListExpirySaga"/> instance — one per live list, in
/// Mongo via Rebus.MongoDb (ARCHITECTURE.md "Sagas: list expiry": "Mongo-backed, as above").
/// </summary>
/// <remarks>
/// <para>
/// This is a persistence document in the Infrastructure ring, so the <c>[Bson*]</c> attributes
/// are allowed here (CONVENTIONS.md "Persistence" bans them from Domain, not from Infrastructure).
/// They are not optional. <c>BuildingBlocks.Persistence.MongoConventions</c> registers a
/// camelCase element-name convention for every class map in the process, and Rebus.MongoDb's saga
/// storage reads and writes its own fields by their C# property names — it filters on
/// <c>Revision</c> for its optimistic-concurrency check, on the correlation property name
/// (<c>ListId</c>) when it looks a saga up, and refuses to start at all if a test serialization
/// does not produce <c>_id</c> and <c>Revision</c> verbatim. Without these attributes it would
/// write <c>revision</c>/<c>listId</c> and then query for <c>Revision</c>/<c>ListId</c>, finding
/// nothing. <see cref="ISagaData"/> is implemented directly rather than via Rebus's
/// <c>SagaData</c> base class for the same reason: the base class's members cannot carry them.
/// </para>
/// <para>
/// <see cref="ExpiresAtUtc"/> is a UTC <see cref="DateTime"/>, not a <see cref="DateTimeOffset"/>,
/// for the same reason <c>GiftListDocument.ExpiresAt</c> is: it stores as a BSON date. Every
/// expiry in this domain is already millisecond-precise (<c>GiftLists.Domain.Common.Timestamps</c>),
/// so the round trip is lossless and the equality <see cref="GiftListExpirySaga"/> relies on holds.
/// </para>
/// </remarks>
internal sealed class GiftListExpirySagaData : ISagaData
{
    [BsonId]
    public Guid Id { get; set; }

    [BsonElement("Revision")]
    public int Revision { get; set; }

    /// <summary>The correlation property — every message this saga handles carries the list id.</summary>
    [BsonElement("ListId")]
    public Guid ListId { get; set; }

    /// <summary>The expiry currently scheduled. A <see cref="GiftListExpiryDue"/> carrying any other instant is stale.</summary>
    [BsonElement("ExpiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }
}
