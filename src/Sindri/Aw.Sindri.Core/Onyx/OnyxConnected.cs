namespace Aw.Sindri.Core.Onyx;

/// <summary>What connecting Onyx did. Nothing here is a secret.</summary>
/// <param name="KeyPointer">Where Sindri's token is stored (a pointer, never the token); null while a step waits for the human.</param>
/// <param name="ProfileChanged">True when the profile's Onyx section was written now.</param>
public sealed record OnyxConnected(string? KeyPointer, bool ProfileChanged);
