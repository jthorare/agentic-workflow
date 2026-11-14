using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;
using static Aw.Sindri.Ui.Tests.Projects.ProjectsFixture;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>Projects polish (#3719): no red box for "Linear is not connected", a removed project's sheet closes, one Save and state chips, and the Add sheet's From Linear tab says it plainly.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectsPolishLinearTests
{
    private readonly ProjectSheetFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ProjectsPageViewModel> ShownNotConnected()
    {
        var list = Golden("mixed");
        list["empty"] = "Linear isn't set up.";
        list["emptyCode"] = "linear-missing";
        _f.Projects.Serve(list);
        return await _f.Projects.Shown(Token);
    }

    [Fact]
    public async Task Linear_not_connected_is_a_quiet_line_with_Connect_and_no_red_box()
    {
        var page = await ShownNotConnected();

        Assert.Equal((false, true, "Linear not connected", true), (page.IsBanner, page.ShowsLinearNote, page.LinearNote, page.ShowsConnect));
    }

    [Fact]
    public async Task A_refresh_after_the_project_was_removed_elsewhere_closes_the_sheet_and_says_so_without_the_id_or_code()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        var done = new List<ProjectDoneEventArgs>();
        sheet.Done += (_, e) => done.Add(e);
        _f.Server.ProjectAnswers[ProjectSheetFixture.Attached] = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-PROJECT-404", "No project prj_x. `sindri projects` lists them.");

        await sheet.Refresh(ProjectsFixture.Url, Token);

        var e = Assert.Single(done);
        Assert.Equal((ProjectDone.Removed, "Removed elsewhere"), (e.Kind, e.Said));
        Assert.DoesNotContain("SND-", sheet.Note + sheet.ProblemText, StringComparison.Ordinal);
        Assert.DoesNotContain(ProjectSheetFixture.Attached, sheet.Note + sheet.ProblemText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Picking_a_state_chip_saves_the_state_at_once()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        await sheet.PickState(sheet.StateChoices.Single(s => string.Equals(s.Value, "inProgress", StringComparison.Ordinal)), Token);

        var (_, body) = Assert.Single(_f.Server.ProjectWrites);
        Assert.Equal("inProgress", Assert.IsType<Ui.Api.Projects.ProjectChange>(body).State);
    }

    [Fact]
    public async Task One_Save_sends_the_name_target_links_and_note_in_one_write()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();
        (sheet.EditName, sheet.EditTarget, sheet.EditNotes) = ("Renamed", "2027-01-31", "A new note");

        await sheet.Save(Token);

        var (_, body) = Assert.Single(_f.Server.ProjectWrites);
        var change = Assert.IsType<Ui.Api.Projects.ProjectChange>(body);
        Assert.Equal(("Renamed", "2027-01-31", "A new note"), (change.Name, change.Target, change.Notes));
    }

    [Fact]
    public void The_Add_sheet_opens_on_By_hand_and_says_Linear_is_not_connected_when_it_is_not()
    {
        var add = new ProjectAddViewModel(_f.Server, () => ProjectsFixture.Url);
        add.SetLinearConnected(connected: false);

        add.Reset();

        Assert.Equal((true, "Linear is not connected", true), (add.IsByHand, add.LinearNotConnectedText, add.ShowsLinearConnect));
    }
}
