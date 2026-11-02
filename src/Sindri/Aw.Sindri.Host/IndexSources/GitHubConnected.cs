namespace Aw.Sindri.Host.IndexSources;

/// <summary>What 'index connect github' did.</summary>
/// <param name="Pointer">The pointer now in the profile, such as 'keychain:sindri-index/github'.</param>
/// <param name="Repos">How many listed repos the token can read.</param>
internal sealed record GitHubConnected(string Pointer, int Repos);
