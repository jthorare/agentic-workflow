using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The keyboard map of the workflow editor (spec 4.6): a bare letter fires only with canvas focus and never in a field, and never writes.</summary>
[Lane(TestLane.Unit)]
public sealed class EditorKeysTests
{
    private static EditorKeyPress Canvas(string key) => new(key, CanvasFocus: true);

    [Theory]
    [InlineData("N", "AddStep")]
    [InlineData("Enter", "OpenSheet")]
    [InlineData("F2", "Rename")]
    [InlineData("T", "TestRun")]
    [InlineData("1", "FitGraph")]
    [InlineData("Left", "WalkGraph")]
    [InlineData("Right", "WalkGraph")]
    [InlineData("Up", "WalkGraph")]
    [InlineData("Down", "WalkGraph")]
    public void With_canvas_focus_the_key_does_its_action(string key, string action) => Assert.Equal(action, EditorKeys.Resolve(Canvas(key)).ToString());

    [Theory]
    [InlineData("N")]
    [InlineData("T")]
    [InlineData("1")]
    [InlineData("Enter")]
    [InlineData("Left")]
    public void A_bare_key_does_nothing_inside_a_field(string key) =>
        Assert.Equal(EditorKeyAction.None, EditorKeys.Resolve(new EditorKeyPress(key, CanvasFocus: true, InField: true)));

    [Theory]
    [InlineData("N")]
    [InlineData("T")]
    [InlineData("1")]
    [InlineData("Enter")]
    public void A_bare_key_does_nothing_without_canvas_focus(string key) => Assert.Equal(EditorKeyAction.None, EditorKeys.Resolve(new EditorKeyPress(key)));

    [Fact]
    public void Ctrl_K_opens_Add_step_unless_the_shell_owns_focus()
    {
        Assert.Equal(EditorKeyAction.AddStep, EditorKeys.Resolve(new EditorKeyPress("K", Ctrl: true, CanvasFocus: true)));
        Assert.Equal(EditorKeyAction.AddStep, EditorKeys.Resolve(new EditorKeyPress("K", Ctrl: true, InField: true)));
        Assert.Equal(EditorKeyAction.None, EditorKeys.Resolve(new EditorKeyPress("K", Ctrl: true, ShellFocus: true)));
    }

    [Fact]
    public void Ctrl_Enter_saves_the_step_only_while_a_sheet_is_open()
    {
        Assert.Equal(EditorKeyAction.SaveStep, EditorKeys.Resolve(new EditorKeyPress("Enter", Ctrl: true, SheetOpen: true, InField: true)));
        Assert.Equal(EditorKeyAction.None, EditorKeys.Resolve(new EditorKeyPress("Enter", Ctrl: true, CanvasFocus: true)));
    }

    [Fact]
    public void Ctrl_Z_undoes_only_while_the_saved_toast_is_up()
    {
        Assert.Equal(EditorKeyAction.Undo, EditorKeys.Resolve(new EditorKeyPress("Z", Ctrl: true, UndoToast: true)));
        Assert.Equal(EditorKeyAction.None, EditorKeys.Resolve(new EditorKeyPress("Z", Ctrl: true, CanvasFocus: true)));
    }

    [Fact]
    public void Activate_has_Ctrl_Shift_A_and_Cmd_Shift_A_and_no_bare_key()
    {
        Assert.Equal(EditorKeyAction.ActivateNow, EditorKeys.Resolve(new EditorKeyPress("A", Ctrl: true, Shift: true)));
        Assert.Equal(EditorKeyAction.ActivateNow, EditorKeys.Resolve(new EditorKeyPress("A", Meta: true, Shift: true)));
        Assert.Equal(EditorKeyAction.None, EditorKeys.Resolve(Canvas("A")));
        Assert.Equal(EditorKeyAction.None, EditorKeys.Resolve(Canvas("P")));   // Pause, Delete and Remove have no bare key
        Assert.Equal(EditorKeyAction.None, EditorKeys.Resolve(Canvas("Delete")));
    }

    [Fact]
    public void Escape_closes_in_a_field_and_on_the_canvas() =>
        Assert.All([new EditorKeyPress("Escape", InField: true), Canvas("Escape")], p => Assert.Equal(EditorKeyAction.Close, EditorKeys.Resolve(p)));

    [Fact]
    public void Only_Save_step_and_Undo_write() =>
        Assert.Equal(
            [EditorKeyAction.SaveStep, EditorKeyAction.Undo],
            Enum.GetValues<EditorKeyAction>().Where(EditorKeys.Writes));

    [Fact]
    public void Escape_closes_the_topmost_layer_in_the_order_confirm_picker_sheet_test()
    {
        Assert.Equal(EscLayer.ActivateConfirm, EditorKeys.TopLayer(activateConfirm: true, picker: true, sheet: true, runningTest: true));
        Assert.Equal(EscLayer.Picker, EditorKeys.TopLayer(activateConfirm: false, picker: true, sheet: true, runningTest: true));
        Assert.Equal(EscLayer.Sheet, EditorKeys.TopLayer(activateConfirm: false, picker: false, sheet: true, runningTest: true));
        Assert.Equal(EscLayer.RunningTest, EditorKeys.TopLayer(activateConfirm: false, picker: false, sheet: false, runningTest: true));
        Assert.Equal(EscLayer.None, EditorKeys.TopLayer(activateConfirm: false, picker: false, sheet: false, runningTest: false));
    }
}
