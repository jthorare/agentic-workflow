using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1b (messages spec §8.2): a ts reads and writes as the plain Slack ts string, so the read routes show "1767777120.000100", never an object.</summary>
[Lane(TestLane.Unit)]
public sealed class MessageTsJsonTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(MessagesJsonContext.Default);

    [Fact]
    public void A_ts_round_trips_as_the_plain_slack_string()
    {
        var json = Encoding.UTF8.GetString(AwJson.Serialize(new MessageTs("1767777120.000100"), _json));

        Assert.Equal("\"1767777120.000100\"", json);
        Assert.Equal(new MessageTs("1767777120.000100"), AwJson.Deserialize<MessageTs>(Encoding.UTF8.GetBytes(json), _json).Value);
    }

    [Fact]
    public void A_ts_inside_a_record_is_a_string_property()
    {
        var holder = new MessageTsHolder(new MessageTs("1767777120.000100"), new MessageTs("1767776520.000100"));

        var json = Encoding.UTF8.GetString(AwJson.Serialize(holder, _json));

        Assert.Equal("{\"ts\":\"1767777120.000100\",\"before\":\"1767776520.000100\"}", json);
        Assert.Equal(holder, AwJson.Deserialize<MessageTsHolder>(Encoding.UTF8.GetBytes(json), _json).Value);
    }

    [Fact]
    public void An_absent_ts_reads_as_null() =>
        Assert.Null(AwJson.Deserialize<MessageTsHolder>(Encoding.UTF8.GetBytes("{\"ts\":\"1767777120.000100\",\"before\":null}"), _json).Value.Before);

    [Theory]
    [InlineData("{\"ts\":\"1767777120\",\"before\":null}")]
    [InlineData("{\"ts\":\"not a ts\",\"before\":null}")]
    [InlineData("{\"ts\":1767777120.000100,\"before\":null}")]
    [InlineData("{\"ts\":{\"value\":\"1767777120.000100\"},\"before\":null}")]
    [InlineData("{\"ts\":\"1767777120.000100\",\"before\":\"x\"}")]
    public void A_malformed_ts_is_a_json_error(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, MessagesJsonContext.Default.MessageTsHolder));
        Assert.Equal(KernelErrors.Invalid.Code, AwJson.Deserialize<MessageTsHolder>(Encoding.UTF8.GetBytes(json), _json).Error.Code);
    }

    [Theory]
    [InlineData(MineReason.DirectMessage, "\"directMessage\"")]
    [InlineData(MineReason.WroteInThread, "\"wroteInThread\"")]
    [InlineData(MineReason.NamedMe, "\"namedMe\"")]
    [InlineData(MineReason.Followed, "\"followed\"")]
    public void Mine_reason_round_trips_as_a_camel_case_string(MineReason reason, string json)
    {
        Assert.Equal(json, Encoding.UTF8.GetString(AwJson.Serialize(reason, _json)));
        Assert.Equal(reason, AwJson.Deserialize<MineReason>(Encoding.UTF8.GetBytes(json), _json).Value);
    }
}
