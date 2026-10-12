using System.Text;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Errors;
using Aw.Judge.Host;
using Aw.Judge.Testing;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Judge.Tests.Config;

// config.json is deserialized into JudgeConfig through the AwJson profile, then checked: anything else is AW-JUDGE-007.
public sealed class JudgeConfigParserTests
{
    private static Result<JudgeConfig> Parse(string text) =>
        JudgeConfigParser.Parse(Encoding.UTF8.GetBytes(text), AwJson.CreateOptions(JudgeJsonContext.Default));

    [Fact, Trait("Lane", "unit")]
    public void A_full_config_reads_every_field()
    {
        var config = Parse(
            """{"questions":{"wake-gate":{"enabled":false,"threshold":0.8},"ask-check":{"enabled":true,"threshold":0.6,"providers":["jev","rules"]}},"providers":{"agentClis":["codex-cli"],"jev":false},"promptSort":{"enabled":false,"budgetMs":800,"cooldownPrompts":2,"scaffolds":{"brief":true}},"unknownKey":1}""");

        Assert.Equal(
            "wake-gate=False/0.8/-; ask-check=True/0.6/[jev,rules]; providers=[codex-cli]|False; promptSort=False|800|2|brief:True",
            ConfigText.Of(config.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_config_without_wake_gate_gets_the_default_one_and_absent_blocks_stay_null()
    {
        var config = Parse("""{"questions":{"ask-check":{"enabled":true,"threshold":0.5}}}""");

        Assert.Equal("ask-check=True/0.5/-; wake-gate=True/0.7/-; ", ConfigText.Of(config.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_question_without_a_threshold_keeps_its_own()
    {
        var config = Parse("""{"questions":{"wake-gate":{"enabled":true},"ask-check":{"enabled":false}}}""");

        Assert.Equal("wake-gate=True//-; ask-check=False//-; ", ConfigText.Of(config.Value));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    [InlineData("{}")]
    [InlineData("""{"questions":[]}""")]
    [InlineData("""{"questions":{"x":null}}""")]
    [InlineData("""{"questions":{"x":{"enabled":"yes","threshold":0.5}}}""")]
    [InlineData("""{"questions":{"x":{"threshold":0.5}}}""")]
    [InlineData("""{"questions":{"x":{"enabled":true,"threshold":1.5}}}""")]
    [InlineData("""{"questions":{"x":{"enabled":true,"threshold":0.5,"providers":["gpt"]}}}""")]
    [InlineData("""{"questions":{},"providers":{"agentClis":["jev"]}}""")]
    [InlineData("""{"questions":{},"providers":{"jev":"no"}}""")]
    [InlineData("""{"questions":{},"promptSort":{"budgetMs":5000}}""")]
    [InlineData("""{"questions":{},"promptSort":{"cooldownPrompts":-1}}""")]
    [InlineData("""{"questions":{},"promptSort":{"cooldownPrompts":1.5}}""")]
    [InlineData("""{"questions":{},"promptSort":{"scaffolds":{"other":true}}}""")]
    public void Anything_that_isnt_a_valid_config_is_AW_JUDGE_007(string text)
    {
        var parsed = Parse(text);

        Assert.True(parsed.IsError);
        Assert.Equal(JudgeErrors.ConfigMalformed.Code, parsed.Error.Code);
        Assert.NotEmpty(parsed.Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_error_names_what_is_wrong()
    {
        var parsed = Parse("""{"questions":{"ask-check":{"enabled":true,"threshold":0.5,"providers":["gpt"]}}}""");

        Assert.StartsWith("question 'ask-check' providers: unknown name 'gpt'", parsed.Error.Message, StringComparison.Ordinal);
    }
}
