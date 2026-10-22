using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Recipes;

namespace Aw.Sindri.Ui.Tests.Recipes;

/// <summary>The recipe sheet's standing (recipes spec §2, §7): a callout per standing, the reasons a workflow is out of date, the terminal copy that approves it, the no-outputs line.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipeSheetLayoutTests
{
    private const string Id = "workflow.dev-triage-three-state";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly FakeClipboard _clipboard = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<RecipeSheetViewModel> Opened(string standing, string stale = "")
    {
        _server.RecipeListAnswer = new RouteAnswer(HttpStatusCode.OK, "{\"recipes\":[" + RecipesPageFixture.Row(Id, standing, stale) + "]}");
        var sheet = new RecipeSheetViewModel(_server, _clipboard);
        await sheet.Open(_api, Id, Token);
        return sheet;
    }

    [Fact]
    public async Task An_approved_current_workflow_has_a_badge_and_no_callout()
    {
        var sheet = await Opened("approved");

        Assert.Equal(("Approved", false, false), (sheet.StandingText, sheet.HasCallout, sheet.NeedsApproval));
        Assert.Empty(sheet.StaleReasons);
    }

    [Fact]
    public async Task A_workflow_that_is_not_approved_says_so_and_offers_the_terminal_command()
    {
        var sheet = await Opened("unapproved");

        Assert.Equal(("Not approved", "This workflow is not approved, so it cannot start a run yet.", true), (sheet.StandingText, sheet.CalloutText, sheet.NeedsApproval));
        Assert.Equal(("To approve it, run this in a terminal:", "sindri recipe approve " + Id), (sheet.ApproveHint, sheet.ApproveCommand));
    }

    [Fact]
    public async Task A_revoked_workflow_has_its_own_callout()
    {
        var sheet = await Opened("revoked");

        Assert.Equal(("Revoked", "This workflow was revoked, so it cannot start a run."), (sheet.StandingText, sheet.CalloutText));
    }

    [Fact]
    public async Task An_out_of_date_workflow_names_each_reason_and_stays_approved()
    {
        var sheet = await Opened("approved", "\"pin-drift\",\"legacy-shape\"");

        Assert.Equal(("Approved", "This workflow is out of date.", true), (sheet.StandingText, sheet.CalloutText, sheet.HasCallout));
        Expect.Sequence(["A step it uses changed since it was approved", "It is written in an old format"], sheet.StaleReasons);
        Assert.False(sheet.NeedsApproval);
    }

    [Fact]
    public async Task Copy_puts_the_approve_command_on_the_clipboard_and_says_so()
    {
        var sheet = await Opened("unapproved");

        await sheet.CopyApprove(Token);

        Assert.Equal(("sindri recipe approve " + Id, "Copied"), (_clipboard.Text, sheet.CopyLabel));
    }

    [Fact]
    public async Task A_list_that_cannot_be_read_leaves_the_sheet_without_a_standing()
    {
        _server.RecipeListAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views have not caught up");
        var sheet = new RecipeSheetViewModel(_server, _clipboard);

        await sheet.Open(_api, Id, Token);

        Assert.Equal((true, false, string.Empty), (sheet.IsShown, sheet.HasCallout, sheet.StandingText));
    }

    [Fact]
    public async Task A_workflow_that_produces_nothing_says_so()
    {
        var sheet = new RecipeSheetViewModel(_server, _clipboard);
        await sheet.Open(_api, "workflow.no-outputs", Token);

        Assert.Equal("This workflow doesn't produce anything.", sheet.Facts?.NoOutputsText);
    }

    [Fact]
    public async Task A_known_workflow_gets_a_one_line_description()
    {
        var sheet = new RecipeSheetViewModel(_server, _clipboard);
        await sheet.Open(_api, "workflow.step", Token);

        Assert.Equal("One step with a checks-and-revise loop; used for trying workflows out.", sheet.Description);
        Assert.True(sheet.HasDescription);
    }
}
