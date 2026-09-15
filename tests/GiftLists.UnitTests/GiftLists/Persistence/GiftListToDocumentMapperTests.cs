using GiftLists.Domain.GiftLists;
using GiftLists.Infrastructure.GiftLists.Persistence;

namespace GiftLists.UnitTests.GiftLists.Persistence;

/// <summary>
/// GL-74's decision #3: rehydration TRUSTS storage rather than re-validating it, mirroring the
/// established <c>ExpiryDate.Rehydrate</c> pattern. Url/Description gained rules (absolute
/// http/https; a length bound) well after items with a completely unchecked <c>Url</c>/unbounded
/// <c>Description</c> may already be persisted — this suite proves reloading one of those legacy
/// items does not throw, which <c>GiftListRepositoryTests</c> cannot exercise directly (there is
/// no wire-level way to force an already-invalid stored value; only a document built by hand can).
/// </summary>
public sealed class GiftListToDocumentMapperTests
{
    private static GiftListDocument DocumentWithOneItem(GiftItemDocument item) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = Guid.NewGuid(),
        Name = "Birthday Wishlist",
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        ShareToken = new string('a', 21),
        CreatedAt = DateTime.UtcNow,
        Version = 1,
        Items = [item],
    };

    [Fact]
    public void ToAggregate_ShouldNotThrow_ForAnItemWithAProtocolRelativeStoredUrl()
    {
        // Arrange — GL-74's rule ("absolute http/https only") postdates this stored value; it
        // could only have been written back when Url was a bare, unchecked string.
        var document = DocumentWithOneItem(new GiftItemDocument
        {
            ItemId = Guid.NewGuid(),
            Name = "Coffee grinder",
            Description = null,
            Url = "//evil.example/",
        });

        // Act
        var exception = Record.Exception(() => GiftListToDocumentMapper.ToAggregate(document));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void ToAggregate_ShouldPreserveTheStoredUrl_EvenThoughItWouldNowFailValidation()
    {
        // Arrange — trusting storage means the value round-trips unchanged, not silently
        // dropped: dropping it would be a different, undiscussed decision (quietly rewriting an
        // owner's data on read), not the one GL-74 makes.
        const string legacyUrl = "//evil.example/";
        var document = DocumentWithOneItem(new GiftItemDocument
        {
            ItemId = Guid.NewGuid(),
            Name = "Coffee grinder",
            Description = null,
            Url = legacyUrl,
        });

        // Act
        var list = GiftListToDocumentMapper.ToAggregate(document);

        // Assert
        var item = Assert.Single(list.Items);
        Assert.Equal(legacyUrl, item.Url?.Value);
    }

    [Fact]
    public void ToAggregate_ShouldNotThrow_ForAnItemWithADescriptionExceedingTheNewMaxLength()
    {
        // Arrange — GL-74's length bound postdates this stored value.
        var legacyDescription = new string('a', 3000);
        var document = DocumentWithOneItem(new GiftItemDocument
        {
            ItemId = Guid.NewGuid(),
            Name = "Coffee grinder",
            Description = legacyDescription,
            Url = null,
        });

        // Act
        var exception = Record.Exception(() => GiftListToDocumentMapper.ToAggregate(document));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void ToAggregate_ShouldRehydrateNullDescriptionAndUrl_AsNull()
    {
        // Arrange — the optional-field round-trip GL-74 must not break: "no URL" stays "no URL".
        var document = DocumentWithOneItem(new GiftItemDocument
        {
            ItemId = Guid.NewGuid(),
            Name = "Coffee grinder",
            Description = null,
            Url = null,
        });

        // Act
        var list = GiftListToDocumentMapper.ToAggregate(document);

        // Assert
        var item = Assert.Single(list.Items);
        Assert.Null(item.Description);
        Assert.Null(item.Url);
    }

    [Fact]
    public void ToDocument_ShouldWriteNull_WhenTheItemHasNoUrlOrDescription()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var list = GiftList.Create(
            new GiftListId(Guid.NewGuid()),
            new OwnerId(Guid.NewGuid()),
            new GiftListName("Birthday Wishlist"),
            new ExpiryDate(now.AddDays(7), now),
            new ShareToken(new string('a', 21)),
            now);
        list.AddItem(GiftItemId.New(), new GiftItemName("Coffee grinder"), null, null, now);

        // Act
        var document = GiftListToDocumentMapper.ToDocument(list);

        // Assert
        var item = Assert.Single(document.Items);
        Assert.Null(item.Description);
        Assert.Null(item.Url);
    }
}
