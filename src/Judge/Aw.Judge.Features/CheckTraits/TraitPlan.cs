namespace Aw.Judge.Features.CheckTraits;

/// <summary>What a check will ask: one task per excerpt and group of traits, each over a file of its own repo.</summary>
/// <param name="Home">The working directory's repo (its top-level directory), or null when it is in none.</param>
/// <param name="Tasks">The Jev requests.</param>
internal sealed record TraitPlan(string? Home, IReadOnlyList<TraitTask> Tasks)
{
    /// <summary>Edited image files that were left out before any request (past 4 files or 4 MiB, unreadable), by <see cref="Display(TraitTask)"/> path, with the reason code.</summary>
    public IReadOnlyDictionary<string, string> Skipped { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>How a file is named to the agent and in the response: repo-relative in the working directory's repo, absolute in any other.</summary>
    /// <param name="home">The working directory's repo, or null.</param>
    /// <param name="root">The file's repo.</param>
    /// <param name="path">The file, relative to its repo.</param>
    /// <returns>The path.</returns>
    internal static string Display(string? home, string root, string path) =>
        string.Equals(root, home, StringComparison.Ordinal) ? path : Path.Combine(root, path);

    /// <summary>The task's file, named as <see cref="Display(string?, string, string)"/> does.</summary>
    /// <param name="task">The task.</param>
    /// <returns>The path.</returns>
    internal string Display(TraitTask task) => Display(Home, task.Root, task.Source.Path);
}
