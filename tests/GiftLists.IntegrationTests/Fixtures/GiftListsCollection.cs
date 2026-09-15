namespace GiftLists.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public sealed class GiftListsCollection : ICollectionFixture<GiftListsFixture>
{
    public const string Name = "GiftLists";
}
