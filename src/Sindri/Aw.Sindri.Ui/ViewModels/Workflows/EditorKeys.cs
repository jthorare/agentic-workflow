#pragma warning disable MA0041, MA0182 // MA0041: stub bodies; MA0182: wired into the page views in the next round

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The keyboard map of the workflow editor (spec 4.6): a bare letter never commits a write and fires only with canvas focus, never inside a field.</summary>
internal static class EditorKeys
{
    /// <summary>Tells what a key press does.</summary>
    /// <param name="press">The key and the focus.</param>
    /// <returns>The action; <see cref="EditorKeyAction.None"/> when the key is not the editor's here.</returns>
    internal static EditorKeyAction Resolve(EditorKeyPress press) => throw new NotSupportedException();

    /// <summary>Tells whether the action writes to the workflow.</summary>
    /// <param name="action">The action.</param>
    /// <returns>True for Save step and Undo only.</returns>
    internal static bool Writes(EditorKeyAction action) => throw new NotSupportedException();

    /// <summary>The layer Esc closes: the Activate confirm, then a picker, a sheet, the running test.</summary>
    /// <param name="activateConfirm">The confirm is open.</param>
    /// <param name="picker">A picker is open.</param>
    /// <param name="sheet">A sheet is open.</param>
    /// <param name="runningTest">A test is running.</param>
    /// <returns>The topmost layer.</returns>
    internal static EscLayer TopLayer(bool activateConfirm, bool picker, bool sheet, bool runningTest) => throw new NotSupportedException();
}
