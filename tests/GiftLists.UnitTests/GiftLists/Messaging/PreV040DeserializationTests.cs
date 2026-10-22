using System.Text;
using GiftLists.Contracts.GiftLists.Events;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Messages;
using Rebus.Serialization;
using Rebus.Transport.InMem;

namespace GiftLists.UnitTests.GiftLists.Messaging;

/// <summary>
/// TC-GL-146-20: a payload from before 0.4.0 has no <c>Priority</c>, and reads with a null one.
/// Goes through Rebus's own <c>SystemTextJsonSerializer</c> with its defaults — the serializer
/// BuildingBlocks leaves wired up — rather than hand-built <c>JsonSerializerOptions</c>, which
/// would prove a different configuration.
/// </summary>
public sealed class PreV040DeserializationTests
{
    private static async Task<object> DeserializeAsync(string json, Type type)
    {
        // Rebus's default serializer is internal, so capture the one a real (in-memory, never
        // used to send) bus builds. BuildingBlocks sets no serializer of its own.
        ISerializer? serializer = null;
        using var activator = new BuiltinHandlerActivator();
        using var bus = Configure.With(activator)
            .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), "pre-v040-test"))
            .Options(o => o.Decorate<ISerializer>(c => serializer = c.Get<ISerializer>()))
            .Start();

        var transportMessage = new TransportMessage(
            new Dictionary<string, string>
            {
                [Headers.ContentType] = "application/json;charset=utf-8",
                [Headers.Type] = $"{type.FullName}, {type.Assembly.GetName().Name}",
            },
            Encoding.UTF8.GetBytes(json));

        var message = await serializer!.Deserialize(transportMessage);
        return message.Body;
    }

    [Fact]
    public async Task GiftItemAddedV1_ShouldDeserializeWithANullPriority_WhenThePayloadHasNone()
    {
        // Arrange — exactly the 0.3.0 shape: six fields, no priority.
        var json = """
            {"ListId":"11111111-1111-1111-1111-111111111111","ItemId":"22222222-2222-2222-2222-222222222222",
             "Name":"Coffee grinder","Description":null,"Url":null,"AddedAt":"2026-01-01T00:00:00+00:00"}
            """;

        // Act
        var body = await DeserializeAsync(json, typeof(GiftItemAddedV1));

        // Assert
        var added = Assert.IsType<GiftItemAddedV1>(body);
        Assert.Null(added.Priority);
        Assert.Equal("Coffee grinder", added.Name);
    }

    [Fact]
    public async Task AddGiftItem_ShouldDeserializeWithANullPriority_WhenThePayloadHasNone()
    {
        // Arrange
        var json = """
            {"ListId":"11111111-1111-1111-1111-111111111111","RequesterId":"33333333-3333-3333-3333-333333333333",
             "ItemId":"22222222-2222-2222-2222-222222222222","Name":"Coffee grinder","Description":null,"Url":null}
            """;

        // Act
        var body = await DeserializeAsync(json, typeof(global::GiftLists.Contracts.GiftLists.AddGiftItem));

        // Assert
        var command = Assert.IsType<global::GiftLists.Contracts.GiftLists.AddGiftItem>(body);
        Assert.Null(command.Priority);
        Assert.Equal("Coffee grinder", command.Name);
    }

    // TC-GL-146-P05: until Reservations and the Gateway merge, a consumer on Contracts 0.3.0
    // receives 0.4.0 payloads with a trailing Priority. BuildingBlocks fail-fasts on a
    // JsonException, so a strict read would dead-letter every item add. These records are the
    // exact 0.3.0 shapes.
    internal sealed record GiftItemAddedV1As030(
        Guid ListId, Guid ItemId, string Name, string? Description, string? Url, DateTimeOffset AddedAt);

    internal sealed record AddGiftItemAs030(
        Guid ListId, Guid RequesterId, Guid ItemId, string Name, string? Description, string? Url);

    [Fact]
    public async Task GiftItemAddedV1_ShouldDeserializeIntoThe030Shape_WhenThePayloadCarriesAPriority()
    {
        // Arrange
        var json = """
            {"ListId":"11111111-1111-1111-1111-111111111111","ItemId":"22222222-2222-2222-2222-222222222222",
             "Name":"Coffee grinder","Description":"Burr","Url":"https://example.com/g","AddedAt":"2026-01-01T00:00:00+00:00","Priority":5}
            """;

        // Act
        var body = await DeserializeAsync(json, typeof(GiftItemAddedV1As030));

        // Assert
        var added = Assert.IsType<GiftItemAddedV1As030>(body);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), added.ListId);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), added.ItemId);
        Assert.Equal("Coffee grinder", added.Name);
        Assert.Equal("Burr", added.Description);
        Assert.Equal("https://example.com/g", added.Url);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), added.AddedAt);
    }

    [Fact]
    public async Task AddGiftItem_ShouldDeserializeIntoThe030Shape_WhenThePayloadCarriesAPriority()
    {
        // Arrange
        var json = """
            {"ListId":"11111111-1111-1111-1111-111111111111","RequesterId":"33333333-3333-3333-3333-333333333333",
             "ItemId":"22222222-2222-2222-2222-222222222222","Name":"Coffee grinder","Description":"Burr","Url":"https://example.com/g","Priority":5}
            """;

        // Act
        var body = await DeserializeAsync(json, typeof(AddGiftItemAs030));

        // Assert
        var command = Assert.IsType<AddGiftItemAs030>(body);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), command.ListId);
        Assert.Equal(Guid.Parse("33333333-3333-3333-3333-333333333333"), command.RequesterId);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), command.ItemId);
        Assert.Equal("Coffee grinder", command.Name);
        Assert.Equal("Burr", command.Description);
        Assert.Equal("https://example.com/g", command.Url);
    }
}
