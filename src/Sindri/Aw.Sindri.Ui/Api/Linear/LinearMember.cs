namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>A member of a team.</summary>
/// <param name="Id">The user's Linear id.</param>
/// <param name="Name">The display name.</param>
/// <param name="Active">False for someone who left.</param>
internal sealed record LinearMember(string Id, string Name, bool Active);
