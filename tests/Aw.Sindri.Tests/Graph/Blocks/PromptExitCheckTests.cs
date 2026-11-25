using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>P3e (#3196, spec §6.2): the prompt block checks the CLI's exit code and the text before it parses; a bad one is SND-GRAPH-103 with the model text kept as raw.</summary>
[Lane(TestLane.Unit)]
public sealed class PromptExitCheckTests
{
    private const string Schema = """{"type":"object","required":["flagged"],"properties":{"flagged":{"type":"boolean"}}}""";

    private static JsonObject Config(string prompt = "Check the day.") => new() { ["prompt"] = prompt, ["output"] = JsonNode.Parse(Schema) };

    private static ValueTask<Result<BlockResult>> Run(ExitModel model, BlockContext context) => new PromptBlock(model).Execute(context, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_non_zero_exit_fails_with_103_even_when_the_text_is_valid_JSON()
    {
        var model = new ExitModel("""{"flagged":false}""", exitCode: 1);

        var result = await Run(model, BlockFakes.Context(Config()));

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.GraphPromptAnswer.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_non_zero_exit_is_not_asked_again_because_a_retry_fails_the_same_way_on_a_usage_limit()
    {
        var model = new ExitModel("Usage limit reached. Try again at 5pm.", exitCode: 1);

        var result = await Run(model, BlockFakes.Context(Config()));

        Assert.Equal(SindriErrors.GraphPromptAnswer.Code, result.Error.Code);
        Assert.Equal(1, model.Asks);
        Assert.Contains("not the JSON this step asks for", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("Usage limit reached", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_non_zero_exit_leaves_the_model_text_as_raw()
    {
        var context = BlockFakes.Context(Config());

        _ = await Run(new ExitModel("Usage limit reached. Try again at 5pm.", exitCode: 1), context);

        Assert.Equal("Usage limit reached. Try again at 5pm.", context.Raw);
    }

    [Fact]
    public async Task An_answer_that_does_not_start_a_JSON_value_leaves_the_last_text_as_raw()
    {
        var context = BlockFakes.Context(Config());

        var result = await Run(new ExitModel("Sorry, no JSON today.", exitCode: 0), context);

        Assert.Equal(SindriErrors.GraphPromptAnswer.Code, result.Error.Code);
        Assert.Equal("Sorry, no JSON today.", context.Raw);
    }

    [Fact]
    public async Task An_exit_of_zero_with_a_JSON_answer_succeeds_and_leaves_no_raw()
    {
        var context = BlockFakes.Context(Config());

        var result = await Run(new ExitModel("""{"flagged":true}""", exitCode: 0), context);

        Assert.True(result.IsSuccess);
        Assert.Null(context.Raw);
    }

    [Fact]
    public async Task A_port_that_only_implements_Ask_counts_as_exit_zero()
    {
        var result = await new PromptBlock(new BlockFakes.ScriptedModelPort("""{"flagged":true}""")).Execute(BlockFakes.Context(Config()), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_prompt_whose_only_reference_has_no_value_fails_with_113_naming_the_reference()
    {
        var model = new ExitModel("""{"flagged":true}""", exitCode: 0);

        var result = await Run(model, BlockFakes.Context(Config("{{trigger.pr.body}}")));

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.GraphPromptFieldEmpty.Code, result.Error.Code);
        Assert.Contains("trigger.pr.body", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, model.Asks);   // the model is never asked with an empty prompt
    }

    [Fact]
    public void The_113_definition_is_registered_with_its_code()
    {
        Assert.Equal("SND-GRAPH-113", SindriErrors.GraphPromptFieldEmpty.Code);
        Assert.Contains(SindriErrors.GraphPromptFieldEmpty, SindriErrors.All);
    }

    private sealed class ExitModel(string text, int exitCode) : IGraphModelPort
    {
        public int Asks { get; private set; }

        public string Untrusted(string source, string text) => $"<untrusted>{text}</untrusted>";

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken) =>
            throw new NotSupportedException("the block must ask through Answer to see the exit code");

        ValueTask<Result<ModelAnswer>> IGraphModelPort.Answer(string prompt, CancellationToken cancellationToken)
        {
            Asks++;
            return ValueTask.FromResult<Result<ModelAnswer>>(new ModelAnswer(text, exitCode));
        }
    }
}
