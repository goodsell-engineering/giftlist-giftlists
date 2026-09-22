using GiftLists.Contracts.GiftLists.Events;
using GiftLists.Domain.Common;
using GiftLists.Domain.GiftLists;
using GiftLists.Domain.GiftLists.Events;
using GiftLists.Infrastructure.GiftLists.Messaging;

namespace GiftLists.UnitTests.GiftLists.Messaging;

/// <summary>
/// The ARCHITECTURE.md "Domain events are not integration events" seam: the aggregate raises a domain event, this mapper emits the
/// <c>*V1</c> integration event from <c>GiftLists.Contracts</c>, and nothing else in the service
/// is allowed to publish a domain type (ARCHITECTURE.md "Domain events are not integration events").
/// </summary>
/// <remarks>
/// <para>
/// GiftLists.IntegrationTests already proves each of the five events reaches a real broker, but
/// only one at a time, over the happy path of the interactor that raises it, and it asserts a
/// subset of fields per event. Neither the exhaustiveness of the switch nor the default arm's
/// throw is reachable that way at all — no interactor can raise an unmapped event, which is
/// exactly why that arm needs a unit test (CONVENTIONS.md "Testing": this suite fills the gaps the
/// integration suite cannot reach).
/// </para>
/// <para>
/// Every event below gets its OWN list id and its own timestamp, deliberately, rather than
/// sharing one set of constants. Shared values make these tests pass under a mapper that copies
/// the wrong field from the right event, or maps two arms to each other — the two mistakes this
/// file exists to catch.
/// </para>
/// </remarks>
public sealed class GiftListEventMapperTests
{
    private static readonly Guid CreatedListId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RenamedListId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DeletedListId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ItemAddedListId = new("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ItemRemovedListId = new("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ExpiryChangedListId = new("99999999-9999-9999-9999-999999999999");

    private static readonly Guid AddedItemId = new("66666666-6666-6666-6666-666666666666");
    private static readonly Guid RemovedItemId = new("77777777-7777-7777-7777-777777777777");
    private static readonly Guid ListOwnerId = new("88888888-8888-8888-8888-888888888888");

    private static readonly DateTimeOffset Now = new(2026, 3, 4, 9, 15, 30, 123, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresAt = new(2026, 4, 1, 12, 0, 0, 456, TimeSpan.Zero);
    private static readonly DateTimeOffset RenamedAt = new(2026, 3, 5, 10, 16, 31, 789, TimeSpan.Zero);
    private static readonly DateTimeOffset DeletedAt = new(2026, 3, 6, 11, 17, 32, 234, TimeSpan.Zero);
    private static readonly DateTimeOffset AddedAt = new(2026, 3, 7, 12, 18, 33, 567, TimeSpan.Zero);
    private static readonly DateTimeOffset RemovedAt = new(2026, 3, 8, 13, 19, 34, 891, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiryChangedAt = new(2026, 3, 9, 14, 20, 35, 135, TimeSpan.Zero);
    private static readonly DateTimeOffset NewExpiresAt = new(2026, 5, 1, 12, 0, 0, 246, TimeSpan.Zero);

    private const string ListName = "Birthday Wishlist";
    private const string NewListName = "Christmas Wishlist";
    private const string ItemName = "Espresso Machine";
    private const string ItemDescription = "The one with the steam wand";
    private const string ItemUrl = "https://example.com/espresso";
    private const string ShareTokenValue = "aB3xY7zQ1mN5pR9tV2wK4";

    /// <summary>
    /// Every domain event this service raises, as a CONSTRUCTED INSTANCE rather than a name.
    /// </summary>
    /// <remarks>
    /// This was a <c>string[]</c> of <c>nameof</c>s, and the guard below compared it to reflection
    /// over the Domain assembly. That guard did not guard the mapper: adding a sixth domain event
    /// AND its name to the array left the suite green with no mapper arm at all (demonstrated in
    /// the Batch 12 review — 122 passed with a deliberately unmapped sixth event). Worse, the red
    /// it did produce invited exactly that fix, so following the failure message re-broke it
    /// silently. A name costs nothing to add; a real instance cannot be added without the mapper
    /// being able to map it, because the guard now maps every one of these.
    /// </remarks>
    private static IDomainEvent[] EveryDomainEventInstance() =>
    [
        GiftListCreatedEvent(),
        GiftListRenamedEvent(),
        GiftListDeletedEvent(),
        GiftItemAddedEvent(),
        GiftItemRemovedEvent(),
        GiftListExpiryChangedEvent(),
    ];

    public static TheoryData<IDomainEvent, Guid> EveryDomainEventWithItsListId() => new()
    {
        { GiftListCreatedEvent(), CreatedListId },
        { GiftListRenamedEvent(), RenamedListId },
        { GiftListDeletedEvent(), DeletedListId },
        { GiftItemAddedEvent(), ItemAddedListId },
        { GiftItemRemovedEvent(), ItemRemovedListId },
        { GiftListExpiryChangedEvent(), ExpiryChangedListId },
    };

    [Fact]
    public void ToIntegrationEvent_ShouldMapGiftListCreatedToGiftListCreatedV1_CarryingEveryField()
    {
        // Arrange
        var domainEvent = GiftListCreatedEvent();

        // Act
        var integrationEvent = GiftListEventMapper.ToIntegrationEvent(domainEvent);

        // Assert
        var published = Assert.IsType<GiftListCreatedV1>(integrationEvent);
        Assert.Equal(CreatedListId, published.ListId);
        Assert.Equal(ListOwnerId, published.OwnerId);
        Assert.Equal(ListName, published.Name);
        Assert.Equal(ExpiresAt, published.ExpiresAt);
        Assert.Equal(ShareTokenValue, published.ShareToken);
        Assert.Equal(Now, published.CreatedAt);
    }

    [Fact]
    public void ToIntegrationEvent_ShouldMapGiftListRenamedToGiftListRenamedV1_CarryingEveryField()
    {
        // Arrange
        var domainEvent = GiftListRenamedEvent();

        // Act
        var integrationEvent = GiftListEventMapper.ToIntegrationEvent(domainEvent);

        // Assert
        var published = Assert.IsType<GiftListRenamedV1>(integrationEvent);
        Assert.Equal(RenamedListId, published.ListId);
        Assert.Equal(NewListName, published.Name);
        Assert.Equal(RenamedAt, published.RenamedAt);
    }

    [Fact]
    public void ToIntegrationEvent_ShouldMapGiftListExpiryChangedToGiftListExpiryChangedV1_CarryingEveryField()
    {
        // Arrange
        var domainEvent = GiftListExpiryChangedEvent();

        // Act
        var integrationEvent = GiftListEventMapper.ToIntegrationEvent(domainEvent);

        // Assert
        var published = Assert.IsType<GiftListExpiryChangedV1>(integrationEvent);
        Assert.Equal(ExpiryChangedListId, published.ListId);
        Assert.Equal(NewExpiresAt, published.ExpiresAt);
        Assert.Equal(ExpiryChangedAt, published.ChangedAt);
    }

    [Fact]
    public void ToIntegrationEvent_ShouldMapGiftListDeletedToGiftListDeletedV1_CarryingEveryField()
    {
        // Arrange
        var domainEvent = GiftListDeletedEvent();

        // Act
        var integrationEvent = GiftListEventMapper.ToIntegrationEvent(domainEvent);

        // Assert
        var published = Assert.IsType<GiftListDeletedV1>(integrationEvent);
        Assert.Equal(DeletedListId, published.ListId);
        Assert.Equal(DeletedAt, published.DeletedAt);
    }

    [Fact]
    public void ToIntegrationEvent_ShouldMapGiftItemAddedToGiftItemAddedV1_CarryingEveryField()
    {
        // Arrange
        var domainEvent = GiftItemAddedEvent();

        // Act
        var integrationEvent = GiftListEventMapper.ToIntegrationEvent(domainEvent);

        // Assert
        var published = Assert.IsType<GiftItemAddedV1>(integrationEvent);
        Assert.Equal(ItemAddedListId, published.ListId);
        Assert.Equal(AddedItemId, published.ItemId);
        Assert.Equal(ItemName, published.Name);
        Assert.Equal(ItemDescription, published.Description);
        Assert.Equal(ItemUrl, published.Url);
        Assert.Equal(AddedAt, published.AddedAt);
    }

    [Fact]
    public void ToIntegrationEvent_ShouldCarryNullsThrough_WhenGiftItemAddedHasNoDescriptionOrUrl()
    {
        // Arrange — description and url are the only optional fields on the whole wire surface,
        // so "absent stays absent" is the one nullability question a consumer projecting an item
        // depends on (ARCHITECTURE.md "Data model": items are { itemId, name, description, url }).
        var domainEvent = new GiftItemAdded(
            new GiftListId(ItemAddedListId),
            new GiftItemId(AddedItemId),
            new GiftItemName(ItemName),
            Description: null,
            Url: null,
            AddedAt);

        // Act
        var integrationEvent = GiftListEventMapper.ToIntegrationEvent(domainEvent);

        // Assert
        var published = Assert.IsType<GiftItemAddedV1>(integrationEvent);
        Assert.Null(published.Description);
        Assert.Null(published.Url);
        Assert.Equal(ItemName, published.Name);
    }

    [Fact]
    public void ToIntegrationEvent_ShouldMapGiftItemRemovedToGiftItemRemovedV1_CarryingEveryField()
    {
        // Arrange
        var domainEvent = GiftItemRemovedEvent();

        // Act
        var integrationEvent = GiftListEventMapper.ToIntegrationEvent(domainEvent);

        // Assert
        var published = Assert.IsType<GiftItemRemovedV1>(integrationEvent);
        Assert.Equal(ItemRemovedListId, published.ListId);
        Assert.Equal(RemovedItemId, published.ItemId);
        Assert.Equal(RemovedAt, published.RemovedAt);
    }

    [Fact]
    public void ToIntegrationEvent_ShouldThrow_WhenTheDomainEventHasNoMapping()
    {
        // Arrange — the whole point of the default arm. A new domain event added to the Domain
        // ring and not to the switch must fail loudly here; the alternative (returning null, or
        // some placeholder) would have GiftListEventPublisher publish nothing, or publish the
        // wrong thing, and consumers would simply never learn the write happened.
        var unmapped = new UnmappedDomainEvent();

        // Act
        var exception = Record.Exception(() => GiftListEventMapper.ToIntegrationEvent(unmapped));

        // Assert
        var invalidOperation = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains(nameof(UnmappedDomainEvent), invalidOperation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryDomainEvent_ShouldHaveARealInstanceHere_AndAMapperArmThatMapsIt()
    {
        // Arrange — reflection over the Domain assembly is what notices a sixth domain event
        // nobody told the mapper about. Without it, the default arm's throw is a runtime discovery
        // in production, after the aggregate has already been saved and the write is unannounced.
        var declared = typeof(IDomainEvent).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IDomainEvent).IsAssignableFrom(t))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        // Act
        var exercised = EveryDomainEventInstance()
            .Select(e => e.GetType().Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.Equal(exercised, declared);

        // ...and the half that was missing: every declared event must actually MAP. Comparing two
        // lists of names only ever proved the lists agreed with each other. Mapping each instance
        // is what makes a missing switch arm fail here rather than at runtime.
        foreach (var domainEvent in EveryDomainEventInstance())
        {
            var exception = Record.Exception(() => GiftListEventMapper.ToIntegrationEvent(domainEvent));
            Assert.Null(exception);
        }
    }

    [Theory]
    [MemberData(nameof(EveryDomainEventWithItsListId))]
    public void ListIdOf_ShouldReturnTheListTheEventConcerns_ForEveryDomainEventType(
        IDomainEvent domainEvent,
        Guid expectedListId)
    {
        // Arrange — each event carries a different list id, so an arm copied from its neighbour
        // fails here rather than passing on a shared constant.

        // Act
        var listId = GiftListEventMapper.ListIdOf(domainEvent);

        // Assert
        Assert.Equal(expectedListId, listId);
    }

    [Fact]
    public void ListIdOf_ShouldReturnEmpty_WhenTheDomainEventHasNoMapping()
    {
        // Arrange — deliberately NOT symmetrical with ToIntegrationEvent's throw, and pinned here
        // so a later refactor does not "tidy" the two into agreement. GiftListEventPublisher
        // calls this from inside its catch block, to name the list that drifted; throwing there
        // would replace a logged, diagnosable failure with a second exception and no log at all.
        var unmapped = new UnmappedDomainEvent();

        // Act
        var listId = GiftListEventMapper.ListIdOf(unmapped);

        // Assert
        Assert.Equal(Guid.Empty, listId);
    }

    private static GiftListCreated GiftListCreatedEvent() => new(
        new GiftListId(CreatedListId),
        new OwnerId(ListOwnerId),
        new GiftListName(ListName),
        new ExpiryDate(ExpiresAt, Now),
        new ShareToken(ShareTokenValue),
        Now);

    private static GiftListRenamed GiftListRenamedEvent() => new(
        new GiftListId(RenamedListId),
        new GiftListName(NewListName),
        RenamedAt);

    private static GiftListDeleted GiftListDeletedEvent() => new(new GiftListId(DeletedListId), DeletedAt);

    private static GiftListExpiryChanged GiftListExpiryChangedEvent() => new(
        new GiftListId(ExpiryChangedListId),
        new ExpiryDate(NewExpiresAt, Now),
        ExpiryChangedAt);

    private static GiftItemAdded GiftItemAddedEvent() => new(
        new GiftListId(ItemAddedListId),
        new GiftItemId(AddedItemId),
        new GiftItemName(ItemName),
        new GiftItemDescription(ItemDescription),
        new GiftItemUrl(ItemUrl),
        AddedAt);

    private static GiftItemRemoved GiftItemRemovedEvent() => new(
        new GiftListId(ItemRemovedListId),
        new GiftItemId(RemovedItemId),
        RemovedAt);

    /// <summary>
    /// A domain event the mapper has never heard of — the stand-in for the sixth event somebody
    /// adds to the Domain ring next year and forgets to map. Declared in the test project on
    /// purpose: putting it in GiftLists.Domain would make it a real domain event nobody raises.
    /// </summary>
    private sealed record UnmappedDomainEvent : IDomainEvent;
}
