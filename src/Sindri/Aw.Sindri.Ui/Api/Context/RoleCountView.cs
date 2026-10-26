namespace Aw.Sindri.Ui.Api.Context;

/// <summary>How many indexed files one role matches.</summary>
/// <param name="Role">Role, as served.</param>
/// <param name="Demoted">True for a demoted role, false for an ignored one.</param>
/// <param name="Files">Files, as served.</param>
internal sealed record RoleCountView(string Role, bool Demoted, int Files);
