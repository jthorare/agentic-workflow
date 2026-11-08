namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>A todo the owner acted on with a key (check off, reopen, remove, restore): the toast to show and what its Undo does.</summary>
/// <param name="text">The toast's words ("Done", "Removed").</param>
/// <param name="undo">Puts the todo back; null when it worked, else why not.</param>
internal sealed class TodoActedEventArgs(string text, Func<Task<string?>> undo) : EventArgs
{
    /// <summary>The toast's words.</summary>
    internal string Text { get; } = text;

    /// <summary>Puts the todo back; null when it worked, else why not.</summary>
    internal Func<Task<string?>> Undo { get; } = undo;
}
