using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The <c lang="text">none</c> host (launcher spec §3): it starts nothing, so the viewer runs the agent itself. Only human opens use it;
/// Steps always use tmux (SL4).
/// </summary>
internal sealed class NoSessionHost : ISessionHost
{
    /// <inheritdoc/>
    public HostKind Kind => HostKind.None;

    /// <inheritdoc/>
    public ValueTask<Result<HostRef>> Start(HostSession session, TimeSpan timeout, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<HostRef>>(SindriErrors.LaunchNoViewer.With("the none host starts no session; the viewer runs the agent"));

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Stop(HostRef host, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Unit.Value);

    /// <inheritdoc/>
    public ValueTask<int> AttachedClients(HostRef host, CancellationToken cancellationToken) => ValueTask.FromResult(0);

    /// <inheritdoc/>
    public IReadOnlyList<string> AttachArgv(HostRef host) => [];

    /// <inheritdoc/>
    public ValueTask<HostFacts> Probe(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new HostFacts(HostKind.None, Present: true, Version: null, Problem: null));
}
