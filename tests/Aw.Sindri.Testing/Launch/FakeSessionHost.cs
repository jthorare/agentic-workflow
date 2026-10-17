using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Testing.Launch;

/// <summary>An <see cref="ISessionHost"/> that keeps sessions in memory: a name is live from Start until Stop, and the test attaches clients to it.</summary>
public sealed class FakeSessionHost : ISessionHost
{
    private readonly Dictionary<string, int> _clients = new(StringComparer.Ordinal);
    private readonly List<HostSession> _started = [];
    private Error? _nextStartFailure;

    /// <inheritdoc/>
    public HostKind Kind => HostKind.Tmux;

    /// <summary>Every session Start accepted, in order (a Stop does not remove it).</summary>
    public IReadOnlyList<HostSession> Started => _started;

    /// <summary>How many sessions are live now.</summary>
    public int LiveCount => _clients.Count;

    /// <summary>Makes the next Start fail with <paramref name="error"/>; the one after that works again.</summary>
    /// <param name="error">The error (SND-LAUNCH-009 for a host that overran).</param>
    public void FailNextStart(Error error) => _nextStartFailure = error;

    /// <summary>Attaches one client to a live session (a no-op for one that is gone).</summary>
    /// <param name="host">The session.</param>
    public void AttachClient(HostRef host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (_clients.TryGetValue(host.Name, out var n))
        {
            _clients[host.Name] = n + 1;
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<HostRef>> Start(HostSession session, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (_nextStartFailure is { } failure)
        {
            _nextStartFailure = null;
            return ValueTask.FromResult<Result<HostRef>>(failure);
        }

        if (_clients.ContainsKey(session.Name))
        {
            return ValueTask.FromResult<Result<HostRef>>(KernelErrors.Conflict.With($"A session named {session.Name} already exists"));
        }

        _clients[session.Name] = 0;
        _started.Add(session);
        return ValueTask.FromResult<Result<HostRef>>(new HostRef(HostKind.Tmux, session.Name));
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Stop(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        _ = _clients.Remove(host.Name);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    /// <inheritdoc/>
    public ValueTask<int> AttachedClients(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        return ValueTask.FromResult(_clients.GetValueOrDefault(host.Name));
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> AttachArgv(HostRef host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return ["tmux", "-L", "sindri", "attach-session", "-t", "=" + host.Name];
    }

    /// <inheritdoc/>
    public ValueTask<HostFacts> Probe(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new HostFacts(HostKind.Tmux, Present: true, "3.4", Problem: null));
}
