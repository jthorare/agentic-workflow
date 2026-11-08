using Aw.Meta.Core.Usage;

namespace Aw.Meta.Core.Routing;

/// <summary>One lane's provider, model and usage at one look.</summary>
/// <param name="Lane">The lane name.</param>
/// <param name="Agent">The agent the lane is assigned to (routing.lanes first, else the one it runs).</param>
/// <param name="RunningAgent">The agent its session runs now.</param>
/// <param name="Model">The model it is assigned; null when unknown.</param>
/// <param name="State">Its limit-stop standing.</param>
/// <param name="Kind">The limit; set when limited.</param>
/// <param name="ResetsAt">When the limit resets.</param>
/// <param name="Signal">The provider's usage windows; null when the provider shows none.</param>
/// <param name="Note">Why there is no signal, or what it rests on.</param>
/// <param name="IsMeta">Whether this is the meta, which is read but never moved.</param>
public sealed record LaneStanding(string Lane, string Agent, string RunningAgent, string? Model, UsageState State, UsageKind? Kind, DateTimeOffset? ResetsAt, ProviderSignal? Signal, string? Note, bool IsMeta = false)
{
    /// <summary>Whether the lane is stopped, by a limit stop or by the provider saying so.</summary>
    public bool Limited => State == UsageState.Limited || Signal is { LimitReached: true };

    /// <summary>The share left, when the provider shows it.</summary>
    public double? RemainingPct => Signal?.RemainingPct;

    /// <summary>Whether routing.lanes assigns a different agent than the session runs, so a relaunch is due.</summary>
    public bool PendingRelaunch => !string.Equals(Agent, RunningAgent, StringComparison.Ordinal);
}
