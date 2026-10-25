using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Setup;
using static Aw.Sindri.Ui.Tests.Setup.SetupFixture;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The workflows row when approvals are older than what ships: each outdated workflow says which version is approved and which is available, the row needs the person even while the server still counts it approved, and the one approve-all flow re-approves them.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupOutdatedWorkflowsTests
{
    private readonly SetupFixture _f = new("extra-setup-ready");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<SetupStepViewModel> RecipesRow()
    {
        _f.Server.RecipeListAnswer = FakeSindriApi.OutdatedRecipeList();
        _f.Serve(With("profile-needs-approval", "recipes", "done"));   // the server still counts them all approved
        return (await _f.Read(Token)).Steps.Single(s => s.Id == "recipes");
    }

    [Fact]
    public async Task Settings_shows_an_outdated_approval_with_the_available_version()
    {
        var row = await RecipesRow();

        Assert.Equal(["approved", "approved v4, v5 available", "Out of date"], row.Recipes.Lines.Select(r => r.Status), StringComparer.Ordinal);
        Assert.Equal(["Plain step", "Review", "Triage"], row.Recipes.Lines.Select(r => r.Name), StringComparer.Ordinal);
    }

    [Fact]
    public void A_workflow_listed_twice_is_one_line_that_says_a_new_version_waits()
    {
        var approved = new Ui.Api.Workflow.RecipeSummaryView("workflow.step", 1, "sha256:aa", "approved", Name: null);
        var waiting = new Ui.Api.Workflow.RecipeSummaryView("workflow.step", 2, "sha256:bb", "unapproved", Name: null, GainsCapability: true);

        var list = new SetupRecipeListViewModel([approved, waiting]);

        var line = Assert.Single(list.Lines);
        Assert.Equal((true, 1, Labels.RecipeGainsCapability), (line.IsOutdated, list.OutdatedCount, line.Status));
    }

    [Fact]
    public async Task Current_workflows_read_approved_and_a_list_with_none_outdated_changes_nothing()
    {
        _f.Serve(With("profile-needs-approval", "recipes", "done"));

        var row = (await _f.Read(Token)).Steps.Single(s => s.Id == "recipes");

        Assert.Equal(["approved"], row.Recipes.Lines.Select(r => r.Status), StringComparer.Ordinal);
        Assert.True(row.IsDone);
        Assert.False(row.Workflows!.IsOpen);
    }

    [Fact]
    public async Task An_outdated_workflow_makes_the_row_need_you_even_when_the_server_says_done()
    {
        var row = await RecipesRow();

        Assert.Equal((true, false, "Needs you"), (row.NeedsYou, row.IsDone, row.ChipWord));
        Assert.Equal("2 workflows have a newer version waiting for your approval.", row.Sub);
        Assert.Equal(("Re-approve all updated workflows", true), (row.Recipes.Heading, row.Workflows!.IsOpen));
    }

    [Fact]
    public async Task Reapprove_all_runs_the_approve_all_flow_children_first()
    {
        var row = await RecipesRow();
        var all = row.Workflows!;

        Assert.NotEmpty(all.Rows);   // the plan lists the versions it covers, children first
        var submitted = all.Approve();
        while (!submitted.IsCompleted)
        {
            _f.Clock.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(1, Token);
        }

        await submitted;

        _ = Assert.Single(_f.Server.Approvals);
    }

    [Fact]
    public async Task The_approved_older_golden_reads_the_newer_version_and_keeps_a_never_approved_workflow_as_it_is()
    {
        _f.Server.RecipeListAnswer = RouteAnswer.Api("workflow-recipes", "approved-older");
        _f.Serve(With("profile-needs-approval", "recipes", "done"));

        var row = (await _f.Read(Token)).Steps.Single(s => s.Id == "recipes");

        Assert.Equal(["Not approved", "approved v5, v6 available"], row.Recipes.Lines.Select(l => l.Status), StringComparer.Ordinal);
        Assert.Equal(("Needs you", true), (row.ChipWord, row.Workflows!.IsOpen));
    }
}
