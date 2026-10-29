namespace Aw.Sindri.Host.IndexSources;

/// <summary>What 'index connect linear' did.</summary>
/// <param name="Pointer">The pointer now in the profile, such as 'keychain:sindri-index/linear'.</param>
/// <param name="Teams">How many teams the key was checked against: the listed ones, or the teams it reads when the profile lists none.</param>
internal sealed record LinearConnected(string Pointer, int Teams);
