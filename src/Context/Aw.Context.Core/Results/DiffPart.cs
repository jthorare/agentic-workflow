namespace Aw.Context.Core.Results;

/// <summary>The diff shape's facts on a result: the range, the changed files with their nodes, and the paths no node claims.</summary>
/// <param name="Repo">The repo the range was read in.</param>
/// <param name="Base">The full sha of the range's first commit.</param>
/// <param name="Head">The full sha of the range's second commit.</param>
/// <param name="MergeBase">The full sha the changes are measured from.</param>
/// <param name="Files">The changed files (denied paths left out), ordinal order, at most 1000.</param>
/// <param name="Unmapped">The paths of <paramref name="Files"/> whose status is unmapped, in the same order.</param>
/// <param name="Counts">The counts over every changed path.</param>
/// <param name="Truncated">True when more than 1000 files changed and the lists hold the first 1000.</param>
/// <param name="AnchorsTotal">How many distinct nodes the changed files claim.</param>
public sealed record DiffPart(
    Engine.ContextRepoName Repo,
    string Base,
    string Head,
    string MergeBase,
    IReadOnlyList<DiffFile> Files,
    IReadOnlyList<string> Unmapped,
    DiffCounts Counts,
    bool Truncated,
    int AnchorsTotal);
