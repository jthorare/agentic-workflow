namespace Aw.Sindri.Core.Profile;

/// <summary>How many judge calls a judge Step runs at once (issue 1440): the repo's 'workflow.judge.parallel', else the box's, else nothing (the Step's own default).</summary>
public static class JudgeParallel
{
    /// <summary>The fewest calls at once.</summary>
    public const int Min = 1;

    /// <summary>The most calls at once.</summary>
    public const int Max = 16;

    /// <summary>The configured width.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="repo">The item's repo name, or null.</param>
    /// <returns>The repo's value, else the box's, else null.</returns>
    public static int? Effective(SindriProfile profile, string? repo)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, repo, StringComparison.Ordinal))?.Workflow?.Judge?.Parallel
            ?? profile.Workflow?.Judge?.Parallel;
    }
}
