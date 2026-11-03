using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Features.CheckMap;
using Aw.Context.Features.ExportMap;
using Aw.Context.Features.ImportMap;
using Aw.Context.Features.PreviewMap;
using Aw.Context.Features.PurgeMap;
using Aw.Context.Features.ShowMapStatus;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The map files and health half of the map facade (S-3a): import, export, status, the doctor checks and the purge.</summary>
internal sealed class MapAdmin(
    IService<PreviewMapRequest, MapFilePreview> preview,
    IService<ImportMapRequest, MapImported> import,
    IService<ExportMapRequest, MapExported> export,
    IService<ShowMapStatusRequest, MapStatusView> status,
    IService<CheckMapRequest, MapHealth> checkMap,
    MapHealthChecks health,
    IService<PurgeMapRequest, PurgeReport> purge)
{
    public ValueTask<Result<MapFilePreview>> PreviewMap(string path, ContextScope scope, CancellationToken cancellationToken) =>
        preview.Handle(new PreviewMapRequest(path, scope), cancellationToken);

    public ValueTask<Result<MapImported>> ImportMap(string path, string confirmedHash, ContextScope scope, CancellationToken cancellationToken) =>
        import.Handle(new ImportMapRequest(path, confirmedHash, scope), cancellationToken);

    public ValueTask<Result<MapExported>> ExportMap(string? path, ContextScope scope, CancellationToken cancellationToken) =>
        export.Handle(new ExportMapRequest(path, scope), cancellationToken);

    public ValueTask<Result<MapStatusView>> MapStatus(string? anchor, ContextScope scope, CancellationToken cancellationToken) =>
        status.Handle(new ShowMapStatusRequest(scope, anchor), cancellationToken);

    public ValueTask<Result<MapHealth>> CheckMap(ContextScope? scope, CancellationToken cancellationToken) =>
        checkMap.Handle(new CheckMapRequest(scope), cancellationToken);

    public ValueTask<Result<MapHealth>> CheckRetention(CancellationToken cancellationToken) =>
        health.CheckRetention(cancellationToken);

    public ValueTask<Result<MapHealth>> CheckStore(CancellationToken cancellationToken) =>
        health.CheckStore(cancellationToken);

    public ValueTask<Result<StoreUpkeep?>> Upkeep(CancellationToken cancellationToken) =>
        health.Upkeep(cancellationToken);

    public ValueTask<Result<PurgeReport>> PurgeExpired(CancellationToken cancellationToken) =>
        purge.Handle(new PurgeMapRequest(), cancellationToken);
}
