using System.Text.Json.Serialization;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// Add a connection ('sindri connection add &lt;name&gt; [--preset P] [--kind K] [--client-id ID] [--client-secret-env NAME] [--key-env NAME]'; POST /sindri/connections;
/// MCP connection_add). An oauth connection starts a browser sign-in; a rest or cli connection stores a key.
/// </summary>
/// <remarks>
/// The CLI reads --key-env and --client-secret-env in its own process and sends the VALUES in 'Key' and 'ClientSecret' in the loopback POST, so the variable
/// need not exist in the environment of the running `sindri serve`. Those two fields are never echoed, logged, stored in the ledger or put in a row: the record's text
/// leaves them out. An MCP agent sends only the names ('KeyEnv', 'ClientSecretEnv'), which are read from serve's environment.
/// </remarks>
/// <param name="Name">The connection name ('linear:work').</param>
/// <param name="Preset">The preset id ('linear'); null for a hand-made connection.</param>
/// <param name="Kind">The kind's wire name; null takes the preset's.</param>
/// <param name="Client">The OAuth client id (named Client because a raw string id may not sit in a public signature, AW1001); the preset ships none, so an oauth connection needs one (SND-CONN-007).</param>
/// <param name="KeyEnv">The name of the environment variable that holds the key of a rest or cli connection (read from serve's environment when 'Key' is null).</param>
/// <param name="Scopes">The scopes; empty takes the preset's defaults.</param>
/// <param name="Wait">True (the CLI): return when the callback landed. False (an agent): return the consent URL at once and finish in the background.</param>
/// <param name="ClientSecretEnv">The name of the environment variable that holds the OAuth client secret, for a provider that wants one at the token endpoint (read from serve's environment when 'ClientSecret' is null).</param>
/// <param name="Key">The key itself, read by the CLI from its own environment. Secret.</param>
/// <param name="ClientSecret">The OAuth client secret itself, read by the CLI from its own environment. Secret.</param>
/// <param name="Url">The MCP server's URL; an mcp connection needs it (stored as the connection's 'serverUrl').</param>
public sealed record AddConnectionRequest(
    string Name,
    string? Preset,
    string? Kind,
    string? Client,
    string? KeyEnv,
    IReadOnlyList<string> Scopes,
    bool Wait,
    string? ClientSecretEnv = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Key = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ClientSecret = null,
    string? Url = null) : ICommand<AddConnectionResponse>
{
    /// <summary>A fixed text with the name only: the key and the client secret are not in it.</summary>
    /// <returns>'AddConnectionRequest(&lt;name&gt;)'.</returns>
    public override string ToString() => $"AddConnectionRequest({Name})";
}
