using System.Text.RegularExpressions;

namespace GiftLists.Domain.GiftLists;

/// <summary>
/// The opaque, unguessable capability that lets an anonymous guest view a list without logging
/// in (ARCHITECTURE.md "Auth & sharing" — "~21 chars, base62"; CONVENTIONS.md "Domain modelling" — value object for its
/// length/alphabet rule).
/// </summary>
/// <remarks>
/// Never constructed from a caller-chosen value — like
/// <c>Identity.Domain.Users.PasswordHash</c>, the raw string always comes from an Infrastructure
/// port (<c>GiftLists.Application.Common.IShareTokenGenerator</c>) using a cryptographically
/// secure random source; this type only ever validates and wraps the result. Domain has no
/// project references (CONVENTIONS.md "Project reference graph") and therefore cannot itself decide how "random" is
/// produced.
/// </remarks>
// Not an event — "ShareToken" merely ends in "en", tripping the past-tense-event-name heuristic.
// architecture:allow-irregular-event-name
public sealed class ShareToken : IEquatable<ShareToken>
{
    public const int Length = 21;

    private static readonly Regex Pattern = new(
        $@"^[0-9A-Za-z]{{{Length}}}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public ShareToken(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!Pattern.IsMatch(value))
        {
            throw new ArgumentException($"Value must be a {Length}-character base62 string.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public bool Equals(ShareToken? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as ShareToken);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
