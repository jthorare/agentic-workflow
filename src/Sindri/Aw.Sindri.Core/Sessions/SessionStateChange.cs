namespace Aw.Sindri.Core.Sessions;

/// <summary>One live session whose state differs from the last good pass (Agents page spec §7.2). Not a ledger row until the collector appends 'session.changed'.</summary>
/// <param name="Ref">The session.</param>
/// <param name="State">The state it is in now.</param>
/// <param name="ObservedAt">The time of the pass that saw it.</param>
public sealed record SessionStateChange(SessionRef Ref, SessionState State, DateTimeOffset ObservedAt);
