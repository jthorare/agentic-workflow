namespace Aw.Sindri.Core.Reviews;

/// <summary>The empty state of the list (PR review page spec §6.3).</summary>
/// <param name="Text">The sentence.</param>
/// <param name="Hint">Where repos are set.</param>
/// <param name="Teams">The team-request caveat.</param>
public sealed record ReviewEmpty(string Text, string Hint, string Teams)
{
    /// <summary>The words of the empty state.</summary>
    public static ReviewEmpty Words { get; } = new("Nothing waiting on you and no open PRs of yours.", "Repos are set in Settings → Connections.", "Reviews asked of a team don't show here yet.");
}
