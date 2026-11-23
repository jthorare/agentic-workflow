namespace Aw.Sindri.Core.Home;

/// <summary>One subject's usage ('aw-meta usage --json' subjects[]).</summary>
/// <param name="Subject">The lane name, or 'meta'.</param>
/// <param name="State">'Ok', 'Limited' or 'Unknown'.</param>
/// <param name="Kind">The limit ('Session', 'Weekly', 'Other') when limited, or null.</param>
/// <param name="ResetsAt">When the limit resets, or null when unknown.</param>
/// <param name="Agent">The agent the subject runs, or null.</param>
public sealed record MetaUsage(string Subject, string State, string? Kind, DateTimeOffset? ResetsAt, string? Agent);
