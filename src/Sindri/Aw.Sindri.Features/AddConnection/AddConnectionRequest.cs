namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// Add a connection ('sindri connection add &lt;name&gt; [--preset P] [--kind K] [--client-id ID] [--key-env NAME]'; POST /sindri/connections;
/// MCP connection_add). An oauth connection starts a browser sign-in; a rest or cli connection reads the key from the environment variable once.
/// </summary>
/// <param name="Name">The connection name ('linear:work').</param>
/// <param name="Preset">The preset id ('linear'); null for a hand-made connection.</param>
/// <param name="Kind">The kind's wire name; null takes the preset's.</param>
/// <param name="Client">The OAuth client id (named Client because a raw string id may not sit in a public signature, AW1001); the preset ships none, so an oauth connection needs one (SND-CONN-007).</param>
/// <param name="KeyEnv">The environment variable that holds the key of a rest or cli connection.</param>
/// <param name="Scopes">The scopes; empty takes the preset's defaults.</param>
/// <param name="Wait">True (the CLI): return when the callback landed. False (an agent): return the consent URL at once and finish in the background.</param>
public sealed record AddConnectionRequest(string Name, string? Preset, string? Kind, string? Client, string? KeyEnv, IReadOnlyList<string> Scopes, bool Wait);
