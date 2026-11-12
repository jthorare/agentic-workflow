using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The Not tested / Tested / Active chips (spec 4.8) come from the Mode of the runs: a passing test run of the newest revision means Tested.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowModeChipTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Run(string id, int revision, string status, string? mode) =>
        $$"""{"id":"{{id}}","revision":{{revision.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"status":"{{status}}","trigger":"manual","startedAt":"2026-01-01T00:00:00Z","endedAt":"2026-01-01T00:01:00Z","error":null{{(mode is null ? string.Empty : $",\"mode\":\"{mode}\"")}}}""";

    private async Task<ViewModels.Workflows.WorkflowsPageViewModel> Open(bool active, params string[] runs)
    {
        var workflow = Golden("workflows-slug", "found");   // revision 2 is the newest; revision 1 is the active one
        workflow["activeRevision"] = active ? 2 : null;
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, workflow.ToJsonString());
        _f.Server.WorkflowRunsAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, $$"""{"slug":"{{Slug}}","runs":[{{string.Join(',', runs)}}],"more":false}""");
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        return page;
    }

    [Fact]
    public async Task A_workflow_with_only_live_runs_is_Not_tested()
    {
        var page = await Open(active: false, Run("a@2:1", 2, "succeeded", mode: null), Run("a@2:2", 2, "succeeded", "live"));

        Assert.Equal("Not tested", page.Runs.StateChipText);
    }

    [Fact]
    public async Task A_passing_test_run_of_the_newest_revision_makes_it_Tested()
    {
        var page = await Open(active: false, Run("a@2:1", 2, "succeeded", "test"));

        Assert.Equal("Tested", page.Runs.StateChipText);
    }

    [Theory]
    [InlineData(1, "succeeded")]
    [InlineData(2, "failed")]
    [InlineData(2, "cancelled")]
    public async Task A_test_run_of_an_older_revision_or_one_that_did_not_pass_does_not_count(int revision, string status)
    {
        var page = await Open(active: false, Run("a@x:1", revision, status, "test"));

        Assert.Equal("Not tested", page.Runs.StateChipText);
    }

    [Fact]
    public async Task A_workflow_that_is_on_is_Active_whatever_its_runs_say()
    {
        var page = await Open(active: true, Run("a@2:1", 2, "succeeded", "test"));

        Assert.Equal("Active", page.Runs.StateChipText);
    }

    [Fact]
    public async Task A_test_run_row_carries_the_Test_chip_and_a_live_row_does_not()
    {
        var page = await Open(active: false, Run("a@2:2", 2, "succeeded", "test"), Run("a@2:1", 2, "succeeded", "live"));

        Assert.Equal([("Test", true), (string.Empty, false)], page.Runs.Rows.Select(r => (r.ModeText, r.IsTest)));
    }
}
