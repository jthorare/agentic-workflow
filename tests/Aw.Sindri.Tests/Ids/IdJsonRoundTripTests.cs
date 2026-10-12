using System.Text.Json;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

public sealed class IdJsonRoundTripTests
{
    private static readonly string _hash = new('a', 64);

    [Fact, Trait("Lane", "unit")]
    public void Ids_write_as_strings_and_read_back()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        Assert.Equal($"\"{_hash}\"", JsonSerializer.Serialize(ProfileHash.From(_hash), options));
        Assert.Equal(ServiceName.From("heartbeat"), JsonSerializer.Deserialize<ServiceName>("\"heartbeat\"", options));
        Assert.Equal(HostId.From("box"), JsonSerializer.Deserialize<HostId>("\"box\"", options));
        Assert.Equal(BootId.From("b-1"), JsonSerializer.Deserialize<BootId>("\"b-1\"", options));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("\"ABC\"")]
    [InlineData("\"heart beat\"")]
    public void A_malformed_id_is_a_json_error(string json)
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProfileHash>(json, options));
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ServiceName>(json, options));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_service_name_over_40_characters_is_refused() =>
        Assert.False(ServiceName.TryFrom(new string('a', 41), out _));
}
