namespace Aw.Sindri.Host.IndexSources;

/// <summary>What 'index connect linear' did.</summary>
/// <param name="Pointer">The pointer now in the profile, such as 'keychain:sindri-index/linear'.</param>
/// <param name="Teams">How many teams the key was checked against: the listed ones, or the teams it reads when the profile lists none.</param>
/// <param name="Added">The team keys written into the profile because it listed none (sorted); empty when it already listed teams.</param>
internal sealed record LinearConnected(string Pointer, int Teams, IReadOnlyList<string> Added);
