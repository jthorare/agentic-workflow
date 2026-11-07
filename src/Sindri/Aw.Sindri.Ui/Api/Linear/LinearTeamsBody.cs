namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The body of POST /sindri/linear/connection/teams. The key is held in memory by Sindri for one query and never logged.</summary>
/// <param name="Key">The read key.</param>
internal sealed record LinearTeamsBody(string Key);
