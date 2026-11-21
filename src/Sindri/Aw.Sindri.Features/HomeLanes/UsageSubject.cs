namespace Aw.Sindri.Features.HomeLanes;

/// <summary>One subject of 'aw-meta usage --json'.</summary>
/// <param name="Subject">The lane name, or 'meta'.</param>
/// <param name="State">'Ok', 'Limited' or 'Unknown'.</param>
/// <param name="Kind">The limit kind ('Session', 'Weekly', 'Other') when limited; null otherwise.</param>
/// <param name="ResetsAt">When the limit resets; null when unknown.</param>
internal sealed record UsageSubject(string Subject, string State, string? Kind, DateTimeOffset? ResetsAt);
