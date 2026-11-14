using Aw.Kernel.Services;
using Aw.Sindri.Features.ConnectAccount;
using Aw.Sindri.Features.GetOpenRuns;
using Aw.Sindri.Features.RestartProcess;
using Aw.Sindri.Features.SkipSetupStep;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The first-run routes (first-run spec §7, A1, A4, A8): POST /sindri/setup/skip, POST /sindri/restart (202 with the old serve's RestartProcessResponse), POST /sindri/setup/accounts/{provider}
/// and GET /sindri/runs/open.
/// </summary>
internal static class FirstRunEndpoints
{
    internal static RouteGroupBuilder MapFirstRunWrites(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/setup/skip", static async (SkipSetupStepRequest request, IService<SkipSetupStepRequest, SkipSetupStepResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/restart", static async (IService<RestartProcessRequest, RestartProcessResponse> service, CancellationToken ct) =>
        {
            var restarted = await service.Handle(new RestartProcessRequest(), ct).ConfigureAwait(false);
            return restarted.IsSuccess ? TypedResults.Accepted((string?)null, restarted.Value) : HttpResults.Problem(restarted.Error);
        });
        _ = group.MapPost("/setup/accounts/{provider}", static async (string provider, IService<ConnectAccountRequest, ConnectAccountResponse> service, CancellationToken ct)
            => (await service.Handle(new ConnectAccountRequest(provider), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }

    internal static RouteGroupBuilder MapGetOpenRuns(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/runs/open", static async (IService<GetOpenRunsRequest, GetOpenRunsResponse> service, CancellationToken ct)
            => (await service.Handle(new GetOpenRunsRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
