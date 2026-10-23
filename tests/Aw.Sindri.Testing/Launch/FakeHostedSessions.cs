using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing.Launch;

/// <summary>An <see cref="IHostedSessions"/> the test fills: a launch is reported alive until the test marks it gone, and until it is forgotten.</summary>
public sealed class FakeHostedSessions : IHostedSessions
{
    private readonly Dictionary<LaunchId, bool> _sessions = [];
    private readonly List<LaunchId> _forgotten = [];

    /// <summary>Every launch forgotten, in order.</summary>
    public IReadOnlyList<LaunchId> Forgotten => _forgotten;

    /// <summary>Adds a live session.</summary>
    /// <param name="launch">Its launch.</param>
    public void Add(LaunchId launch) => _sessions[launch] = true;

    /// <summary>Marks a session gone (its thread was archived or deleted).</summary>
    /// <param name="launch">Its launch.</param>
    public void MarkGone(LaunchId launch) => _sessions[launch] = false;

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<HostedSession>> Check(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<HostedSession>>([.. _sessions.Select(static s => new HostedSession(s.Key, s.Value))]);

    /// <inheritdoc/>
    public void Forget(LaunchId launch)
    {
        _ = _sessions.Remove(launch);
        _forgotten.Add(launch);
    }
}
