namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>What git says a repo folder is, for finding its T3 project.</summary>
/// <param name="MainCheckout">The main checkout a linked worktree belongs to (the folder itself for a main checkout), or null when git can't tell.</param>
/// <param name="RepositoryKey">The key of its primary remote, as T3 computes it, or null when it has no remote.</param>
internal sealed record T3RepoIdentity(string? MainCheckout, string? RepositoryKey);
