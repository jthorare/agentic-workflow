namespace Aw.Sindri.Core.Connections.OAuth;

/// <summary>A connection's tokens. It never prints them: ToString and the record's printing show no value.</summary>
/// <param name="AccessToken">The access token (or the API key of a rest connection).</param>
/// <param name="RefreshToken">The refresh token, or null.</param>
/// <param name="ExpiresAt">When the access token expires, or null.</param>
public sealed record TokenSet(string AccessToken, string? RefreshToken, DateTimeOffset? ExpiresAt)
{
    /// <summary>A fixed text: the tokens are not in it.</summary>
    /// <returns>'TokenSet(redacted)'.</returns>
    public override string ToString() => "TokenSet(redacted)";
}
