using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="IAgentSessions"/> in memory: a launch is live until the test ends it, and spends what the test records. Held to <see cref="AgentSessionsContract"/>.</summary>
public sealed class FakeAgentSessions : IAgentSessions
{
    private readonly Dictionary<LaunchId, AgentSessionState> _ended = [];
    private readonly Dictionary<LaunchId, (int Sample, long Tokens)> _usage = [];

    /// <summary>Ends <paramref name="launch"/>'s session; a second end changes nothing.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="reason">Exit or lost.</param>
    /// <param name="exitCode">The exit code for an exit.</param>
    public void End(LaunchId launch, SessionEndReason reason, int? exitCode = null) => _ = _ended.TryAdd(launch, new AgentSessionState(reason == SessionEndReason.Exit ? AgentSessionEnd.Exit : AgentSessionEnd.Lost, exitCode));

    /// <summary>Ends <paramref name="launch"/>'s session as a sandbox limit kill; a second end changes nothing.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="kind">Memory or wall.</param>
    /// <param name="limit">The limit.</param>
    /// <param name="peak">The most the session used.</param>
    public void EndByLimit(LaunchId launch, LimitKind kind, long limit, long peak) =>
        _ = _ended.TryAdd(launch, new AgentSessionState(AgentSessionEnd.Limit, ExitCode: null, new AgentSessionLimit(kind, limit, peak)));

    /// <summary>Records a cumulative usage sample; an earlier sample number than the latest changes nothing.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="sample">The sample's number, from 1.</param>
    /// <param name="tokens">Input and output tokens so far.</param>
    public void Spend(LaunchId launch, int sample, long tokens)
    {
        if (!_usage.TryGetValue(launch, out var latest) || sample > latest.Sample)
        {
            _usage[launch] = (sample, tokens);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<AgentSessionState>> Watch(LaunchId launch, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<AgentSessionState>>(_ended.GetValueOrDefault(launch, AgentSessionState.Live));

    /// <inheritdoc/>
    public ValueTask<Result<long>> Usage(LaunchId launch, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<long>>(_usage.TryGetValue(launch, out var latest) ? latest.Tokens : 0L);
}
