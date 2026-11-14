using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Launch;

/// <summary>What a 2xx start left behind.</summary>
/// <param name="Pane">The pane id the answer carried.</param>
/// <param name="Worktree">The worktree folder the start made, or null when it made none.</param>
public sealed record StartedEntry(TmuxPaneId Pane, string? Worktree)
{
    /// <summary>The title made from the start prompt (first non-empty line, 80 characters), or null; kept in memory only, so a restart drops it.</summary>
    public string? Title { get; init; }
}
