namespace Aw.Context.Core.Extract;

/// <summary>A group of files that belong together, found by clustering the code graph: a candidate feature.</summary>
/// <param name="Id">A stable name for the cluster ('c3').</param>
/// <param name="Files">Its files (paths from the repo root).</param>
/// <param name="Confidence">0 to 1.</param>
public sealed record FileCluster(ClusterId Id, IReadOnlyList<string> Files, double Confidence)
{
    /// <summary>The name the build gives the cluster when no model names it (derived from its path), or null when the source has none. The Jev pick offers it as the safe last candidate (spec §6.1 step 2).</summary>
    public string? FileName { get; init; }
}
