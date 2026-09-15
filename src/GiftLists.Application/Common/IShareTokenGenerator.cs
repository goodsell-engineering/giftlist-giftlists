namespace GiftLists.Application.Common;

/// <summary>
/// Port over the cryptographically-random source that backs
/// <c>GiftLists.Domain.GiftLists.ShareToken</c> (CONVENTIONS.md "Folder structure" — genuinely domain-agnostic
/// port, so it lives in <c>Common/</c> next to <see cref="IClock"/> rather than under
/// <c>GiftLists/</c>; also keeps it out of
/// <c>NamingConventionTests.InputPorts_ShouldHaveAMatchingInteractorAndRequest</c>'s text scan,
/// the same reason <c>Identity.Application.Common.ITokenIssuer</c> lives in <c>Common/</c>
/// rather than beside a use case). Application never sees a random-number-generation library by
/// name; <see cref="Generate"/> returns a raw string that
/// <c>GiftLists.Domain.GiftLists.ShareToken</c>'s constructor validates and wraps.
/// </summary>
public interface IShareTokenGenerator
{
    string Generate();
}
