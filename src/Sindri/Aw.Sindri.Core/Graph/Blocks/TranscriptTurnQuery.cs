namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Which human turns 'transcript_turns' lists.</summary>
/// <param name="Since">Only turns that started at or after this time.</param>
/// <param name="Providers">Only these providers ('claude-code', 'codex', 'cursor'); every provider when empty.</param>
/// <param name="Repo">Only sessions of this main repo (an exact path match), or every repo when null.</param>
public sealed record TranscriptTurnQuery(DateTimeOffset Since, IReadOnlyList<string> Providers, string? Repo);
