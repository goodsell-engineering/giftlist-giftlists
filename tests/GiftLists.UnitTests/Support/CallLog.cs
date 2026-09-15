namespace GiftLists.UnitTests.Support;

/// <summary>
/// A single ordered timeline shared by more than one fake, so a test can assert the order of calls
/// made ACROSS ports rather than only within one.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FakeGiftListRepository.Calls"/> and <see cref="FakeDomainEventPublisher.PublishedBatches"/>
/// are separate lists, so no assertion built from them can express "the save happened before the
/// publish" — each fake only knows its own history. Both fakes' doc comments claimed to support
/// relative-order assertions, and no test ever made one, because none could.
/// </para>
/// <para>
/// That mattered: ARCHITECTURE.md "Event publishing: synchronous"'s save-then-publish rule is the ordering GL-20 was written
/// to establish, and the only test covering it was an integration test that observed the published
/// event and then checked Mongo. That test passes 3/3 with the ordering fully INVERTED (verified by
/// mutation) — under publish-then-save the event still has to cross a real broker, which takes
/// longer than the local write it was racing, so the write has almost always landed by the time the
/// assertion runs. It was green for a reason unrelated to the rule it named.
/// </para>
/// </remarks>
internal sealed class CallLog
{
    public List<string> Entries { get; } = [];

    public void Record(string name) => Entries.Add(name);
}
