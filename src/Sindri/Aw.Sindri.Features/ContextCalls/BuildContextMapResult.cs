namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a build job did, small by design (the job's result is capped at 16 KiB): the draft, how many nodes were proposed and what degraded.</summary>
/// <param name="Repo">The repo the draft is for.</param>
/// <param name="Draft">The draft's version number.</param>
/// <param name="Proposed">How many nodes the build proposed.</param>
/// <param name="Degraded">What degraded, at most 20 lines (no labeling, a cluster pass that failed).</param>
public sealed record BuildContextMapResult(string Repo, int Draft, int Proposed, IReadOnlyList<string> Degraded)
{
    /// <summary>The structure measures as one line of numbers (map structure spec §4.3), on every build; null when the build did not measure.</summary>
    public string? Checks { get; init; }

    /// <summary>The build report's notes (what the concept pass did and could not do), at most 20 lines; empty when there are none.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>What the build's structure cut replaced (the migration line); null when it replaced nothing.</summary>
    public Context.Core.Map.StructureCounts? Replaced { get; init; }

    /// <summary>The result of a build.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="draft">The draft number.</param>
    /// <param name="proposed">How many nodes were proposed.</param>
    /// <param name="report">The build report.</param>
    /// <returns>The small result the job keeps.</returns>
    internal static BuildContextMapResult Of(string repo, int draft, int proposed, Context.Core.Map.MapBuildReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var replaced = report.Structure;
        return new BuildContextMapResult(repo, draft, proposed, [.. report.Degraded.Take(MaxLines)])
        {
            Checks = report.Checks?.Summary,
            Notes = [.. report.Notes.Take(MaxLines)],
            Replaced = replaced is { Features: 0, Links: 0, Undone: 0, Carried: 0 } ? null : replaced,
        };
    }

    private const int MaxLines = 20;
}
