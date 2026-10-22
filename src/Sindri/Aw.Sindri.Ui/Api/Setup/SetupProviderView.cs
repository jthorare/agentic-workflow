namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>One installed provider's state for a repo's MCP registration.</summary>
/// <param name="Provider">The provider ('claude', 'codex', 'cursor').</param>
/// <param name="State">'connected', 'connecting', 'not-started' or 'failed'.</param>
/// <param name="Detail">Why it failed, or null.</param>
internal sealed record SetupProviderView(string Provider, string State, string? Detail);
