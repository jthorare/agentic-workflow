namespace Aw.Sindri.Core.Connections;

/// <summary>A connection's tokens. It never prints them: ToString and the record's printing show no value.</summary>
/// <param name="AccessToken">The access token (or the API key of a rest connection).</param>
/// <param name="RefreshToken">The refresh token, or null.</param>
/// <param name="ExpiresAt">When the access token expires, or null.</param>
/// <param name="ClientSecret">The OAuth client secret the token endpoint also wants (GitHub OAuth apps, Google desktop clients), or null for a public client. Stored with the tokens, in the same keychain secret.</param>
public sealed record TokenSet(string AccessToken, string? RefreshToken, DateTimeOffset? ExpiresAt, string? ClientSecret = null)
{
    /// <summary>A fixed text: the tokens are not in it.</summary>
    /// <returns>'TokenSet(redacted)'.</returns>
    public override string ToString() => "TokenSet(redacted)";
}
