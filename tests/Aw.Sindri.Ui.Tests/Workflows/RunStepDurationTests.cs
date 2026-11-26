using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3683 (found in the shots of #4395): a step card on the run page says how long the step ran, from the served node seconds. Under a second it reads "&lt;1 s", from a minute "1m 15s",
/// and a step with no times yet (running, skipped, a collect's own row) shows nothing.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RunStepDurationTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(0.24, "<1 s")]
    [InlineData(0.0, "<1 s")]
    [InlineData(7.0, "7 s")]
    [InlineData(75.0, "1m 15s")]
    [InlineData(null, "")]
    public async Task A_step_card_shows_how_long_the_step_ran(double? seconds, string words)
    {
        var served = Golden("workflow-runs-run", "finished");
        served["nodes"]!.AsArray().Single(n => string.Equals((string?)n!["node"], "agent", StringComparison.Ordinal))!["seconds"] = seconds is { } s ? JsonValue.Create(s) : null;
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        var card = run.Groups.Single(g => string.Equals(g.Node, "agent", StringComparison.Ordinal)).Runs[0];
        Assert.Equal(words, card.DurationText);
    }
}
