using GiftLists.Application.GiftLists;
using GiftLists.Contracts.GiftLists;
using GiftLists.Contracts.GiftLists.Events;
using GiftLists.Domain.Common;
using GiftLists.IntegrationTests.Fixtures;
using GiftLists.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace GiftLists.IntegrationTests.GiftLists;

[Collection(GiftListsCollection.Name)]
public sealed class ChangeGiftListExpiryTests(GiftListsFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ChangeGiftListExpiry_ShouldMoveTheExpiryInMongo_AndPublishGiftListExpiryChangedV1()
    {
        // Arrange — a list already saved via the real repository, and a subscriber wired up
        // exactly the way the Gateway/Reservation would be.
        var ownerId = Guid.NewGuid();
        var list = await GiftListSeeding.SeedListAsync(fixture, ownerId);
        var newExpiresAt = Timestamps.ToStoredPrecision(DateTimeOffset.UtcNow.AddDays(30));
        await using var subscriber = await EventSubscriber<GiftListExpiryChangedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act
        await fixture.RequesterBus.Send(new ChangeGiftListExpiry(list.Id.Value, ownerId, newExpiresAt));
        var published = await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(list.Id.Value, published.ListId);
        Assert.Equal(newExpiresAt, published.ExpiresAt);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(newExpiresAt, persisted.Expiry.Value);
    }
}
