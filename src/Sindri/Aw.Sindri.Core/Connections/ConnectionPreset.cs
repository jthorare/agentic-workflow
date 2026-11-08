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
/// <param name="PollableEvents">Events a poll trigger can watch (empty for now).</param>
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
    string? ProbeUrl);
