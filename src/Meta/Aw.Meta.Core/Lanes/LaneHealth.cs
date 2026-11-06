namespace Aw.Meta.Core.Lanes;

/// <summary>How one non-Claude lane stands for the meta's obligations.</summary>
/// <param name="Lane">The lane.</param>
/// <param name="Agent">The lane's agent id.</param>
/// <param name="State">One line: "taking part", "launch-only: no obligations", "waiting for its session id", "hooks never ran: ..." (and the other hook-trace reasons), "hooks not verified: ...", "mail not delivered for N min: ...", "obligation not delivered (...)", or "ambiguous session id".</param>
/// <param name="Attention">Whether the state needs acting on (everything but taking part, launch-only and waiting for its session id, which every lane passes through after launch).</param>
public sealed record LaneHealth(string Lane, string Agent, string State, bool Attention);
