namespace Aw.Sindri.Ui.ViewModels.Toast;

/// <summary>
/// One toast to show. Named ToastMessage, not Toast, because a type called Toast inside the namespace Toast would hide itself
/// from the code of Aw.Sindri.Ui.ViewModels.
/// </summary>
/// <param name="Text">What it says, without the Undo words ("Done.", "Kept Obsidian's version.").</param>
/// <param name="Undo">What Undo runs, or null when the toast offers none. It answers null when it worked, else the reason it did not (the host then shows "Couldn't undo: reason").</param>
/// <param name="Kind">Plain or a pick's.</param>
/// <param name="CountdownSeconds">The window in seconds when the toast counts down on screen ("Undo (9 s)"); null shows no number and lasts 10 s.</param>
/// <param name="UndoAllowed">Asked each time Undo is pressed and each second: false turns the button and the key off for now (a workflow's Undo while an Activate is on the way); null means always on.</param>
internal sealed record ToastMessage(string Text, Func<Task<string?>>? Undo = null, ToastKind Kind = ToastKind.Normal, int? CountdownSeconds = null, Func<bool>? UndoAllowed = null);
