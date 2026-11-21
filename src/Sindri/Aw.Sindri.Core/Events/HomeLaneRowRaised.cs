namespace Aw.Sindri.Core.Events;

/// <summary>The 'home.lane-row.raised' payload (Home spec §3.3): a lane trigger holds, so Home shows its row. The row id is the kind's wire name, a colon and the key.</summary>
/// <param name="Kind">'lane-recycle', 'agent-limited' or 'pr-ready-merge'.</param>
/// <param name="Key">The lane, subject or PR number.</param>
/// <param name="Title">The headline.</param>
/// <param name="Reason">Why it needs the owner.</param>
/// <param name="At">When the collector saw the trigger hold.</param>
public sealed record HomeLaneRowRaised(string Kind, string Key, string Title, string Reason, DateTimeOffset At);
