using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.Launch;

public sealed class AgentIdTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(Ids.IdsJsonContext.Default);

    [Theory, Trait("Lane", "unit")]
    [InlineData("Claude-code")]
    [InlineData("CLAUDE")]
    [InlineData("a")]
    [InlineData("1abc")]
    [InlineData("-abc")]
    [InlineData("abc_def")]
    public void AgentId_rejects_upper_case_and_other_shapes(string value) =>
        Assert.False(AgentId.TryParse(value, provider: null, out _));

    [Fact, Trait("Lane", "unit")]
    public void AgentId_rejects_a_33_char_id_and_takes_a_32_char_one()
    {
        Assert.False(AgentId.TryParse("a" + new string('b', 32), provider: null, out _));
        Assert.True(AgentId.TryParse("a" + new string('b', 31), provider: null, out _));
    }

    [Fact, Trait("Lane", "unit")]
    public void AgentId_round_trips_json()
    {
        var id = AgentId.From("claude-code");

        var back = AwJson.Deserialize<AgentIdHolder>(AwJson.Serialize(new AgentIdHolder(id), _json), _json);

        Assert.True(back.IsSuccess);
        Assert.Equal(id, back.Value.Id);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{\"id\":\"Claude\"}")]
    [InlineData("{}")]
    [InlineData("{\"id\":null}")]
    public void Invalid_missing_or_null_is_AW_KERNEL_001(string json)
    {
        var read = AwJson.Deserialize<AgentIdHolder>(Encoding.UTF8.GetBytes(json), _json);

        Assert.True(read.IsError);
        Assert.Equal(KernelErrors.Invalid.Code, read.Error.Code);
    }
}
