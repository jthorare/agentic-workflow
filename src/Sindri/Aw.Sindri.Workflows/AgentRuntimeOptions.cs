namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The agent runtime's knobs (engine P8 D3): how often the watch asks about the session. Tests give it a shorter interval.</summary>
/// <param name="PollInterval">How often 'WatchAgentStep' reads the session's state and heartbeats; 5 s in production.</param>
internal sealed record AgentRuntimeOptions(TimeSpan PollInterval)
{
    /// <summary>How long the launch activity waits before it asks again when the session cap is reached (SND-LAUNCH-020; sandbox spec §6).</summary>
    public TimeSpan SlotRetry { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How long one launch activity keeps waiting for a free slot before it hands back to the workflow, which starts another (the activity's own limit is 2 minutes).</summary>
    public TimeSpan SlotSlice { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>The production options.</summary>
    public static AgentRuntimeOptions Default { get; } = new(TimeSpan.FromSeconds(5));
}
