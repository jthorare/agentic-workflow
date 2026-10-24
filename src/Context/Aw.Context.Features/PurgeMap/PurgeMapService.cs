using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.PurgeMap;

/// <summary>The context-purge service's work (spec §6.2, Phase 3): deletes exports older than 14 days and retained versions past the newest 100.</summary>
internal sealed class PurgeMapService(IGraphStore store, IExportStore exports, TimeProvider clock) : IService<PurgeMapRequest, PurgeReport>
{
    public async ValueTask<Result<PurgeReport>> Handle(PurgeMapRequest request, CancellationToken cancellationToken)
    {
        var files = await exports.List(cancellationToken).ConfigureAwait(false);
        if (files.IsError)
        {
            return files.Error;
        }

        var removed = 0;
        foreach (var file in MapRetention.ExpiredExports(files.Value, clock.GetUtcNow()))
        {
            var deleted = await exports.Delete(file.Path, cancellationToken).ConfigureAwait(false);
            if (deleted.IsError)
            {
                return deleted.Error;
            }

            removed += deleted.Value ? 1 : 0;
        }

        var versions = await store.Versions(cancellationToken).ConfigureAwait(false);
        if (versions.IsError)
        {
            return versions.Error;
        }

        var dropped = await store.Delete(MapRetention.ExcessVersions(versions.Value, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        return dropped.IsError ? dropped.Error : new PurgeReport(removed, dropped.Value);
    }
}
