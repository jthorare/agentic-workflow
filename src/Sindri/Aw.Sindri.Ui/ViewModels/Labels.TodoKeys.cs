namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Todo panel's row keys (todo spec §3.3, §3.4).</summary>
internal static partial class Labels
{
    /// <summary>The toast after a check-off.</summary>
    internal const string TodoToastDone = "Done";

    /// <summary>The toast after a delete.</summary>
    internal const string TodoToastRemoved = "Removed";

    /// <summary>The toast after X on a done todo.</summary>
    internal const string TodoToastReopened = "Reopened";

    /// <summary>The toast after X on a removed todo.</summary>
    internal const string TodoToastRestored = "Restored";

    /// <summary>What Undo says (after "Couldn't undo: ") when the todo changed meanwhile.</summary>
    internal const string TodoUndoChanged = "it changed since. Here's the latest";

    /// <summary>The line on a row whose write failed.</summary>
    /// <param name="reason">Why Sindri refused, or that it isn't answering.</param>
    /// <returns>"Couldn't save: …".</returns>
    internal static string TodoCouldntSave(string reason) => "Couldn't save: " + reason;
}
