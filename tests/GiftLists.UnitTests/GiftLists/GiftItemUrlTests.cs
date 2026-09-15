using GiftLists.Domain.GiftLists;

namespace GiftLists.UnitTests.GiftLists;

public sealed class GiftItemUrlTests
{
    [Fact]
    public void Constructor_ShouldAccept_AnAbsoluteHttpsUrl()
    {
        // Arrange
        const string value = "https://example.com/grinder";

        // Act
        var url = new GiftItemUrl(value);

        // Assert
        Assert.Equal(value, url.Value);
    }

    [Fact]
    public void Constructor_ShouldAccept_AnAbsoluteHttpUrl()
    {
        // Arrange
        const string value = "http://example.com/grinder";

        // Act
        var url = new GiftItemUrl(value);

        // Assert
        Assert.Equal(value, url.Value);
    }

    [Fact]
    public void Constructor_ShouldTrimTheValue()
    {
        // Arrange
        const string padded = "  https://example.com/grinder  ";

        // Act
        var url = new GiftItemUrl(padded);

        // Assert
        Assert.Equal("https://example.com/grinder", url.Value);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsWhitespace()
    {
        // Arrange — none

        // Act
        var exception = Record.Exception(() => new GiftItemUrl("   "));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsNotAbsolute()
    {
        // Arrange — GL-74's own traced example of a value that resolves against the current
        // origin rather than being rejected outright.
        const string relative = "not-a-url-at-all";

        // Act
        var exception = Record.Exception(() => new GiftItemUrl(relative));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsProtocolRelative()
    {
        // Arrange — GL-74's headline vector: "//evil.example/" has no scheme of its own and
        // silently resolves to a third-party origin under the browser's current scheme.
        const string protocolRelative = "//evil.example/";

        // Act
        var exception = Record.Exception(() => new GiftItemUrl(protocolRelative));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("vbscript:msgbox(1)")]
    public void Constructor_ShouldThrowArgumentException_WhenSchemeIsNotHttpOrHttps(string value)
    {
        // Arrange — none. React already blocks "javascript:" at render time (GL-74's own
        // verification), but this type's rule is "absolute http/https only", not merely "not
        // javascript:", so it must reject these regardless of what one specific renderer does.

        // Act
        var exception = Record.Exception(() => new GiftItemUrl(value));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueExceedsMaxLength()
    {
        // Arrange
        var tooLong = "https://example.com/" + new string('a', GiftItemUrl.MaxLength);

        // Act
        var exception = Record.Exception(() => new GiftItemUrl(tooLong));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void IsValid_ShouldReturnTrue_WhenValueIsNullOrWhitespace()
    {
        // Arrange — Url is optional; "must be http/https" cannot be a rule about absence.

        // Act
        var validNull = GiftItemUrl.IsValid(null);
        var validWhitespace = GiftItemUrl.IsValid("   ");

        // Assert
        Assert.True(validNull);
        Assert.True(validWhitespace);
    }

    [Theory]
    [InlineData("https://example.com/grinder")]
    [InlineData("not-a-url-at-all")]
    [InlineData("//evil.example/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    public void IsValid_ShouldAgreeWithTheConstructor(string value)
    {
        // The Application validator's predicate and the constructor it stands in front of must
        // answer about the SAME set of values — see ExpiryDate.IsInFuture's own test for the
        // shape of bug this guards against: a validator that says "fine" followed by a
        // constructor that throws escapes the handler into Rebus's error queue instead of
        // returning giftlist.item_url_invalid.
        var predicateAccepts = GiftItemUrl.IsValid(value);
        var exception = Record.Exception(() => new GiftItemUrl(value));

        Assert.Equal(predicateAccepts, exception is null);
    }

    [Fact]
    public void Rehydrate_ShouldNotThrow_ForAProtocolRelativeValue()
    {
        // Arrange — GL-74 tightens this rule well after items with a completely unchecked Url
        // may already be persisted. Reloading one must not turn a stricter rule into an outage
        // for an existing list (GiftItemUrl's own doc comment).
        const string legacyValue = "//evil.example/";

        // Act
        var exception = Record.Exception(() => GiftItemUrl.Rehydrate(legacyValue));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Rehydrate_ShouldPreserveTheGivenValue()
    {
        // Arrange
        const string legacyValue = "//evil.example/";

        // Act
        var url = GiftItemUrl.Rehydrate(legacyValue);

        // Assert
        Assert.Equal(legacyValue, url.Value);
    }
}
