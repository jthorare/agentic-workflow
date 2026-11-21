using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Testing.Launch;

/// <summary>An <see cref="ISessionHost"/> that keeps sessions in memory: a name is live from Start until Stop, and the test attaches clients to it.</summary>
public sealed class FakeSessionHost : ISessionHost, ISessionPanes
{
    private readonly Dictionary<string, string> _panes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ended = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _clients = new(StringComparer.Ordinal);
    private readonly List<HostSession> _started = [];
    private Error? _nextStartFailure;

    /// <inheritdoc/>
    public HostKind Kind => HostKind.Tmux;

    /// <summary>Every session Start accepted, in order (a Stop does not remove it).</summary>
    public IReadOnlyList<HostSession> Started => _started;

    /// <inheritdoc/>
    public string Socket => "sindri-launch";

    /// <summary>When true, <see cref="PaneId"/> answers null, as tmux does when it can't be asked.</summary>
    public bool PaneLookupFails { get; set; }

    /// <summary>When true, <see cref="Probe"/> reports tmux as not installed.</summary>
    public bool TmuxMissing { get; set; }

    /// <summary>How many sessions are live now.</summary>
    public int LiveCount => _clients.Count;

    /// <summary>When true, <see cref="Exists"/> answers false for a live session, as a host that did not answer in time does; Start still clashes with it.</summary>
    public bool ExistsAnswersFalse { get; set; }

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
        _panes[session.Name] = "%" + _started.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _started.Add(session);
        return ValueTask.FromResult<Result<HostRef>>(new HostRef(HostKind.Tmux, session.Name));
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Stop(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        _ = _clients.Remove(host.Name);
        _ = _panes.Remove(host.Name);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    /// <inheritdoc/>
    public ValueTask<bool> Exists(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        return ValueTask.FromResult(!ExistsAnswersFalse && _clients.ContainsKey(host.Name));
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
        return ["tmux", "-L", "sindri-launch", "attach", "-t", "=" + host.Name];
    }

    /// <inheritdoc/>
    public ValueTask<HostFacts> Probe(CancellationToken cancellationToken) =>
        ValueTask.FromResult(TmuxMissing ? new HostFacts(HostKind.Tmux, Present: false, Version: null, "not-installed") : new HostFacts(HostKind.Tmux, Present: true, "3.4", Problem: null));

    /// <inheritdoc/>
    public ValueTask<TmuxPaneId?> PaneId(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        return ValueTask.FromResult(PaneLookupFails || !_panes.TryGetValue(host.Name, out var pane) ? (TmuxPaneId?)null : TmuxPaneId.From(pane));
    }

    /// <summary>Ends a session's agent: <see cref="Running"/> answers false for it from now on (null while <see cref="PaneLookupFails"/>).</summary>
    /// <param name="name">The session name.</param>
    public void End(string name) => _ended.Add(name);

    /// <inheritdoc/>
    public ValueTask<bool?> Running(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        return ValueTask.FromResult<bool?>(PaneLookupFails ? null : _panes.ContainsKey(host.Name) && !_ended.Contains(host.Name));
    }
}
