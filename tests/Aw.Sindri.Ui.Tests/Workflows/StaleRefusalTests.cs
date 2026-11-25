using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3999: a refusal belongs to the one save that caused it. A step sheet (a new step, or Step settings) opens with no refusal text, whether the refusal came from an earlier
/// sheet's Save or from a graph edit (Remove) refused while the sheet was shut; Close leaves no refusal text on the page. The workflow served is the "found" golden: steps "start" and "agent".
/// </summary>
[Lane(TestLane.Unit)]
public sealed class StaleRefusalTests
{
    private const string Refusal = "GRAPH-REF-003 node 'code_1' field 'config.input': 'triage.items' names node 'triage'";

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer Refused => RouteAnswer.Problem(HttpStatusCode.UnprocessableEntity, "SND-GRAPH-002", Refusal);

    private async Task<WorkflowsPageViewModel> Opened()
    {
        _f.Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        return page;
    }

    private static async Task AddStep(WorkflowsPageViewModel page, string type)
    {
        await page.GraphEdit.BeginAdd(Token);
        var block = page.GraphEdit.Groups.SelectMany(static g => g.Blocks).Single(b => string.Equals(b.Type, type, StringComparison.Ordinal));
        await page.GraphEdit.AddStep(block, Token);
    }

    // Remove on "agent", refused by Sindri while the sheet is shut: the refusal shows on the page line.
    private async Task<WorkflowsPageViewModel> RefusedRemove()
    {
        var page = await Opened();
        Assert.True(page.Runs.Graph!.SelectNode("agent"));
        _f.Server.ApplyWorkflowOpsAnswer = Refused;
        page.GraphEdit.RequestRemove();
        await page.GraphEdit.ConfirmRemove(Token);
        Assert.Contains("GRAPH-REF-003", page.GraphEdit.FailureText, StringComparison.Ordinal);
        return page;
    }

    // A new step refused by Sindri on Save: the sheet stays open with the refusal.
    private async Task<WorkflowsPageViewModel> RefusedNewStep()
    {
        var page = await Opened();
        await AddStep(page, "code");
        page.Editor.Fields.Single(static f => string.Equals(f.Name, "code", StringComparison.Ordinal)).Text = "return 1;";   // the required field, so Save reaches Sindri (#4272: nothing is filled in)
        _f.Server.ApplyWorkflowOpsAnswer = Refused;
        await page.Save(Token);
        Assert.Contains("GRAPH-REF-003", page.Editor.FailureText, StringComparison.Ordinal);
        return page;
    }

    // The sheet's failure line each time the sheet's form changes (IsNewStep is raised on every open), before any read of the open ends.
    private static List<string> LineOnOpen(WorkflowsPageViewModel page)
    {
        var seen = new List<string>();
        page.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(WorkflowsPageViewModel.IsNewStep), StringComparison.Ordinal))
            {
                seen.Add(page.Editor.FailureText);
            }
        };

        return seen;
    }

    [Fact]
    public async Task A_new_step_sheet_opens_without_the_refusal_of_an_earlier_graph_edit()
    {
        var page = await RefusedRemove();
        var seen = LineOnOpen(page);

        await AddStep(page, "http");

        Assert.True(page.IsNewStep);
        Assert.Equal(string.Empty, Assert.Single(seen));
        Assert.Equal(string.Empty, page.Editor.FailureText);   // the sheet's line
        Assert.False(page.GraphEdit.HasFailure);   // the page's line
    }

    [Fact]
    public async Task Step_settings_opens_without_the_refusal_of_an_earlier_graph_edit()
    {
        var page = await RefusedRemove();
        var seen = LineOnOpen(page);

        await page.OpenSettings(Token);

        Assert.True(page.IsSettingsOpen);
        Assert.Equal(string.Empty, Assert.Single(seen));   // not shown even while the sheet reads the workflow
        Assert.Equal(string.Empty, page.Editor.FailureText);
    }

    [Fact]
    public async Task Step_settings_straight_from_a_refused_new_step_opens_without_its_refusal()
    {
        var page = await RefusedNewStep();
        var seen = LineOnOpen(page);

        await page.OpenSettings(Token);

        Assert.False(page.IsNewStep);
        Assert.Equal(string.Empty, Assert.Single(seen));
        Assert.Equal(string.Empty, page.Editor.FailureText);
    }

    [Fact]
    public async Task A_refused_new_step_then_Close_leaves_no_refusal_and_the_next_new_step_opens_clean()
    {
        var page = await RefusedNewStep();

        page.CloseSettings();

        Assert.False(page.IsSettingsOpen);
        Assert.Equal(string.Empty, page.GraphEdit.FailureText);   // nothing under Add step / Connect to / Remove
        Assert.False(page.GraphEdit.HasFailure);

        await AddStep(page, "http");

        Assert.True(page.IsNewStep);
        Assert.Equal(string.Empty, page.Editor.FailureText);
    }
}
