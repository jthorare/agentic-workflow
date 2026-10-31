using Aw.Context.Core.Map;

namespace Aw.Context.Features.QueryContext;

/// <summary>The rebuild state of a host that runs no refresh service: nothing is ever being built, so a stale map carries the fix.</summary>
internal sealed class NoMapRebuild : IMapRebuildState
{
    public ValueTask<MapRebuildStatus> Read(CancellationToken cancellationToken) => ValueTask.FromResult(MapRebuildStatus.Idle);
}
