namespace Aw.Sindri.Ui.Api.Connections;

/// <summary>PUT /sindri/connections/{name}/key: the connection after its key was replaced, and whether the new key was checked with its service (#4358).</summary>
/// <param name="Connection">The connection.</param>
/// <param name="Checked">True when the service took the new key; false when the key was stored unchecked.</param>
/// <param name="UncheckedReason">Why it was not checked; null when it was.</param>
internal sealed record ConnectionKeyReplaced(ConnectionView Connection, bool Checked, string? UncheckedReason);
