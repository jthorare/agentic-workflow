namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The owner picked a version of a conflict, or applied or put back a hold, and Sindri accepted it: the toast with its countdown and what its Undo does (todo spec D11).</summary>
/// <param name="text">The toast's words ("Kept Obsidian's version.").</param>
/// <param name="undo">Reopens the conflict or the hold; null when it worked, else why not.</param>
/// <param name="seconds">Seconds left in the Undo window when Sindri named its end; null for the full 10.</param>
internal sealed class TodoPickedEventArgs(string text, Func<Task<string?>> undo, int? seconds = null) : EventArgs
{
    /// <summary>The toast's words.</summary>
    internal string Text { get; } = text;

    /// <summary>Reopens the conflict or the hold; null when it worked, else why not.</summary>
    internal Func<Task<string?>> Undo { get; } = undo;

    /// <summary>Seconds left in the Undo window, or null for the full 10.</summary>
    internal int? Seconds { get; } = seconds;
}
