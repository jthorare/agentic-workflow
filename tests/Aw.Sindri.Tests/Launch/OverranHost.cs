using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>A host whose Start reports it overran the bound but whose session came up anyway: the launcher must end it.</summary>
internal sealed class OverranHost : ISessionHost
{
    private readonly HashSet<string> _live = new(StringComparer.Ordinal);

    public HostKind Kind => HostKind.Tmux;

    public int LiveCount => _live.Count;

    public ValueTask<Result<HostRef>> Start(HostSession session, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        _ = _live.Add(session.Name);
        return ValueTask.FromResult<Result<HostRef>>(SindriErrors.LaunchTimedOut.With("tmux did not start the session within 4 s"));
    }

    public ValueTask<Result<Unit>> Stop(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        _ = _live.Remove(host.Name);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    public ValueTask<int> AttachedClients(HostRef host, CancellationToken cancellationToken) => ValueTask.FromResult(0);

    public IReadOnlyList<string> AttachArgv(HostRef host) => ["tmux", "attach"];

    public ValueTask<HostFacts> Probe(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new HostFacts(HostKind.Tmux, Present: true, "3.4", Problem: null));
}
