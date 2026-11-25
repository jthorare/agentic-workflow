using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>#3588: the model sometimes answers with the output schema the prompt shows instead of filling it in.</summary>
[Lane(TestLane.Unit)]
public sealed class PromptSchemaEchoTests
{
    private const string Schema = """{"type":"object","required":["flagged","summary"],"properties":{"flagged":{"type":"boolean"},"summary":{"type":"string"}}}""";

    // the raw answer the Daily digest step saved in the real app: the schema shown in the prompt, implicit fields included
    private const string Echo = """{"type":"object","required":["flagged","summary"],"properties":{"flagged":{"type":"boolean"},"summary":{"type":"string"},"couldNotAnswer":{"type":"boolean"},"couldNotAnswerWhy":{"type":"string"}}}""";

    private static JsonObject WithSchema() => new() { ["prompt"] = "Today is Friday. Write one short line.", ["output"] = JsonNode.Parse(Schema) };

    private static Task<Result<BlockResult>> Run(RecordingModel model) =>
        new PromptBlock(model).Execute(BlockFakes.Context(WithSchema()), TestContext.Current.CancellationToken).AsTask();

    [Fact]
    public async Task A_schema_echo_is_asked_again_and_the_filled_second_answer_is_used()
    {
        var model = new RecordingModel(n => n == 1 ? Echo : """{"flagged":true,"summary":"Friday"}""");

        var result = await Run(model);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Equal("Friday", result.Value.Outputs["summary"]!.GetValue<string>());
        Assert.Equal(2, model.Prompts.Count);
        Assert.Contains("instead of filling it in", model.Prompts[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_schema_echo_twice_fails_saying_the_model_repeated_the_schema()
    {
        var model = new RecordingModel(_ => Echo);

        var result = await Run(model);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.GraphPromptAnswer.Code, result.Error.Code);
        Assert.Contains("the schema itself", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, model.Prompts.Count);
    }

    [Fact]
    public async Task An_answer_that_fills_a_schema_field_is_taken_even_with_type_and_properties_keys()
    {
        var model = new RecordingModel(_ => """{"type":"object","properties":{},"flagged":false,"summary":"quiet"}""");

        var result = await Run(model);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Equal("quiet", result.Value.Outputs["summary"]!.GetValue<string>());
        _ = Assert.Single(model.Prompts);
    }

    [Fact]
    public async Task The_instruction_says_never_to_repeat_the_schema_and_shows_an_example_answer()
    {
        var model = new RecordingModel(_ => """{"flagged":false,"summary":"quiet"}""");

        _ = await Run(model);

        var prompt = Assert.Single(model.Prompts);
        Assert.Contains("never repeat the schema", prompt, StringComparison.Ordinal);
        Assert.Contains("""{"flagged": <true or false>, "summary": "<your summary>"}""", prompt, StringComparison.Ordinal);
    }

    private sealed class RecordingModel(Func<int, string> script) : IGraphModelPort
    {
        public List<string> Prompts { get; } = [];

        public string Untrusted(string source, string text) => $"<untrusted>{text}</untrusted>";

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            return ValueTask.FromResult<Result<string>>(script(Prompts.Count));
        }
    }
}
