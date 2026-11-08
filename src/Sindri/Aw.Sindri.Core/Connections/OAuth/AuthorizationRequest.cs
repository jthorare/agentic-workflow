namespace Aw.Sindri.Core.Connections.OAuth;

/// <summary>The consent-page request of one sign-in.</summary>
/// <param name="Preset">The provider preset.</param>
/// <param name="Client">The OAuth client id (not secret; named Client because a raw string id may not sit in a public signature, AW1001).</param>
/// <param name="RedirectUri">The loopback redirect ('http://127.0.0.1:&lt;port&gt;/callback').</param>
/// <param name="Scopes">The scopes asked for.</param>
/// <param name="Pkce">The sign-in's PKCE pair and state.</param>
public sealed record AuthorizationRequest(ConnectionPreset Preset, string Client, string RedirectUri, IReadOnlyList<string> Scopes, PkceChallenge Pkce)
{
    /// <summary>Builds the consent URL: response_type=code, client_id, redirect_uri, scope (space-separated), state, code_challenge, code_challenge_method=S256.</summary>
    /// <returns>The URL.</returns>
    public Uri ToUrl()
    {
        var query = new (string Key, string Value)[]
        {
            ("response_type", "code"),
            ("client_id", Client),
            ("redirect_uri", RedirectUri),
            ("scope", string.Join(' ', Scopes)),
            ("state", Pkce.State),
            ("code_challenge", Pkce.Challenge),
            ("code_challenge_method", PkceChallenge.Method),
        };
        var text = string.Join('&', query.Select(static pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
        var separator = Preset.AuthorizeUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return new Uri(Preset.AuthorizeUrl + separator + text);
    }
}
