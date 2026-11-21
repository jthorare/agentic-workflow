using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Lanes;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.HomeLanes;

/// <summary>The aw-meta reader: three read-only commands through the process port, each with a timeout, and one error for every way aw-meta can fail to answer.</summary>
[Lane(TestLane.Unit)]
public sealed class AwMetaLaneSourceTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_member_runs_exactly_its_one_read_only_command_with_the_timeout()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, "{}"));
        var source = new AwMetaLaneSource(runner, _timeout);

        _ = await source.Status(Token);
        _ = await source.Context(Token);
        _ = await source.Usage(Token);

        Assert.Equal(
            [("aw-meta", "status --json"), ("aw-meta", "lanes-context --json"), ("aw-meta", "usage --json")],
            runner.Calls.Select(static c => (c.FileName, string.Join(' ', c.Arguments))));
        Assert.All(runner.Calls, static c => Assert.Equal(_timeout, c.Timeout));
    }

    [Fact]
    public async Task Exit_0_and_exit_1_both_return_the_printed_json_because_exit_1_means_something_needs_attention()
    {
        var zero = await new AwMetaLaneSource(new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, LaneFixtures.ContextOk)), _timeout).Context(Token);
        var one = await new AwMetaLaneSource(new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 1, LaneFixtures.ContextRecycle)), _timeout).Context(Token);

        Assert.Equal(LaneFixtures.ContextOk, zero.Value);
        Assert.Equal(LaneFixtures.ContextRecycle, one.Value);
    }

    [Fact]
    public async Task A_missing_binary_a_timeout_a_cancel_exit_2_and_empty_output_are_each_SND_LANES_001()
    {
        ToolRun[] failures =
        [
            ToolRun.Missing,
            ToolRun.TimedOut,
            ToolRun.Cancelled,
            new ToolRun(ToolOutcome.Exited, 2, "usage: aw-meta ..."),
            new ToolRun(ToolOutcome.Exited, 0, "   "),
        ];

        foreach (var failure in failures)
        {
            var read = await new AwMetaLaneSource(new FakeToolRunner(failure), _timeout).Status(Token);

            Assert.True(read.IsError, failure.ToString());
            Assert.Equal(SindriErrors.LanesUnavailable.Code, read.Error.Code);
        }
    }
}
