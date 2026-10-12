using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.PingProcess;
using Aw.Sindri.Features.StopProcess;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>Maps every Sindri endpoint under /sindri (factory addendum §17.21 API): each binds, calls one service and maps its Result.</summary>
internal static class EndpointTable
{
    internal static WebApplication MapSindriEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/sindri");
        _ = group.MapGet("/status", static async (IService<GetStatusRequest, GetStatusResponse> service, CancellationToken ct)
            => (await service.Handle(new GetStatusRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/ping", static async (IService<PingProcessRequest, Ping> service, CancellationToken ct)
            => (await service.Handle(new PingProcessRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/stop", static async (IService<StopProcessRequest, StopProcessResponse> service, CancellationToken ct)
            => (await service.Handle(new StopProcessRequest(StopReason.Stop), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/profile/approve", static async (ApproveProfileRequest request, IService<ApproveProfileRequest, ApproveProfileResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/events", EventStream.Stream);
        return app;
    }
}
