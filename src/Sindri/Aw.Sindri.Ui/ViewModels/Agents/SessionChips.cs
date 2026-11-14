using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The session chips (collector spec CO14, CU-6), shared by the Agents page and the session sheet.</summary>
internal static class SessionChips
{
    /// <summary>The chip for a served state. A word this build doesn't know renders as itself in capitals, so a newer Sindri never fails the read.</summary>
    /// <param name="state">The state as served (running, asking, idle, unknown, or something newer).</param>
    /// <returns>The glyph and the word.</returns>
    internal static SessionChip For(string state) => state switch
    {
        "running" => new("●", "Working", "working"),
        "asking" => new("●", "Waiting on you", "waiting"),
        "idle" => new("●", "Idle"),
        "unknown" => new("●", "Unknown"),
        "starting" => new("○", "Starting"),
        _ => new("●", state),
    };

    /// <summary>How long after the 2xx the Starting chip changes (agents spec §15.4).</summary>
    internal static readonly TimeSpan StartingChipAfter = TimeSpan.FromSeconds(4);

    /// <summary>The chip of a one-off that is still in starting (agents spec §15.4): "Starting" for 4 s, then "Answer in the pane" when Claude Code shows its trust dialog (a new worktree, or a folder Sindri has not started in), else "No recent activity". The row stays in starting.</summary>
    /// <param name="sinceStart">Time since the start's 2xx.</param>
    /// <param name="trustDialogLikely">True when the start made a new worktree or used a folder Sindri has not started in before.</param>
    /// <returns>The glyph and the word.</returns>
    internal static SessionChip Starting(TimeSpan sinceStart, bool trustDialogLikely) =>
        sinceStart < StartingChipAfter ? For("starting")
        : new("○", trustDialogLikely ? "Answer in the pane" : "No recent activity");

    /// <summary>The state's word in lower case, for an accessible name ("running").</summary>
    /// <param name="state">The state as served.</param>
    /// <returns>The word.</returns>
    internal static string Word(string state) => For(state).Word.ToLower(CultureInfo.InvariantCulture);
}
