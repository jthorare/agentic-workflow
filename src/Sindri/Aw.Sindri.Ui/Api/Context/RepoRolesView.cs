namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One repo's role counts in GET /sindri/context/map (issue 1105).</summary>
/// <param name="Repo">Repo, as served.</param>
/// <param name="Roles">Roles, as served, in name order.</param>
/// <param name="Unassigned">Unassigned, as served; absent when no file looks like a role without matching a glob.</param>
internal sealed record RepoRolesView(
    string Repo,
    IReadOnlyList<RoleCountView> Roles,
    UnassignedRoleFilesView? Unassigned = null);
