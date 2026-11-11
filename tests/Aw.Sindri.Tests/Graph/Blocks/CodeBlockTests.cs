using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>The 'code' block over a fake runner (agent-first PR 9c): config in, the run it asks for, the result out.</summary>
[Lane(TestLane.Unit)]
public sealed class CodeBlockTests
{
    private const string ScoreFilter = "return input!.AsArray().Where(i => i![\"score\"]!.GetValue<int>() > 100).Select(i => i![\"title\"]!.GetValue<string>()).ToArray();";

    private static Task<Result<BlockResult>> Run(CodeBlock block, JsonObject config) => block.Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken).AsTask();

    [Fact]
    public async Task The_score_filter_example_returns_the_titles_over_100()
    {
        var runner = new FakeRunner("""["b","c"]""");
        var items = new JsonArray(new JsonObject { ["title"] = "a", ["score"] = 50 }, new JsonObject { ["title"] = "b", ["score"] = 150 }, new JsonObject { ["title"] = "c", ["score"] = 101 });

        var result = await Run(new CodeBlock(runner), new JsonObject { ["input"] = items, ["code"] = ScoreFilter });

        Assert.Equal("""["b","c"]""", result.Value.Outputs["result"]!.ToJsonString());
        var run = Assert.Single(runner.Runs);
        Assert.Equal(ScoreFilter, run.Code);
        Assert.Equal(items.ToJsonString(), run.InputJson);
        Assert.Equal((TimeSpan.FromSeconds(5), 256), (run.Timeout, run.MemoryMb));   // the defaults
    }

    [Fact]
    public async Task Timeout_and_memory_are_read_and_a_timeout_past_sixty_seconds_is_refused()
    {
        var runner = new FakeRunner("1");
        var block = new CodeBlock(runner);

        var ok = await Run(block, new JsonObject { ["code"] = "return 1;", ["timeout"] = "30s", ["memoryMb"] = 64 });
        var tooLong = await Run(block, new JsonObject { ["code"] = "return 1;", ["timeout"] = "2m" });

        Assert.True(ok.IsSuccess);
        Assert.Equal((TimeSpan.FromSeconds(30), 64), (runner.Runs[0].Timeout, runner.Runs[0].MemoryMb));
        Assert.Equal(SindriErrors.GraphBlockFailed.Code, tooLong.Error.Code);
        Assert.Contains("60", tooLong.Error.Message, StringComparison.Ordinal);
        _ = Assert.Single(runner.Runs);   // the refused run never started
    }

    [Fact]
    public async Task A_failure_of_the_runner_fails_the_node_with_its_code()
    {
        var runner = new FakeRunner(SindriErrors.CodeTimeout.With("the code ran past its limit of 1 s"));

        var result = await Run(new CodeBlock(runner), new JsonObject { ["code"] = "while (true) { }" });

        Assert.Equal("SND-CODE-002", result.Error.Code);
    }

    private sealed class FakeRunner : ICodeRunPort
    {
        private readonly Result<string> _answer;

        public FakeRunner(string json)
        {
            _answer = json;
        }

        public FakeRunner(Error error)
        {
            _answer = error;
        }

        public List<CodeRun> Runs { get; } = [];

        public ValueTask<Result<string>> Run(CodeRun run, CancellationToken cancellationToken)
        {
            Runs.Add(run);
            return ValueTask.FromResult(_answer);
        }
    }
}
