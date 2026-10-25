namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One repo's role counts in GET /sindri/context/map (issue 1105).</summary>
/// <param name="Repo">Repo, as served.</param>
/// <param name="Roles">Roles, as served, in name order.</param>
/// <param name="Unassigned">Unassigned, as served; absent when no file looks like a role without matching a glob.</param>
internal sealed record RepoRolesView(
    string Repo,
    IReadOnlyList<RoleCountView> Roles,
    UnassignedRoleFilesView? Unassigned = null);

/// <summary>How many indexed files one role matches.</summary>
/// <param name="Role">Role, as served.</param>
/// <param name="Demoted">True for a demoted role, false for an ignored one.</param>
/// <param name="Files">Files, as served.</param>
internal sealed record RoleCountView(string Role, bool Demoted, int Files);

/// <summary>Role-like files no glob covers.</summary>
/// <param name="Count">Count, as served.</param>
/// <param name="Examples">Examples, as served: at most five paths, in path order.</param>
internal sealed record UnassignedRoleFilesView(int Count, IReadOnlyList<string> Examples);
