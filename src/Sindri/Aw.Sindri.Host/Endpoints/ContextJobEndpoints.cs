using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/context/sources/sync, /index/sync, /map/build and /eval/backtest: the context engine's jobs on the generic job route.</summary>
internal static class ContextJobEndpoints
{
    internal static RouteGroupBuilder MapContextJobs(this RouteGroupBuilder group) =>
        group.MapJob<SyncContextSourcesRequest, SyncContextSourcesResult>("/context/sources/sync", whenEmpty: static () => new SyncContextSourcesRequest())   // no body syncs every source
            .MapJob<SyncIndexSourceRequest, SyncIndexSourceResult>("/index/sync")   // the named source's own job: the body names it
            .MapJob<BuildContextMapRequest, BuildContextMapResult>("/context/map/build", static _ => new BuildContextMapRequest())   // the build takes no input: any body, or none, starts it
            .MapJob<RefreshContextMapRequest, RefreshContextMapResult>("/context/map/refresh", static _ => new RefreshContextMapRequest())   // the refresh takes no input either
            .MapJob<RunContextBacktestRequest, RunContextBacktestResult>("/context/eval/backtest");
}
