using System.Security.Cryptography;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Infrastructure.Platform.Security;

/// <summary>
/// The real <see cref="IShareTokenGenerator"/> — belongs to no domain (purely mechanical
/// randomness, like <c>Identity.Infrastructure.Platform.Security.BCryptPasswordHasher</c>), so
/// it lives in Platform/Security/ (CONVENTIONS.md "Folder structure").
/// </summary>
internal sealed class ShareTokenGenerator : IShareTokenGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public string Generate()
    {
        // A cryptographically secure source (ARCHITECTURE.md "Auth & sharing" — "opaque unguessable
        // shareToken"), not System.Random: the token is a bearer capability, so predictability
        // would let a stranger enumerate other people's lists. The small modulo bias this leaves
        // (256 % 62 != 0) is an acceptable trade for this demo's threat model — an attacker still
        // faces the full 21-character search space, not a shortcut through it.
        var bytes = RandomNumberGenerator.GetBytes(ShareToken.Length);
        var chars = new char[ShareToken.Length];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }
}
