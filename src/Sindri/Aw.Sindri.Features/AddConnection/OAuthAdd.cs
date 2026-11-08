using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>What one oauth add needs. It carries the client secret, so its text shows the name only.</summary>
/// <param name="Name">The connection.</param>
/// <param name="Preset">The provider.</param>
/// <param name="Client">The OAuth client id (not secret).</param>
/// <param name="Secret">The OAuth client secret, or null for a public client.</param>
/// <param name="Scopes">The scopes; empty takes the preset's defaults.</param>
/// <param name="Actor">Who is adding.</param>
/// <param name="Wait">Whether the request waits for the callback.</param>
internal sealed record OAuthAdd(ConnectionName Name, ConnectionPreset Preset, string Client, string? Secret, IReadOnlyList<string> Scopes, string Actor, bool Wait)
{
    /// <summary>A fixed text with the name only.</summary>
    /// <returns>'OAuthAdd(&lt;name&gt;)'.</returns>
    public override string ToString() => $"OAuthAdd({Name})";
}
