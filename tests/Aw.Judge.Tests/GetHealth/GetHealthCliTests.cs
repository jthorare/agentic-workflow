using Aw.Judge.Core.Decisions;

namespace Aw.Judge.Tests.GetHealth;

// The frozen surface (TS cli.ts runHealth), on a real decisions.sqlite.
public sealed class GetHealthCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Health_prints_ok_and_no_failures_on_a_new_state_directory()
    {
        var run = await new JudgeCli(_state).Run("health");

        AssertHealth(run, "ok", 0);
        CliGolden.Match(run, "GetHealth/Golden/health-ok.json");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Health_prints_degraded_with_the_failure_count()
    {
        var cli = new JudgeCli(_state);
        var log = _state.DecisionLog();
        _ = await log.RecordFailure(new FailureRecord(cli.Clock.GetUtcNow().AddHours(-1), "wake-gate", "claude-cli", "timeout"), CancellationToken.None);

        var run = await cli.Run("health");

        AssertHealth(run, "degraded", 1);
        CliGolden.Match(run, "GetHealth/Golden/health-degraded.json");
    }

    private static void AssertHealth(CliRun run, string status, int failures)
    {
        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        var json = run.Json();
        Assert.Equal(status, json.GetProperty("status").GetString());
        Assert.Equal(failures, json.GetProperty("failures24h").GetInt32());
    }

    public void Dispose() => _state.Dispose();
}
