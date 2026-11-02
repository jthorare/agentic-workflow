namespace Aw.Sindri.Host.IndexSources;

/// <summary>What 'index connect slack' did.</summary>
/// <param name="Pointer">The pointer now in the profile, such as 'keychain:sindri-index/slack'.</param>
/// <param name="Channels">How many listed channels the token can read.</param>
internal sealed record SlackConnected(string Pointer, int Channels);
