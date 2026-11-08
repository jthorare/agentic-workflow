namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a 'news' node asks a provider for.</summary>
/// <param name="Feed">A Hacker News list ('top', 'new', 'best', 'ask', 'show') or an RSS feed URL.</param>
/// <param name="Query">Keep only items whose title holds this text, or null for all.</param>
/// <param name="Limit">The most items to return.</param>
/// <param name="Since">Keep only items posted at or after this time, or null for all.</param>
public sealed record NewsRequest(string Feed, string? Query, int Limit, DateTimeOffset? Since);
