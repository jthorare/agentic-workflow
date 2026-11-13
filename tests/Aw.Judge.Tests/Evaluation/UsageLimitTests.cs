using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Evaluation.EvaluatorHarness;

namespace Aw.Judge.Tests.Evaluation;

[Lane(TestLane.Unit)]
public sealed class UsageLimitTests
{
    [Fact]
    public async Task A_usage_limited_provider_is_skipped_and_failed_as_usage_limit()
    {
        var harness = new EvaluatorHarness();
        var cli = new ScriptedProvider(ProviderName.ClaudeCli).Returns(new ProviderErrored("exit-1") { UsageLimit = true, LimitPattern = "hit-your-limit" });

        _ = await harness.Run(A.Question().Build(), Text("hmm"), cli);

        Assert.Equal([new SkippedProvider("claude-cli", SkipReason.Failed) { Code = "usage-limit" }], harness.Row.Skipped);
        Assert.Equal([("wake-gate", "claude-cli", "usage-limit")], harness.Log.Failures.Select(f => (f.Question, f.Provider, f.ReasonCode)));
    }

    [Fact]
    public async Task A_plain_errored_provider_keeps_its_exit_code_and_no_skip_code()
    {
        var harness = new EvaluatorHarness();
        var cli = new ScriptedProvider(ProviderName.ClaudeCli).Returns(new ProviderErrored("exit-1"));

        _ = await harness.Run(A.Question().Build(), Text("hmm"), cli);

        Assert.Equal([new SkippedProvider("claude-cli", SkipReason.Failed)], harness.Row.Skipped);
        Assert.Null(harness.Row.Skipped.Single().Code);
        Assert.Equal([("wake-gate", "claude-cli", "exit-1")], harness.Log.Failures.Select(f => (f.Question, f.Provider, f.ReasonCode)));
    }
}
