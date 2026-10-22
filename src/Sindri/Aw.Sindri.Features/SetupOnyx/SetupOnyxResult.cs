namespace Aw.Sindri.Features.SetupOnyx;

/// <summary>What the Onyx setup job did. Nothing here is a secret: the key is a pointer to where it is kept.</summary>
/// <param name="Url">Where Onyx answers.</param>
/// <param name="Version">The release it reported.</param>
/// <param name="KeyPointer">Where Sindri's token is stored (a pointer, never the token).</param>
/// <param name="ProfileChanged">True when the profile's Onyx section was written now.</param>
public sealed record SetupOnyxResult(string Url, string Version, string? KeyPointer, bool ProfileChanged);
