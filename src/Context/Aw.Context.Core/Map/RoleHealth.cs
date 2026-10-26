using System.Globalization;
using Aw.Context.Core.Roles;

namespace Aw.Context.Core.Map;

/// <summary>Doctor 'context:roles': warns when indexed files look like a role but match no glob.</summary>
public static class RoleHealth
{
    private const string Name = "context:roles";

    /// <summary>The check for the role counts of a map.</summary>
    /// <param name="roles">The per-repo counts.</param>
    /// <returns>Ok when no repo has an unassigned file; a warning naming the counts and the fix otherwise.</returns>
    public static MapHealth Check(IReadOnlyList<RepoRoles> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var loose = roles.Where(static r => r.Unassigned is not null).ToList();
        if (loose.Count == 0)
        {
            return new MapHealth(Name, MapHealthState.Ok, Counted(roles), Fix: null);
        }

        // why: the filter above keeps only repos whose Unassigned is not null
        var detail = string.Join("; ", loose.Select(r => string.Create(CultureInfo.InvariantCulture, $"{r.Repo}: {r.Unassigned!.Count} role-like file(s) match no role glob ({string.Join(", ", r.Unassigned.Examples)})")));
        return new MapHealth(Name, MapHealthState.Warn, detail, "add a glob to index.demote or index.ignore in the profile");
    }

    private static string Counted(IReadOnlyList<RepoRoles> roles)
    {
        var words = roles.Where(static r => r.Roles.Count > 0).Select(r => $"{r.Repo}: {string.Join(", ", r.Roles.Select(c => string.Create(CultureInfo.InvariantCulture, $"{c.Role} {c.Files}")))}").ToList();
        return words.Count == 0 ? "no role globs configured, no role-like file outside one" : $"every role-like file is in a role glob ({string.Join("; ", words)})";
    }
}
