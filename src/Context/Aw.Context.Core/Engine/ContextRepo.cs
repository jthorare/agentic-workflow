namespace Aw.Context.Core.Engine;

/// <summary>A repo the profile names.</summary>
/// <param name="Name">Its short name (the 'repo:' key prefix and the X-Sindri-Repo scope).</param>
/// <param name="Path">The absolute path of its checkout.</param>
public sealed record ContextRepo(ContextRepoName Name, string Path)
{
    /// <summary>The globs of the paths extraction never reads: the built-in secret globs and the profile's 'denyPaths' for this repo.</summary>
    public IReadOnlyList<string> DenyPaths { get; init; } = PathDenylist.Defaults;

    /// <summary>The concepts the profile's 'index.concepts' pins for this repo (its own added to the profile's); none by default.</summary>
    public IReadOnlyList<ConceptPin> Concepts { get; init; } = [];

    /// <summary>Whether extraction must leave a path alone.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>True when a deny glob matches it.</returns>
    public bool Denies(string path) => PathDenylist.Matches(path, DenyPaths);

    /// <summary>The globs of the paths whose role the repo demotes: a demoted file keeps its code node but implements no feature.</summary>
    public IReadOnlyList<string> DemotePaths { get; init; } = [];

    /// <summary>Whether a path's role is demoted.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>True when a demote glob matches it.</returns>
    public bool Demotes(string path) => PathDenylist.Matches(path, DemotePaths);
}
