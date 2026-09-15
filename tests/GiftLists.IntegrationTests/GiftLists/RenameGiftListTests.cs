using GiftLists.Application.GiftLists;
using GiftLists.Contracts.GiftLists;
using GiftLists.Contracts.GiftLists.Events;
using GiftLists.IntegrationTests.Fixtures;
using GiftLists.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace GiftLists.IntegrationTests.GiftLists;

[Collection(GiftListsCollection.Name)]
public sealed class RenameGiftListTests(GiftListsFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RenameGiftList_ShouldUpdateTheNameInMongo_AndPublishGiftListRenamedV1()
    {
        // Arrange — a list already saved via the real repository, and a subscriber wired up
        // exactly the way the Gateway/Reservation would be.
        var ownerId = Guid.NewGuid();
        var list = await GiftListSeeding.SeedListAsync(fixture, ownerId);
        await using var subscriber = await EventSubscriber<GiftListRenamedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act
        await fixture.RequesterBus.Send(new RenameGiftList(list.Id.Value, ownerId, "Housewarming Wishlist"));
        var published = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(list.Id.Value, published.ListId);
        Assert.Equal("Housewarming Wishlist", published.Name);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("Housewarming Wishlist", persisted.Name.Value);
    }
}
