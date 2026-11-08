using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Setup;
using static Aw.Sindri.Ui.Tests.Setup.SetupFixture;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The workflows row (setup spec §8.3; q93): the plan with its Approve button, one plain click. The profile row has no approval any more: edits apply on save.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupConfirmTests
{
    private readonly SetupFixture _f = new("extra-setup-ready");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SetupStepViewModel Step(SetupViewModel setup, string id) => setup.Steps.Single(s => s.Id == id);

    [Fact]
    public async Task Workflows_row_shows_the_plan_with_its_change_words_and_the_approve_button()
    {
        _f.Serve(With("profile-needs-approval", "recipes", "needs-you", "ApproveAllRecipes"));

        var row = Step(await _f.Read(Token), "recipes");
        var all = row.Workflows!;

        Assert.Null(row.Action);
        Assert.Equal((true, true, true), (all.IsOpen, all.ShowsPlan, all.CanApprove));
        Assert.NotEmpty(all.Rows);
        Assert.All(all.Rows, r => Assert.NotEmpty(r.Reason));   // New, Updated or Needs a second look, as the plan serves it
        Assert.Empty(_f.Server.Approvals);   // reading the plan approves nothing
    }

    [Fact]
    public async Task A_workflows_row_that_stops_needing_the_person_closes_its_plan()
    {
        _f.Serve(With("profile-needs-approval", "recipes", "needs-you", "ApproveAllRecipes"));
        var setup = await _f.Read(Token);

        _f.Serve(With("profile-needs-approval", "recipes", "todo"));
        await setup.Refresh(Url, Token);

        Assert.False(Step(setup, "recipes").Workflows!.IsOpen);
    }
}
