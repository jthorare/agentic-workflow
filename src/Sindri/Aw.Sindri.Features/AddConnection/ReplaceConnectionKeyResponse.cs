using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>The connection after its key was replaced, and whether the new key was checked with its service.</summary>
/// <param name="Connection">The connection, connected, with no secret in it.</param>
/// <param name="Checked">True when the new key was asked of its service and passed; false when the connection has no service to ask (a cli connection, or a preset with no key check) and the key was stored unchecked.</param>
/// <param name="UncheckedReason">Why the key was not checked, in words; null when it was.</param>
public sealed record ReplaceConnectionKeyResponse(ConnectionView Connection, bool Checked, string? UncheckedReason);
