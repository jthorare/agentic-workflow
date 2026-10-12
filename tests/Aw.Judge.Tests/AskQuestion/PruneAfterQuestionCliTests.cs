using Aw.Judge.Core.Ids;

namespace Aw.Judge.Tests.AskQuestion;

// Retention: after each question the judge drops the detail rows older than 30 days (best effort; the question's
// output and exit code never depend on it). Only questions write details, so only they prune; no other command, and
// no command line the judge can't read, opens the database for it.
public sealed class PruneAfterQuestionCliTests : IDisposable
{
    private const string Ack = """{"text":"ok","senderKind":"teammate"}""";

    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task A_question_prunes_the_details_older_than_30_days()
    {
        var cli = new JudgeCli(_state);
        var old = Id(await cli.RunWithInput(Ack, "wake-gate"));
        cli.Clock.Advance(TimeSpan.FromDays(31));

        var recent = Id(await cli.RunWithInput(Ack, "wake-gate"));

        var log = _state.DecisionLog();
        var ct = TestContext.Current.CancellationToken;
        Assert.Null((await log.Find(old, ct)).Value.Details);
        Assert.NotNull((await log.Find(recent, ct)).Value.Details);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_config_command_or_a_usage_error_never_creates_the_database()
    {
        var cli = new JudgeCli(_state);

        Assert.Equal(0, (await cli.Run("config", "get")).ExitCode);
        Assert.Equal(0, (await cli.Run("config", "set", "wake-gate", "threshold", "0.8")).ExitCode);
        Assert.Equal(64, (await cli.RunWithInput("{}", "no-such-question")).ExitCode);

        Assert.False(File.Exists(Path.Combine(_state.Path, "judge", "decisions.sqlite")));
    }

    // why: a decided envelope always has an id
    private static DecisionId Id(CliRun run) => DecisionId.From(run.Json().GetProperty("id").GetString()!);

    public void Dispose() => _state.Dispose();
}
