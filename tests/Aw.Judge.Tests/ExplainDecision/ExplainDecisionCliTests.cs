using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.ExplainDecision;

// The contract of 'judge why' (TS runWhy: the decision row with snake_case fields; unknown → exit 1).
public sealed class ExplainDecisionCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Why_prints_the_decision_row_with_snake_case_fields()
    {
        _ = await _state.DecisionLog().Record(A.Decision().WithId("d-1").Build(), CancellationToken.None);

        var run = await new JudgeCli(_state).Run("why", "d-1");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        DecisionJson.AssertRow(run.Json(), "d-1", "model", undone: false);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Why_prints_a_question_s_decision_row_with_its_skipped_list()
    {
        // An unsure model, then wake-gate's fallback rule settles: the row names the rule and the skipped model.
        var unsure = new ScriptedProvider(ProviderName.Jev, ContentClass.MessageMeta).Returns(
            new ProviderDecided("send", 0.4, "model") { Probabilities = new Dictionary<string, double>(StringComparer.Ordinal) { ["send"] = 0.4, ["batch"] = 0.35, ["drop"] = 0.25 } });
        var cli = new JudgeCli(_state) { Providers = [unsure] };
        var asked = await cli.RunWithInput("""{"text":"progress on the parser, half done","senderKind":"teammate"}""", "wake-gate");
        Assert.Equal(0, asked.ExitCode);

        var run = await cli.Run("why", asked.Json().GetProperty("id").GetString()!);   // why: the envelope's id is a string

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        CliGolden.Match(run, "ExplainDecision/Golden/why.json");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Why_exits_1_for_an_unknown_id()
    {
        var cli = new JudgeCli(_state);

        Assert.Equal(new CliRun(1, "", "unknown decision: nope\n"), await cli.Run("why", "nope"));
    }

    public void Dispose() => _state.Dispose();
}
