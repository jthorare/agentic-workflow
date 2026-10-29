using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sources;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The context index's read of the fake (#1597): GET /sindri/index/status answers a spec-shaped golden, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private int _indexCalls;

    /// <summary>GET /sindri/index/status's answer: the per-source golden by default.</summary>
    public RouteAnswer IndexStatusAnswer { get; set; } = RouteAnswer.SpecShaped("index-status", "per-source");

    /// <summary>How many times the status was read.</summary>
    public int IndexCalls => Volatile.Read(ref _indexCalls);

    public Task<Result<IndexStatusView>> IndexStatus(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _indexCalls);
        return Task.FromResult(Answer<IndexStatusView>(api, "/sindri/index/status", IndexStatusAnswer));
    }
}
