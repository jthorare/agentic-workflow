using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflows;
using Aw.Sindri.Ui.ViewModels;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3805: the run page's header shows the tokens the run used, against its budget when it has one; a run that used none and has no budget shows nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class RunDetailTokensTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_run_with_a_budget_shows_used_over_budget()
    {
        var served = Golden("workflow-runs-run", "finished");
        served["tokens"] = new JsonObject { ["used"] = 1200, ["budget"] = 5000 };
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        Assert.Equal(("1,200 / 5,000 tokens", true), (run.TokensText, run.HasTokens));
    }

    [Fact]
    public async Task A_run_with_no_budget_shows_what_it_used()
    {
        var served = Golden("workflow-runs-run", "finished");
        served["tokens"] = new JsonObject { ["used"] = 1200, ["budget"] = null };
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        Assert.Equal("1,200 tokens", run.TokensText);
    }

    [Fact]
    public async Task A_run_that_used_none_and_has_no_budget_shows_nothing()
    {
        var run = await _f.OpenedRun(Token);

        Assert.Equal((string.Empty, false), (run.TokensText, run.HasTokens));
    }

    [Theory]
    [InlineData(0L, 100L, "0 / 100 tokens")]
    [InlineData(150L, 100L, "150 / 100 tokens")]
    [InlineData(7L, null, "7 tokens")]
    public void The_label_says_used_and_budget(long used, long? budget, string expected) =>
        Assert.Equal(expected, Labels.WfRunTokens(new WorkflowRunTokens(used, budget)));

    [Fact]
    public void A_sindri_that_serves_no_tokens_shows_nothing() => Assert.Equal(string.Empty, Labels.WfRunTokens(tokens: null));
}
