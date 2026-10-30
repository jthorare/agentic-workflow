namespace Aw.Sindri.Core.Scheduling;

/// <summary>The commit a map build read for one repo.</summary>
/// <param name="Commit">The commit.</param>
/// <param name="OnDefaultBranch">Whether it was on the repo's default branch.</param>
public sealed record MapRepoHead(string Commit, bool OnDefaultBranch)
{
    /// <summary>The commit of the code index the draft's symbol citations were resolved against; null when none held <see cref="Commit"/> at the build (#1704).</summary>
    public string? IndexCommit { get; init; }

    /// <summary>Whether the draft was built with the code index of its commit: the index commit is the head's (either may be abbreviated).</summary>
    public bool BuiltWithIndex =>
        IndexCommit is { Length: > 0 } index && (Commit.StartsWith(index, StringComparison.OrdinalIgnoreCase) || index.StartsWith(Commit, StringComparison.OrdinalIgnoreCase));
}
