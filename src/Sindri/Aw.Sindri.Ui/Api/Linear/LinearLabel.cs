namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>A label on an issue, or one a team offers.</summary>
/// <param name="Id">The label's Linear id.</param>
/// <param name="Name">The label's name.</param>
/// <param name="Color">A CSS hex colour.</param>
internal sealed record LinearLabel(string Id, string Name, string Color);
