namespace Aw.Sindri.Core.Connections.OAuth;

/// <summary>
/// One sign-in's PKCE pair (RFC 7636, S256) and its 'state': the verifier is 43 to 128 base64url characters, the challenge is
/// base64url(SHA-256(verifier)), the state is 32 random bytes as base64url. The record's text hides all three.
/// </summary>
/// <param name="Verifier">The secret verifier, sent only to the token endpoint.</param>
/// <param name="Challenge">The challenge sent with the consent URL.</param>
/// <param name="State">The random state the callback has to return.</param>
public sealed record PkceChallenge(string Verifier, string Challenge, string State)
{
    /// <summary>The challenge method: always 'S256'.</summary>
    public const string Method = "S256";

    /// <summary>Makes a pair from a byte source.</summary>
    /// <param name="randomBytes">Gives n random bytes (RandomNumberGenerator.GetBytes in production, a fixed source in tests).</param>
    /// <returns>The pair.</returns>
    public static PkceChallenge Create(Func<int, byte[]> randomBytes) => throw new NotSupportedException();

    /// <summary>The S256 challenge of a verifier.</summary>
    /// <param name="verifier">The verifier.</param>
    /// <returns>base64url(SHA-256(verifier)) without padding.</returns>
    public static string ChallengeFor(string verifier) => throw new NotSupportedException();

    /// <summary>Checks the state a callback returned, in constant time.</summary>
    /// <param name="state">The state from the callback.</param>
    /// <returns>True when it is this sign-in's state.</returns>
    public bool Verify(string? state) => throw new NotSupportedException();

    /// <summary>A fixed text: the verifier and the state are not in it.</summary>
    /// <returns>'PkceChallenge(redacted)'.</returns>
    public override string ToString() => "PkceChallenge(redacted)";
}
