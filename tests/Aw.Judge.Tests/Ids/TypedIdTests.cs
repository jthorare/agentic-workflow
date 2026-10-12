using System.Text;
using System.Text.Json;
using Aw.Judge.Core.Ids;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Ids;

/// <summary>Every Judge id round-trips through the Kernel profile; an invalid, missing or null one is AW-KERNEL-001 (R9).</summary>
[Lane(TestLane.Unit)]
public sealed class TypedIdTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    private static JudgeIdsHolder Holder(string providerIds) => new(
        DecisionId.From("00000000-0000-4000-8000-000000000001"), ToolUseId.From(providerIds), AgentId.From(providerIds),
        SessionId.From(providerIds), PromptId.From(providerIds));

    private static Result<JudgeIdsHolder> Read(string json) => AwJson.Deserialize<JudgeIdsHolder>(Encoding.UTF8.GetBytes(json), _json);

    [Theory]
    [InlineData("toolu_01ABC")]
    [InlineData(" ")]
    public void Every_id_round_trips_and_a_provider_id_may_be_blank(string providerIds)
    {
        var holder = Holder(providerIds);

        var read = AwJson.Deserialize<JudgeIdsHolder>(AwJson.Serialize(holder, _json), _json);

        Assert.Equal(holder, read.Value);
    }

    [Theory]
    [InlineData("""{"decision":" ","toolUse":"t","agent":"a","session":"s","prompt":"p"}""")]
    [InlineData("""{"toolUse":"t","agent":"a","session":"s","prompt":"p"}""")]
    [InlineData("""{"decision":null,"toolUse":"t","agent":"a","session":"s","prompt":"p"}""")]
    [InlineData("""{"decision":"d","toolUse":null,"agent":"a","session":"s","prompt":"p"}""")]
    public void A_blank_decision_id_or_a_missing_or_null_id_is_AW_KERNEL_001(string json) =>
        Assert.Equal(KernelErrors.Invalid.Code, Read(json).Error.Code);

    // The id itself takes an empty string (the TS judge stores one from argv); the Kernel's JSON converter refuses an
    // empty id, so a JSON payload carrying one is AW-KERNEL-001.
    [Fact]
    public void An_empty_provider_id_is_valid_but_the_JSON_converter_refuses_it()
    {
        Assert.True(ToolUseId.TryParse(string.Empty, provider: null, out _));
        Assert.Equal(KernelErrors.Invalid.Code, Read("""{"decision":"d","toolUse":"","agent":"a","session":"s","prompt":"p"}""").Error.Code);
    }

    [Fact]
    public void A_provider_id_over_4096_characters_is_invalid()
    {
        var json = "{\"decision\":\"d\",\"toolUse\":\"" + new string('x', 4097) + "\",\"agent\":\"a\",\"session\":\"s\",\"prompt\":\"p\"}";

        Assert.Equal(KernelErrors.Invalid.Code, Read(json).Error.Code);
        Assert.False(ToolUseId.TryParse(new string('x', 4097), provider: null, out _));
        Assert.True(ToolUseId.TryParse(new string('x', 4096), provider: null, out _));
    }
}
