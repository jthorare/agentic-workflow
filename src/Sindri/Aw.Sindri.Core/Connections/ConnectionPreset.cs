namespace Aw.Sindri.Core.Connections;

/// <summary>
/// A shipped provider preset (config/connections/presets/&lt;id&gt;.json): public endpoints and defaults. It carries no client id;
/// the owner passes one with --client-id or the profile overlay supplies it.
/// </summary>
/// <param name="Key">The preset key ('github'): the name the JSON file gives under "id".</param>
/// <param name="DisplayName">The name to show ('GitHub').</param>
/// <param name="Kind">The connection kind it makes (oauth).</param>
/// <param name="AuthorizeUrl">The provider's consent page (https).</param>
/// <param name="TokenUrl">The provider's token endpoint (https).</param>
/// <param name="RevokeUrl">The provider's revoke endpoint (https), or null when it has none.</param>
/// <param name="DefaultScopes">The scopes asked for when the owner names none.</param>
/// <param name="HostGroup">The network host group the provider's API lives in (for the sandbox allowlist).</param>
/// <param name="LoopbackAllowed">Whether the provider accepts a plain-http 127.0.0.1 redirect; when false the sign-in note says how to sign in instead.</param>
/// <param name="SignInNote">A line shown with the consent URL.</param>
/// <param name="PollableEvents">Events a Connection event block (connection_event) can watch: the preset's entry in <see cref="PollableEvents"/>, the one list.</param>
/// <param name="ProbeUrl">A cheap authenticated GET that proves the token works, or null.</param>
public sealed record ConnectionPreset(
    string Key,
    string DisplayName,
    ConnectionKind Kind,
    string AuthorizeUrl,
    string TokenUrl,
    string? RevokeUrl,
    IReadOnlyList<string> DefaultScopes,
    string HostGroup,
    bool LoopbackAllowed,
    string SignInNote,
    IReadOnlyList<string> PollableEvents,
    string? ProbeUrl)
{
    /// <summary>The "auth" entry for a pasted key.</summary>
    public const string KeyAuth = "key";

    /// <summary>The "auth" entry for a browser sign-in.</summary>
    public const string OAuthAuth = "oauth";

    /// <summary>Gets the client id of Sindri's own OAuth app for this provider, as this build ships it (first-run spec decision 15a): null or empty until the owner registers the app. The profile's `setup.oauth.*` replaces it (<see cref="OAuthClientIds"/>). Not a secret.</summary>
    public string? ShippedClient { get; init; }

    /// <summary>Gets the provider's device authorization endpoint (RFC 8628, https), or null when the provider has no device flow.</summary>
    public string? DeviceCodeUrl { get; init; }

    /// <summary>Gets the ways to connect, the default first (#4357): "key" (a pasted API key or personal access token, a rest connection) and "oauth" (a browser sign-in). A file without "auth" reads as oauth only.</summary>
    public IReadOnlyList<string> Auth { get; init; } = [OAuthAuth];

    /// <summary>Gets a value indicating whether a key is the default way to connect: an add that names no kind stores a key (a rest connection).</summary>
    public bool KeyFirst => Auth.Count > 0 && string.Equals(Auth[0], KeyAuth, StringComparison.Ordinal);
}
