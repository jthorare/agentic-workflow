namespace Aw.Sindri.Ui.Api.Connections;

/// <summary>POST /sindri/connections/{name}/test.</summary>
/// <param name="Connection">The connection after the test.</param>
/// <param name="Ok">Whether the test passed.</param>
/// <param name="Reason">Why it failed, or null.</param>
/// <param name="Tools">The tools an mcp connection offers, or null.</param>
internal sealed record ConnectionTested(ConnectionView Connection, bool Ok, string? Reason, IReadOnlyList<string>? Tools);
