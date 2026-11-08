namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The owner picked a version of a conflict, or applied or put back a hold, and Sindri accepted it: the toast with its countdown and what its Undo does (todo spec D11).</summary>
/// <param name="text">The toast's words ("Kept Obsidian's version.").</param>
/// <param name="undo">Reopens the conflict or the hold; null when it worked, else why not.</param>
internal sealed class TodoPickedEventArgs(string text, Func<Task<string?>> undo) : EventArgs
{
    /// <summary>The toast's words.</summary>
    internal string Text { get; } = text;

    /// <summary>Reopens the conflict or the hold; null when it worked, else why not.</summary>
    internal Func<Task<string?>> Undo { get; } = undo;
}
