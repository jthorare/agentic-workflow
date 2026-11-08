namespace Aw.Sindri.Core.Connections.OAuth;

/// <summary>What the token endpoint needs to trade a callback's code for tokens.</summary>
/// <param name="Preset">The provider.</param>
/// <param name="Client">The OAuth client id (not secret).</param>
/// <param name="Code">The code from the callback.</param>
/// <param name="RedirectUri">The redirect used in the consent request.</param>
/// <param name="Verifier">The PKCE verifier. Secret: the record's text hides it.</param>
public sealed record CodeExchange(ConnectionPreset Preset, string Client, string Code, string RedirectUri, string Verifier)
{
    /// <summary>A fixed text: the code and the verifier are not in it.</summary>
    /// <returns>'CodeExchange(redacted)'.</returns>
    public override string ToString() => "CodeExchange(redacted)";
}
