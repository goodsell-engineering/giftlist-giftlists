using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.UnitTests.Support;

/// <summary>Hand-written fake — always returns a fixed, already-valid <see cref="ShareToken"/>-shaped string, so interactor tests never depend on real randomness.</summary>
internal sealed class FakeShareTokenGenerator : IShareTokenGenerator
{
    private readonly string _value;

    public FakeShareTokenGenerator(string? value = null) =>
        _value = value ?? new string('a', ShareToken.Length);

    public string Generate() => _value;
}
