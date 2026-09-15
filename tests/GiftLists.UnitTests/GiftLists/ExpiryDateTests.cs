using GiftLists.Domain.GiftLists;

namespace GiftLists.UnitTests.GiftLists;

public sealed class ExpiryDateTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_ShouldAccept_AValueStrictlyAfterNow()
    {
        // Arrange
        var future = Now.AddDays(1);

        // Act
        var expiry = new ExpiryDate(future, Now);

        // Assert
        Assert.Equal(future, expiry.Value);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueEqualsNow()
    {
        // Arrange — the boundary itself, not just a clearly-past date.

        // Act
        var exception = Record.Exception(() => new ExpiryDate(Now, Now));

        // Assert
        var argumentException = Assert.IsType<ArgumentException>(exception);
        Assert.Equal("value", argumentException.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsBeforeNow()
    {
        // Arrange
        var past = Now.AddDays(-1);

        // Act
        var exception = Record.Exception(() => new ExpiryDate(past, Now));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Rehydrate_ShouldNotThrow_ForAValueAlreadyInThePast()
    {
        // Arrange — a list loaded back out of storage may legitimately already be expired
        // (ExpiryDate's own doc comment); only the validating constructor enforces
        // "must be future at creation".
        var past = Now.AddYears(-1);

        // Act
        var exception = Record.Exception(() => ExpiryDate.Rehydrate(past));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Rehydrate_ShouldPreserveTheGivenValue()
    {
        // Arrange
        var past = Now.AddYears(-1);

        // Act
        var expiry = ExpiryDate.Rehydrate(past);

        // Assert
        Assert.Equal(past, expiry.Value);
    }

    [Fact]
    public void IsInFuture_ShouldReturnFalse_WhenValueEqualsNow()
    {
        // Arrange — none

        // Act
        var isInFuture = ExpiryDate.IsInFuture(Now, Now);

        // Assert
        Assert.False(isInFuture);
    }

    [Fact]
    public void IsInFuture_ShouldReturnFalse_WhenValueIsOnlyOneTickAfterNow()
    {
        // This asserted TRUE until the Batch 11 review, and flipping it is a deliberate semantic
        // change, not a test relaxed to accommodate a fix. An instant in this domain has millisecond
        // resolution (Domain.Common.Timestamps), so a value one tick later IS now — not a later
        // instant. The old assertion was one half of a contradiction the suite pinned as correct:
        // it said the Application validator should accept such an expiry, while the constructor
        // (which normalised first) rejected it, so a validated request threw into Rebus's error
        // queue instead of returning giftlist.expiry_invalid.
        // The companion test below is what stops this becoming vacuous by making IsInFuture simply
        // answer false more often.
        // Arrange
        var oneTickLater = Now.AddTicks(1);

        // Act
        var isInFuture = ExpiryDate.IsInFuture(oneTickLater, Now);

        // Assert
        Assert.False(isInFuture);
    }
    [Fact]
    public void IsInFuture_ShouldReturnTrue_WhenValueIsOneMillisecondAfterNow()
    {
        // The other side of the resolution boundary. Without this, the fix above could be satisfied
        // by an IsInFuture that answers false for everything.
        // Arrange
        var oneMillisecondLater = Now.AddMilliseconds(1);

        // Act
        var isInFuture = ExpiryDate.IsInFuture(oneMillisecondLater, Now);

        // Assert
        Assert.True(isInFuture);
    }


    [Fact]
    public void HasExpired_ShouldReturnTrue_WhenNowEqualsExpiry()
    {
        // Arrange
        var expiry = new ExpiryDate(Now.AddDays(1), Now);

        // Act
        var hasExpired = expiry.HasExpired(expiry.Value);

        // Assert
        Assert.True(hasExpired);
    }

    [Fact]
    public void HasExpired_ShouldReturnFalse_WhenNowIsOneTickBeforeExpiry()
    {
        // Arrange
        var expiry = new ExpiryDate(Now.AddDays(1), Now);

        // Act
        var hasExpired = expiry.HasExpired(expiry.Value.AddTicks(-1));

        // Assert
        Assert.False(hasExpired);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5_000)]
    [InlineData(9_999)]
    [InlineData(TimeSpan.TicksPerMillisecond)]
    public void IsInFuture_ShouldAgreeWithTheConstructor_AtSubMillisecondOffsets(long ticks)
    {
        // The predicate the Application validator calls and the constructor that enforces the same
        // rule must answer about the SAME value. They did not: the constructor normalised to
        // millisecond resolution and then validated, while IsInFuture tested the raw input. For any
        // expiry in (now, now+1ms) the validator returned Success and the constructor then threw,
        // escaping the handler into Rebus's error queue instead of returning giftlist.expiry_invalid.
        //
        // Asserting AGREEMENT rather than either answer separately is the point: the suite already
        // had a test pinning IsInFuture(Now.AddTicks(1), Now) == true and another pinning that the
        // constructor rejects anything not strictly after Now. Both were green and neither could see
        // the other (Batch 11 review).
        var value = Now.AddTicks(ticks);

        var predicateAccepts = ExpiryDate.IsInFuture(value, Now);
        var exception = Record.Exception(() => new ExpiryDate(value, Now));

        Assert.Equal(predicateAccepts, exception is null);
    }
}
