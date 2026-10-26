namespace Aw.Context.Core.Roles;

/// <summary>How many indexed files one role of a repo matches.</summary>
/// <param name="Role">The role's name.</param>
/// <param name="Demoted">True for a demoted role, false for an ignored one (whose files are not indexed, so its count is normally 0).</param>
/// <param name="Files">Indexed files matching one of the role's globs.</param>
public sealed record RoleCount(string Role, bool Demoted, int Files);
