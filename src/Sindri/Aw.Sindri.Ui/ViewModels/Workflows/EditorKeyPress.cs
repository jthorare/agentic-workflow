namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>A key press with the focus facts that decide what it does.</summary>
/// <param name="Key">The key's name: a letter in upper case, "Enter", "Escape", "F2", "1", "Left", "Right", "Up" or "Down".</param>
/// <param name="Ctrl">Ctrl is held.</param>
/// <param name="Shift">Shift is held.</param>
/// <param name="Meta">Cmd is held (macOS).</param>
/// <param name="CanvasFocus">Focus is on the canvas.</param>
/// <param name="InField">Focus is in a text field.</param>
/// <param name="SheetOpen">A step sheet is open.</param>
/// <param name="ShellFocus">The shell owns focus (its command palette wins Ctrl+K).</param>
/// <param name="UndoToast">The "Step saved. Undo" toast is up.</param>
internal sealed record EditorKeyPress(string Key, bool Ctrl = false, bool Shift = false, bool Meta = false, bool CanvasFocus = false, bool InField = false, bool SheetOpen = false, bool ShellFocus = false, bool UndoToast = false);
