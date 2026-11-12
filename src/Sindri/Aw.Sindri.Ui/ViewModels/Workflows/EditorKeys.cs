#pragma warning disable MA0182 // wired into the page and its views in the next round (needs the P0 stubs)

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The keyboard map of the workflow editor (spec 4.6): a bare letter never commits a write and fires only with canvas focus, never inside a field.</summary>
internal static class EditorKeys
{
    /// <summary>Tells what a key press does.</summary>
    /// <param name="press">The key and the focus.</param>
    /// <returns>The action; <see cref="EditorKeyAction.None"/> when the key is not the editor's here.</returns>
    internal static EditorKeyAction Resolve(EditorKeyPress press)
    {
        var chord = press.Ctrl || press.Meta;
        if (string.Equals(press.Key, "Escape", StringComparison.Ordinal))
        {
            return EditorKeyAction.Close;
        }

        if (chord)
        {
            return ResolveChord(press);
        }

        if (press.Shift || !press.CanvasFocus || press.InField)
        {
            return EditorKeyAction.None;
        }

        return press.Key switch
        {
            "Left" or "Right" or "Up" or "Down" => EditorKeyAction.WalkGraph,
            "N" => EditorKeyAction.AddStep,
            "Enter" => EditorKeyAction.OpenSheet,
            "F2" => EditorKeyAction.Rename,
            "T" => EditorKeyAction.TestRun,
            "1" => EditorKeyAction.FitGraph,
            _ => EditorKeyAction.None,
        };
    }

    /// <summary>Tells whether the action writes to the workflow.</summary>
    /// <param name="action">The action.</param>
    /// <returns>True for Save step and Undo only.</returns>
    internal static bool Writes(EditorKeyAction action) => action is EditorKeyAction.SaveStep or EditorKeyAction.Undo;

    /// <summary>The layer Esc closes: the Activate confirm, then a picker, a sheet, the running test.</summary>
    /// <param name="activateConfirm">The confirm is open.</param>
    /// <param name="picker">A picker is open.</param>
    /// <param name="sheet">A sheet is open.</param>
    /// <param name="runningTest">A test is running.</param>
    /// <returns>The topmost layer.</returns>
    internal static EscLayer TopLayer(bool activateConfirm, bool picker, bool sheet, bool runningTest) =>
        activateConfirm ? EscLayer.ActivateConfirm : picker ? EscLayer.Picker : sheet ? EscLayer.Sheet : runningTest ? EscLayer.RunningTest : EscLayer.None;

    private static EditorKeyAction ResolveChord(EditorKeyPress press) => press.Key switch
    {
        "A" when press.Shift => EditorKeyAction.ActivateNow,   // Ctrl+Shift+A, and Cmd+Shift+A on macOS: page-level, in any focus
        "K" when !press.Shift && !press.ShellFocus => EditorKeyAction.AddStep,   // yields to the shell's command palette
        "Enter" when press.SheetOpen => EditorKeyAction.SaveStep,
        "Z" when !press.Shift && press.UndoToast => EditorKeyAction.Undo,
        _ => EditorKeyAction.None,
    };
}
