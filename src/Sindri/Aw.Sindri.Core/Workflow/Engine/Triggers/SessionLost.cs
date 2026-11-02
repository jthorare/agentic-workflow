namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// An agent run's session had already ended when the suspension that held the run (a park or a takeover) ended and the watch resumed (sandbox spec §7, SB16 (8)): there is no session
/// to hand back to, so the Step runs again in a new run. The answer's own effect (a grant) has applied; no round is counted and no Step failure is recorded.
/// </summary>
/// <param name="Actor">The service that saw the session had ended.</param>
/// <param name="Run">The agent run whose session ended; a trigger for another run is stale.</param>
public sealed record SessionLost(string Actor, RunToken Run) : Trigger(Actor)
{
    /// <summary>The tokens the lost run's session spent; the engine charges them to the budget (0 when they could not be read).</summary>
    public int Tokens { get; init; }

    /// <inheritdoc/>
    public override string Name => TriggerNames.SessionLost;

    /// <inheritdoc/>
    public override string KeyPart => "session.lost:run:" + Run.Value;
}
