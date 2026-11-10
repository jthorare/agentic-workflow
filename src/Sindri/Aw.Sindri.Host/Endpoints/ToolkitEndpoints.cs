using Aw.Kernel.Services;
using Aw.Sindri.Features.BuildToolkit;
using Aw.Sindri.Features.CreateToolkit;
using Aw.Sindri.Features.ListToolkitCalls;
using Aw.Sindri.Features.ListToolkits;
using Aw.Sindri.Features.PauseToolkit;
using Aw.Sindri.Features.ResumeToolkit;
using Aw.Sindri.Features.StartToolkit;
using Aw.Sindri.Features.StopToolkit;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The toolkit routes (agent-first PR 16c): GET /sindri/toolkits lists them, GET /sindri/toolkits/{name}/calls[?limit=] reads the call log, POST /sindri/toolkits/{name}/start|stop|pause|resume
/// change a toolkit's state, and POST /sindri/toolkits (create) and POST /sindri/toolkits/build start the two jobs. No route refuses an agent session.
/// </summary>
internal static class ToolkitEndpoints
{
    internal static RouteGroupBuilder MapToolkitReads(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/toolkits", static async (IService<ListToolkitsRequest, ListToolkitsResponse> service, CancellationToken ct)
            => (await service.Handle(new ListToolkitsRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/toolkits/{name}/calls", static async (string name, int? limit, IService<ListToolkitCallsRequest, ListToolkitCallsResponse> service, CancellationToken ct)
            => (await service.Handle(limit is { } n ? new ListToolkitCallsRequest(name, n) : new ListToolkitCallsRequest(name), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }

    internal static RouteGroupBuilder MapToolkitWrites(this RouteGroupBuilder group)
    {
        _ = group.MapJob<CreateToolkitRequest, CreateToolkitResult>("/toolkits").MapJob<BuildToolkitRequest, BuildToolkitResult>("/toolkits/build");
        _ = group.MapPost("/toolkits/{name}/start", static async (string name, IService<StartToolkitRequest, StartToolkitResponse> service, CancellationToken ct)
            => (await service.Handle(new StartToolkitRequest(name), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/toolkits/{name}/stop", static async (string name, IService<StopToolkitRequest, StopToolkitResponse> service, CancellationToken ct)
            => (await service.Handle(new StopToolkitRequest(name), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/toolkits/{name}/pause", static async (string name, IService<PauseToolkitRequest, PauseToolkitResponse> service, CancellationToken ct)
            => (await service.Handle(new PauseToolkitRequest(name), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/toolkits/{name}/resume", static async (string name, IService<ResumeToolkitRequest, ResumeToolkitResponse> service, CancellationToken ct)
            => (await service.Handle(new ResumeToolkitRequest(name), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
