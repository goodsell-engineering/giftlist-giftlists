using GiftLists.Application.Common;

namespace GiftLists.Infrastructure.Platform;

/// <summary>The real <see cref="IClock"/> — belongs to no domain, so it lives in Platform/ (CONVENTIONS.md "Folder structure").</summary>
internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
