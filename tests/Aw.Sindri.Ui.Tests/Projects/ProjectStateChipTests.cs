using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The sheet's state chips save on pick (#3719): a pick keeps the open edit form, and a pick that is not sent leaves no mark.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectStateChipTests
{
    private readonly ProjectSheetFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ProjectStateChoice Other(ProjectSheetViewModel sheet) => sheet.StateChoices.First(s => s != sheet.SelectedState);

    [Fact]
    public async Task Picking_a_chip_while_editing_keeps_the_form_open_and_the_typed_text()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();
        sheet.EditNotes = "typed, not saved yet";

        await sheet.PickState(Other(sheet), Token);

        Assert.Equal((true, "typed, not saved yet"), (sheet.IsEditing, sheet.EditNotes));
    }

    [Fact]
    public async Task A_Save_after_a_chip_pick_in_the_open_form_still_sends_the_typed_note()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();
        sheet.EditNotes = "typed, not saved yet";
        await sheet.PickState(Other(sheet), Token);

        await sheet.Save(Token);

        var (_, body) = _f.Server.ProjectWrites[^1];
        Assert.Equal("typed, not saved yet", Assert.IsType<Ui.Api.Projects.ProjectChange>(body).Notes);
    }

    [Fact]
    public async Task A_chip_pick_while_a_write_is_in_flight_sends_nothing_and_marks_nothing()
    {
        _f.Server.CommandGate = new TaskCompletionSource();
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        var saved = sheet.SelectedState;
        sheet.BeginEdit();
        var saving = sheet.Save(Token);

        await sheet.PickState(Other(sheet), Token);

        Assert.Equal((saved, 1), (sheet.SelectedState, _f.Server.ProjectWrites.Count));
        _f.Server.CommandGate.SetResult();
        await saving;
    }

    [Fact]
    public async Task A_refused_chip_pick_puts_the_mark_back_on_the_saved_state()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        var saved = sheet.SelectedState;
        _f.Server.ProjectWriteAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-PROJECT-004", "refused");

        await sheet.PickState(Other(sheet), Token);

        Assert.Equal((saved, true), (sheet.SelectedState, sheet.SaveError.Length > 0));
    }
}
