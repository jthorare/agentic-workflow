namespace Aw.Sindri.Core.Events;

/// <summary>The 'home.lane-row.cleared' payload (Home spec §3.3): the trigger of a lane row stopped holding, so its row goes.</summary>
/// <param name="Kind">'lane-recycle', 'agent-limited' or 'pr-ready-merge'.</param>
/// <param name="Key">The lane, subject or PR number.</param>
/// <param name="At">When the collector saw it stop.</param>
public sealed record HomeLaneRowCleared(string Kind, string Key, DateTimeOffset At);
