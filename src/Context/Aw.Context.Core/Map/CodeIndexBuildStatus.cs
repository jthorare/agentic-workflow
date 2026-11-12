namespace Aw.Context.Core.Map;

/// <summary>Where a repo's code index build stands.</summary>
/// <param name="building">Whether a build or embed job for the repo is queued or running.</param>
/// <param name="progress">The latest progress line of that job (<c xml:lang="en">extracting 6,700 of 14,016 files</c>), or null before it reports one.</param>
public sealed class CodeIndexBuildStatus(bool building, string? progress = null)
{
    /// <summary>No build runs for the repo.</summary>
    public static CodeIndexBuildStatus Idle { get; } = new(building: false);

    /// <summary>Whether a build or embed job for the repo is queued or running.</summary>
    public bool Building { get; } = building;

    /// <summary>The latest progress line of the running job, or null.</summary>
    public string? Progress { get; } = progress;
}
