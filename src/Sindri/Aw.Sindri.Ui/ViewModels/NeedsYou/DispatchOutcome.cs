namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What became of a Home row's action when the dispatcher ran it.</summary>
internal enum DispatchOutcome
{
    /// <summary>The command was sent and the server accepted it.</summary>
    Sent = 1,

    /// <summary>The command was sent and the server refused it (or no endpoint was known).</summary>
    Refused = 2,

    /// <summary>The action needs a choice the row cannot make: the row's sheet opens.</summary>
    OpenSheet = 3,

    /// <summary>The app has no send for the action yet: the row says so.</summary>
    Unhandled = 4,

    /// <summary>A command for the same target was already in flight; nothing was sent.</summary>
    Busy = 5,
}
