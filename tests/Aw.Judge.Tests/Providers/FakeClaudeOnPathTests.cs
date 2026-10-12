using System.Diagnostics;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Processes;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Evaluation;
using Aw.Judge.Tests.Processes;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>The agent-CLI providers over the real process runner, with fake `claude` and `codex` scripts on a temp PATH.</summary>
[Lane(TestLane.Integration)]
public sealed class FakeClaudeOnPathTests : IDisposable
{
    private readonly ShellPrograms _bin = new();

    public void Dispose() => _bin.Dispose();

    [Fact]
    public async Task An_in_enum_answer_is_decided()
    {
        Claude(new FakeProgram { StandardOutput = Questions.ClaudeStructured("""{"decision":"send"}""") });

        Assert.Equal(new ProviderDecided("send", 1, "model"), await Decide(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task An_out_of_enum_answer_is_errored()
    {
        Claude(new FakeProgram { StandardOutput = Questions.ClaudeStructured("""{"decision":"wake-everyone"}""") });

        Assert.Equal(new ProviderErrored("out-of-enum"), await Decide(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task Garbage_on_stdout_is_errored()
    {
        Claude(new FakeProgram { StandardOutput = "\u001b[31mSomething went wrong\u001b[0m" });

        Assert.Equal(new ProviderErrored("unparseable-output"), await Decide(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task The_child_gets_the_recursion_guard_and_a_closed_stdin()
    {
        _ = _bin.InstallScript(
            "claude",
            "cat > /dev/null\n"
            + """printf '{"type":"result","structured_output":{"decision":"%s"}}' "$( [ "$AW_JUDGE_CHILD" = 1 ] && echo send || echo drop )" """
            + "\n");

        Assert.Equal(new ProviderDecided("send", 1, "model"), await Decide(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task A_cli_that_sleeps_past_the_budget_is_killed_near_it_and_reported_as_a_timeout()
    {
        Claude(new FakeProgram { Runs = TimeSpan.FromSeconds(30), StandardOutput = Questions.ClaudeStructured("""{"decision":"send"}""") });
        var clock = Stopwatch.StartNew();

        var result = await Decide(TimeSpan.FromMilliseconds(500));

        Assert.Equal(new ProviderUnavailable(ProviderUnavailable.Timeout), result);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(4), $"killed near the 500 ms budget, took {clock.Elapsed}");
    }

    [Fact]
    public async Task A_hung_cli_is_a_timeout_failure_and_the_next_provider_decides_within_the_budgets()
    {
        Claude(new FakeProgram { Runs = TimeSpan.FromSeconds(30) });
        _ = _bin.Install("codex", new FakeProgram { StandardOutput = """{"type":"item.completed","item":{"type":"agent_message","text":"{\"decision\":\"batch\"}"}}""" });
        var runner = new SystemProcessRunner(_bin.Environment);
        var harness = new EvaluatorHarness();
        var question = new QuestionBuilder().Build() with { TimeBudget = TimeSpan.FromSeconds(3) };
        var clock = Stopwatch.StartNew();

        var outcome = await harness.Run(
            question,
            EvaluatorHarness.Text("hello"),
            new EvaluationContext
            {
                Providers = [new ClaudeCliProvider(runner), new CodexCliProvider(runner)],
                Chain = EvaluatorHarness.Chain(ProviderName.ClaudeCli, ProviderName.CodexCli),
            });

        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal(("batch", ProviderName.CodexCli), (decided.Decision, decided.Model));
        Assert.Equal(new FailureRecord(harness.Clock.GetUtcNow(), "wake-gate", "claude-cli", "timeout"), Assert.Single(harness.Log.Failures));
        Assert.Equal([new SkippedProvider("claude-cli", SkipReason.Timeout)], harness.Row.Skipped);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(6), $"the command stays within budget × providers (2 × 3 s), took {clock.Elapsed}");
    }

    [Fact]
    public async Task A_prompt_far_past_the_argument_limit_still_reaches_the_cli()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the fake CLIs are sh scripts");
        _ = _bin.InstallScript("claude", "cat > /dev/null\n" + """printf '{"type":"result","structured_output":{"decision":"send"}}'""" + "\n");
        var provider = new ClaudeCliProvider(new SystemProcessRunner(_bin.Environment));
        var huge = Questions.Text with { Prompt = new string('d', 2 << 20) };

        var result = await provider.Decide(huge, new JsonObject { ["diff"] = new string('d', 2 << 20) }, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(new ProviderDecided("send", 1, "model"), result);
    }

    [Fact]
    public async Task A_cli_missing_from_the_path_is_not_installed()
    {
        var result = await Decide(TimeSpan.FromSeconds(10));

        Assert.Equal(new ProviderUnavailable("not-installed"), result);
    }

    private void Claude(FakeProgram program) => _ = _bin.Install("claude", program);

    private async Task<ProviderResult> Decide(TimeSpan budget)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the fake CLIs are sh scripts");
        var provider = new ClaudeCliProvider(new SystemProcessRunner(_bin.Environment));
        return await provider.Decide(Questions.Text, new JsonObject { ["text"] = "hello" }, budget, TestContext.Current.CancellationToken);
    }
}
