using System.Text.Json.Serialization;

namespace Aw.Context.Core.Roles;

/// <summary>One repo's role counts: per configured role the indexed files it matches, and the role-like files no glob covers.</summary>
/// <param name="Repo">The repo's name.</param>
/// <param name="Roles">Its configured roles, in name order; empty when it configures none.</param>
/// <param name="Unassigned">Role-like files matching no glob; left out when there are none.</param>
public sealed record RepoRoles(
    string Repo,
    IReadOnlyList<RoleCount> Roles,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] UnassignedRoleFiles? Unassigned);
