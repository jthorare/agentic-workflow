using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Roles;

/// <summary>Counts a map's code files per role per repo and finds the role-like ones no glob covers.</summary>
public static class RoleCounter
{
    /// <summary>The repo-relative paths of the map's code nodes, by repo name (the repo <see cref="NodeRepos"/> puts each in).</summary>
    /// <param name="graph">The map as the caller sees it.</param>
    /// <param name="mapRepo">The repo the map version was built for.</param>
    /// <returns>Paths per repo.</returns>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> CodePaths(GraphSnapshot graph, string? mapRepo)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var repoOf = NodeRepos.Index(graph, mapRepo);
        return graph.Nodes
            .Where(n => n.Key.Kind == NodeKind.Code && repoOf.TryGetValue(n.Key.Value, out var repo) && repo is not null)
            .GroupBy(n => repoOf[n.Key.Value]!, StringComparer.Ordinal)   // why: the filter above keeps only nodes whose repo is not null
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(n => n.Key.Name)], StringComparer.Ordinal);
    }

    /// <summary>The role counts of every repo that configures a role or has an unassigned role-like file.</summary>
    /// <param name="repos">The repos in scope.</param>
    /// <param name="pathsByRepo">The indexed paths per repo name.</param>
    /// <param name="dropped">What the map build recorded each ignored role dropped, or null when it recorded nothing.</param>
    /// <returns>One entry per such repo, in the order of <paramref name="repos"/>.</returns>
    public static IReadOnlyList<RepoRoles> Of(IReadOnlyList<ContextRepo> repos, IReadOnlyDictionary<string, IReadOnlyList<string>> pathsByRepo, IReadOnlyList<RoleDropped>? dropped = null)
    {
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(pathsByRepo);
        return [.. repos.Select(r => One(r, pathsByRepo.TryGetValue(r.Name.Value, out var paths) ? paths : [], dropped)).Where(static r => r.Roles.Count > 0 || r.Unassigned is not null)];
    }

    /// <summary>How many of <paramref name="paths"/> a role's globs match.</summary>
    /// <param name="globs">The role's globs.</param>
    /// <param name="paths">Repo-relative paths.</param>
    /// <returns>The number of matching paths.</returns>
    public static int Matching(IReadOnlyList<string> globs, IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(globs);
        ArgumentNullException.ThrowIfNull(paths);
        return paths.Count(p => PathDenylist.Matches(p, globs));
    }

    /// <summary>The tracked files each ignored role of <paramref name="repo"/> drops: a build can't index them, so it counts them from the tracked list and keeps the count with the map version (issue 1161).</summary>
    /// <param name="repo">The repo with its roles.</param>
    /// <param name="tracked">Its tracked files at the build's commit, deny globs not applied.</param>
    /// <returns>One entry per ignored role (0 when no tracked file matches), in role-name order; empty when the repo ignores no role.</returns>
    public static IReadOnlyList<RoleDropped> Dropped(ContextRepo repo, IReadOnlyList<string> tracked)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(tracked);
        return [.. repo.Roles.Where(static r => !r.Demoted).OrderBy(static r => r.Name, StringComparer.Ordinal).Select(r => new RoleDropped(repo.Name.Value, r.Name, Matching(r.Globs, tracked)))];
    }

    private static RepoRoles One(ContextRepo repo, IReadOnlyList<string> paths, IReadOnlyList<RoleDropped>? dropped)
    {
        var counts = repo.Roles.OrderBy(static r => r.Name, StringComparer.Ordinal)
            .Select(r => new RoleCount(r.Name, r.Demoted, Matching(r.Globs, paths)) { Ignored = r.Demoted ? null : dropped?.FirstOrDefault(d => d.Repo == repo.Name.Value && d.Role == r.Name)?.Files })
            .ToList();
        var loose = paths.Where(p => RoleLikePaths.Matches(p) && !Covered(repo, p)).Order(StringComparer.Ordinal).ToList();
        return new RepoRoles(repo.Name.Value, counts, loose.Count == 0 ? null : new UnassignedRoleFiles(loose.Count, [.. loose.Take(RoleLikePaths.ExampleLimit)]));
    }

    private static bool Covered(ContextRepo repo, string path) =>
        repo.Denies(path) || repo.Demotes(path) || repo.Roles.Any(r => PathDenylist.Matches(path, r.Globs));
}
