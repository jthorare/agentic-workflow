using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Templates;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>#3079: a prompt node with an 'output' schema must ask the model for that JSON, and a prose answer fails in words.</summary>
[Lane(TestLane.Unit)]
public sealed class PromptAnswerJsonTests
{
    private const string Schema = """{"type":"object","required":["flagged","summary"],"properties":{"flagged":{"type":"boolean"},"summary":{"type":"string"}}}""";

    private static JsonObject WithSchema(string prompt = "Check the day.") => new() { ["prompt"] = prompt, ["output"] = JsonNode.Parse(Schema) };

    private static Task<Result<BlockResult>> Run(RecordingModel model, JsonObject config) =>
        new PromptBlock(model).Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken).AsTask();

    [Fact]
    public async Task A_prompt_with_an_output_schema_asks_for_a_JSON_object_naming_the_properties()
    {
        var model = new RecordingModel((_, _) => """{"flagged":false,"summary":"ok"}""");

        _ = await Run(model, WithSchema());

        var prompt = Assert.Single(model.Prompts);
        Assert.Contains("JSON", prompt, StringComparison.Ordinal);
        Assert.Contains("flagged", prompt, StringComparison.Ordinal);
        Assert.Contains("summary", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_prompt_without_an_output_schema_sends_exactly_the_rendered_text()
    {
        var model = new RecordingModel((_, _) => "free text");

        var result = await Run(model, new JsonObject { ["prompt"] = "Say hi." });

        Assert.Equal(["Say hi."], model.Prompts);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_prose_answer_is_asked_again_and_the_JSON_second_answer_is_used()
    {
        var model = new RecordingModel((n, _) => n == 1 ? "Unfortunately I can't check that." : """{"flagged":false,"summary":"ok"}""");

        var result = await Run(model, WithSchema());

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.False(result.Value.Outputs["flagged"]!.GetValue<bool>());
        Assert.Equal(2, model.Prompts.Count);
    }

    [Fact]
    public async Task A_valid_JSON_answer_is_taken_with_one_ask()
    {
        var model = new RecordingModel((_, _) => """{"flagged":true,"summary":"x"}""");

        var result = await Run(model, WithSchema());

        Assert.True(result.IsSuccess);
        _ = Assert.Single(model.Prompts);
    }

    [Fact]
    public async Task Prose_twice_fails_in_words_without_the_parser_message()
    {
        var model = new RecordingModel((_, _) => "Unfortunately I can't check that.");

        var result = await Run(model, WithSchema());

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.GraphPromptAnswer.Code, result.Error.Code);
        Assert.Contains("not the JSON", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("Unfortunately I can't", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("LineNumber", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid start", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_long_prose_answer_is_cut_to_200_characters_in_the_message()
    {
        var model = new RecordingModel((_, _) => new string('z', 500));

        var result = await Run(model, WithSchema());

        Assert.True(result.IsError);
        Assert.Contains(new string('z', 200), result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('z', 201), result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_paragraphed_prose_answer_shows_on_one_line()
    {
        var model = new RecordingModel((_, _) => "First paragraph.\n\n  Second\tparagraph.\r\nThird.");

        var result = await Run(model, WithSchema());

        Assert.True(result.IsError);
        Assert.DoesNotContain('\n', result.Error.Message);
        Assert.DoesNotContain('\r', result.Error.Message);
        Assert.Contains("\"First paragraph. Second paragraph. Third.\"", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_answer_over_200_characters_ends_in_an_ellipsis_inside_the_quotes()
    {
        var model = new RecordingModel((_, _) => new string('z', 201));

        var result = await Run(model, WithSchema());

        Assert.True(result.IsError);
        Assert.EndsWith(new string('z', 200) + "…\"", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_answer_of_exactly_200_characters_has_no_ellipsis()
    {
        var model = new RecordingModel((_, _) => new string('z', 200));

        var result = await Run(model, WithSchema());

        Assert.True(result.IsError);
        Assert.EndsWith(new string('z', 200) + "\"", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("…", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_cut_never_splits_a_surrogate_pair()
    {
        var model = new RecordingModel((_, _) => new string('z', 199) + "😀" + new string('z', 50));

        var result = await Run(model, WithSchema());

        Assert.True(result.IsError);
        Assert.EndsWith(new string('z', 199) + "…\"", result.Error.Message, StringComparison.Ordinal);
    }

    public static TheoryData<string, string> StockPromptNodes()
    {
        var data = new TheoryData<string, string>();
        foreach (var template in GraphTemplates.All)
        {
            foreach (var node in template.Spec.Nodes.Where(static n => string.Equals(n.Type, "prompt", StringComparison.Ordinal)))
            {
                data.Add(template.Name, node.Id.Value);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(StockPromptNodes))]
    public async Task Every_stock_template_prompt_node_gets_a_JSON_answer_on_its_first_run(string template, string nodeId)
    {
        var node = GraphTemplates.Find(template)!.Spec.Nodes.Single(n => string.Equals(n.Id.Value, nodeId, StringComparison.Ordinal));
        var schema = (JsonObject)node.Config["output"]!;
        var names = ((JsonObject)schema["properties"]!).Select(static p => p.Key).ToList();
        var model = new RecordingModel((_, prompt) =>
        {
            if (!prompt.Contains("JSON", StringComparison.Ordinal) || names.Exists(n => !prompt.Contains(n, StringComparison.Ordinal)))
            {
                return "Sure, nothing needs attention today.";
            }

            var answer = new JsonObject();
            foreach (var (name, spec) in (JsonObject)schema["properties"]!)
            {
                answer[name] = spec?["type"]?.GetValue<string>() switch
                {
                    "boolean" => false,
                    "number" or "integer" => 0,
                    _ => "x",
                };
            }

            return answer.ToJsonString();
        });

        var result = await new PromptBlock(model).Execute(BlockFakes.Context((JsonObject)node.Config.DeepClone(), node: nodeId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, $"template '{template}' node '{nodeId}': {(result.IsError ? result.Error.Message : null)}");
    }

    private sealed class RecordingModel(Func<int, string, string> script) : IGraphModelPort
    {
        public List<string> Prompts { get; } = [];

        public string Untrusted(string source, string text) => $"<untrusted>{text}</untrusted>";

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            return ValueTask.FromResult<Result<string>>(script(Prompts.Count, prompt));
        }
    }
}
