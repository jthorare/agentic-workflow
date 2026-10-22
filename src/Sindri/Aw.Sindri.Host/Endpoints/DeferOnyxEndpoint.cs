using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.DeferOnyx;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/onyx/defer (setup-once spec §5.6; desk): answers 200 '{deferred, sequence}' and the Onyx step becomes 'skipped' (or todo again).</summary>
internal static class DeferOnyxEndpoint
{
    internal static RouteGroupBuilder MapDeferOnyx(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/onyx/defer", static async (DeferOnyxBody? body, IService<DeferOnyxRequest, DeferOnyxResponse> service, CancellationToken ct) =>
        {
            if (body?.Deferred is not { } deferred)
            {
                Result<DeferOnyxResponse> missing = SindriErrors.InvalidInput.With("deferred is needed (fix: send {\"deferred\": true} or {\"deferred\": false})");
                return missing.ToHttpResult();
            }

            return (await service.Handle(new DeferOnyxRequest(deferred), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
