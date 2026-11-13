using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>#3224: a prompt answer that says the model couldn't answer fails the step with its reason instead of passing a default.</summary>
[Lane(TestLane.Unit)]
public sealed class PromptCouldNotAnswerTests
{
    // why: the same prompt and answer shape as the "Weekday check" starter's "check" node
    private const string WeekdayCheck = """{"prompt":"Check whether anything needs attention today. Set flagged to true when it does, and summarize why.","output":{"type":"object","required":["flagged","summary"],"properties":{"flagged":{"type":"boolean"},"summary":{"type":"string"}}}}""";

    private static JsonObject Weekday() => (JsonObject)JsonNode.Parse(WeekdayCheck)!;

    private static Task<Result<BlockResult>> Run(ScriptedModel model) =>
        new PromptBlock(model).Execute(BlockFakes.Context(Weekday()), TestContext.Current.CancellationToken).AsTask();

    [Fact]
    public async Task A_could_not_answer_reply_fails_the_step_with_its_reason()
    {
        var model = new ScriptedModel("""{"flagged":false,"summary":"x","couldNotAnswer":true,"couldNotAnswerWhy":"I have no access to the CI system"}""");

        var result = await Run(model);

        Assert.True(result.IsError);
        Assert.Equal("SND-GRAPH-133", result.Error.Code);
        Assert.Contains("I have no access to the CI system", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_normal_reply_still_passes_and_hides_the_two_fields()
    {
        var model = new ScriptedModel("""{"flagged":true,"summary":"red","couldNotAnswer":false,"couldNotAnswerWhy":""}""");

        var result = await Run(model);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.True(result.Value.Outputs["flagged"]!.GetValue<bool>());
        Assert.Equal("red", result.Value.Outputs["summary"]!.GetValue<string>());
        Assert.False(result.Value.Outputs.ContainsKey("couldNotAnswer"));
        Assert.False(result.Value.Outputs.ContainsKey("couldNotAnswerWhy"));
    }

    [Fact]
    public async Task The_prompt_sent_to_the_model_asks_for_couldNotAnswer()
    {
        var model = new ScriptedModel("""{"flagged":false,"summary":"ok","couldNotAnswer":false,"couldNotAnswerWhy":""}""");

        _ = await Run(model);

        var prompt = Assert.Single(model.Prompts);
        Assert.Contains("couldNotAnswer", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_user_field_named_why_keeps_its_meaning_in_the_schema_and_the_outputs()
    {
        var config = (JsonObject)JsonNode.Parse("""{"prompt":"Why is the build red?","output":{"type":"object","required":["why"],"properties":{"why":{"type":"string","description":"keep me"}}}}""")!;
        var model = new ScriptedModel("""{"why":"flaky test","couldNotAnswer":false}""");

        var result = await new PromptBlock(model).Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken).AsTask();

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Contains("\"description\":\"keep me\"", Assert.Single(model.Prompts), StringComparison.Ordinal);
        Assert.Equal("flaky test", result.Value.Outputs["why"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_long_reason_is_cut_at_200_characters_with_an_ellipsis()
    {
        var model = new ScriptedModel($$"""{"couldNotAnswer":true,"couldNotAnswerWhy":"{{new string('x', 500)}}"}""");

        var result = await Run(model);

        Assert.True(result.IsError);
        Assert.Equal("SND-GRAPH-133", result.Error.Code);
        Assert.EndsWith(new string('x', 200) + "…", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputsFor_does_not_list_the_two_fields()
    {
        var fields = new PromptBlock(new ScriptedModel("")).OutputsFor(Weekday()).Fields;

        Assert.Equal(["flagged", "summary"], fields.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    private sealed class ScriptedModel(string answer) : IGraphModelPort
    {
        public List<string> Prompts { get; } = [];

        public string Untrusted(string source, string text) => $"<untrusted>{text}</untrusted>";

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            return ValueTask.FromResult<Result<string>>(answer);
        }
    }
}
