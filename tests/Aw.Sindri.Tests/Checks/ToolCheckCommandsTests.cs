using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Infrastructure.Checks;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Checks;

/// <summary>What the tool adapter adds to a run: the working directory, the program split from its arguments, scrubbed and bounded output, and the time it took.</summary>
[Lane(TestLane.Unit)]
public sealed class ToolCheckCommandsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_program_runs_in_the_worktree_with_the_rest_of_the_argv_as_its_arguments()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, string.Empty));
        var commands = new ToolCheckCommands(runner, new Mask("x"), new FakeTimeProvider());

        _ = await commands.Run(new CheckInvocation("unit", ["dotnet", "test", "--filter", "A B"], TimeSpan.FromMinutes(5)), "/work/p1", Token);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(("dotnet", TimeSpan.FromMinutes(5)), (call.FileName, call.Timeout));
        Assert.Equal(["test", "--filter", "A B"], call.Arguments);
        Assert.Equal("/work/p1", Assert.Single(runner.WorkingDirectories));
    }

    [Fact]
    public async Task What_it_printed_is_scrubbed_before_it_is_cut_so_a_secret_cut_in_half_cannot_survive()
    {
        var secret = "ghp_" + new string('s', 30);
        var output = new string('a', 100) + secret + new string('b', CheckRun.TailLimit - 20);   // the cut falls inside the secret
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 1, output));
        var commands = new ToolCheckCommands(runner, new Mask(secret), new FakeTimeProvider());

        var run = await commands.Run(new CheckInvocation("unit", ["t"], TimeSpan.FromMinutes(1)), "/w", Token);

        Assert.DoesNotContain("sssss", run.Value.Tail, StringComparison.Ordinal);
        Assert.Contains("[secret]", run.Value.Tail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Standard_error_follows_standard_output_in_the_tail()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 2, "out\n", "err\n"));
        var commands = new ToolCheckCommands(runner, new Mask("x"), new FakeTimeProvider());

        var run = await commands.Run(new CheckInvocation("unit", ["t"], TimeSpan.FromMinutes(1)), "/w", Token);

        Assert.Equal("out\nerr\n", run.Value.Tail);
    }

    [Fact]
    public async Task The_duration_is_the_time_the_run_took()
    {
        var clock = new FakeTimeProvider();
        var runner = new FakeToolRunner((_, _) =>
        {
            clock.Advance(TimeSpan.FromSeconds(42));
            return new ToolRun(ToolOutcome.Exited, 0, string.Empty);
        });
        var commands = new ToolCheckCommands(runner, new Mask("x"), clock);

        var run = await commands.Run(new CheckInvocation("unit", ["t"], TimeSpan.FromMinutes(1)), "/w", Token);

        Assert.Equal(TimeSpan.FromSeconds(42), run.Value.Duration);
    }

    [Fact]
    public async Task An_empty_argv_is_refused_without_running_anything()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, string.Empty));
        var commands = new ToolCheckCommands(runner, new Mask("x"), new FakeTimeProvider());

        var run = await commands.Run(new CheckInvocation("unit", [], TimeSpan.FromMinutes(1)), "/w", Token);

        Assert.True(run.IsError);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task A_child_command_is_told_the_caller_holds_a_heavy_slot()
    {
        var tools = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, "ok"));

        _ = await new ToolCheckCommands(tools, new Testing.Checks.IdentityScrubber(), new FakeTimeProvider()).Run(new CheckInvocation("c", ["c"], TimeSpan.FromSeconds(1)), "/work", Token);

        Assert.Equal("1", tools.Calls.Single().Environment!["AW_HEAVY_LOCK_HELD"]);
    }

    [Fact]
    public async Task A_timed_out_run_keeps_the_tail_of_what_it_printed_scrubbed()
    {
        var tools = new FakeToolRunner(new ToolRun(ToolOutcome.TimedOut, -1, "building...\nhunter2 still running", "warn"));

        var run = await new ToolCheckCommands(tools, new Mask("hunter2"), new FakeTimeProvider()).Run(new CheckInvocation("c", ["c"], TimeSpan.FromSeconds(1)), "/work", Token);

        Assert.True(run.Value.TimedOut);
        Assert.Contains("still running", run.Value.Tail, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", run.Value.Tail, StringComparison.Ordinal);
    }

    private sealed class Mask(string secret) : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(secret, "[secret]", StringComparison.Ordinal);
    }
}
