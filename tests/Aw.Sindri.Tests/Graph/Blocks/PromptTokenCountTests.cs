using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>#3805: the prompt block adds the tokens of every model call it makes to its context, the repair ask too, so the run's token budget counts them.</summary>
[Lane(TestLane.Unit)]
public sealed class PromptTokenCountTests
{
    private const string Schema = """{"type":"object","required":["flagged"],"properties":{"flagged":{"type":"boolean"}}}""";

    private static ValueTask<Result<BlockResult>> Run(TokenModel model, BlockContext context) => new PromptBlock(model).Execute(context, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_plain_prompt_counts_the_tokens_of_its_one_call()
    {
        var context = BlockFakes.Context(new JsonObject { ["prompt"] = "Say hi" });

        var result = await Run(new TokenModel(tokens: 42, "hi"), context);

        Assert.True(result.IsSuccess);
        Assert.Equal(42L, context.Tokens);
    }

    [Fact]
    public async Task A_repair_ask_adds_its_tokens_to_the_first_one()
    {
        var context = BlockFakes.Context(new JsonObject { ["prompt"] = "Check the day.", ["output"] = JsonNode.Parse(Schema) });

        var result = await Run(new TokenModel(tokens: 30, "Sure, it looks fine.", """{"flagged":false}"""), context);

        Assert.True(result.IsSuccess);
        Assert.Equal(60L, context.Tokens);
    }

    [Fact]
    public async Task A_failed_answer_still_counts_its_tokens()
    {
        var context = BlockFakes.Context(new JsonObject { ["prompt"] = "Check the day.", ["output"] = JsonNode.Parse(Schema) });

        var result = await Run(new TokenModel(tokens: 25, "no", "still no"), context);

        Assert.True(result.IsError);
        Assert.Equal(50L, context.Tokens);
    }

    [Fact]
    public void A_negative_count_adds_nothing()
    {
        var context = BlockFakes.Context(new JsonObject { ["prompt"] = "Say hi" });

        context.CountTokens(-5);
        context.CountTokens(7);

        Assert.Equal(7L, context.Tokens);
    }

    private sealed class TokenModel(long tokens, params string[] answers) : IGraphModelPort
    {
        private int _asked;

        public string Untrusted(string source, string text) => text;

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken) =>
            throw new NotSupportedException("the block must ask through Answer to see the tokens");

        ValueTask<Result<ModelAnswer>> IGraphModelPort.Answer(string prompt, CancellationToken cancellationToken)
        {
            var text = answers[Math.Min(_asked, answers.Length - 1)];
            _asked++;
            return ValueTask.FromResult<Result<ModelAnswer>>(new ModelAnswer(text) { Tokens = tokens });
        }
    }
}
