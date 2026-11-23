namespace Aw.Sindri.Features.GetMessagesStatus;

/// <summary>The sync status (messages spec §8.2): the path, its state, the last read, the gaps, the scopes and the rate-limit words.</summary>
/// <param name="Path">How the sync reads Slack: 'poll' in Release 1, 'socket' from Release 3.</param>
/// <param name="State">The header's words: 'Live (polling every 30 s)', 'Behind: …', 'Offline: last read 09:41' (§5.3).</param>
/// <param name="LastRead">When the sync last read Slack, or null before the first read.</param>
/// <param name="Gaps">How many gap markers the store holds.</param>
/// <param name="Scopes">The scopes the token was granted.</param>
/// <param name="RateLimit">The rate-limit words, or null when no method waits.</param>
public sealed record GetMessagesStatusResponse(string Path, string State, DateTimeOffset? LastRead, int Gaps, IReadOnlyList<string> Scopes, string? RateLimit);
