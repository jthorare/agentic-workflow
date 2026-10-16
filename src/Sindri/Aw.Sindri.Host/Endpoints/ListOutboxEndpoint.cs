using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Features.ListOutbox;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/outbox[?state=pending|dispatched|refused|cancelled] (engine spec §6.3; query; engine P6 unit (c)): the
/// effects at their latest ledger row, read through the read-only ledger port. Another state is SND-CLI-003 (a 400).
/// </summary>
internal static class ListOutboxEndpoint
{
    internal static RouteGroupBuilder MapListOutbox(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/outbox", static async (string? state, IService<ListOutboxRequest, ListOutboxResponse> service, CancellationToken ct) =>
        {
            var request = Request(state);
            var listed = request.IsSuccess ? await service.Handle(request.Value, ct).ConfigureAwait(false) : request.Error;
            return listed.ToHttpResult();
        });
        return group;
    }

    /// <summary>The request for a '?state=' value: the state's wire name, as 'outbox list --json' writes it.</summary>
    private static Result<ListOutboxRequest> Request(string? state) => state switch
    {
        null => new ListOutboxRequest(),
        "pending" => new ListOutboxRequest(EffectState.Pending),
        "dispatched" => new ListOutboxRequest(EffectState.Dispatched),
        "refused" => new ListOutboxRequest(EffectState.Refused),
        "cancelled" => new ListOutboxRequest(EffectState.Cancelled),
        _ => SindriErrors.InvalidInput.With($"'{state}' isn't an outbox state (pending, dispatched, refused or cancelled)"),
    };
}
