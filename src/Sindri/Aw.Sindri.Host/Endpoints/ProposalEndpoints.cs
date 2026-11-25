using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Evolve;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.GetProposal;
using Aw.Sindri.Features.ListProposals;
using Aw.Sindri.Features.RejectProposal;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The proposal routes (issue 3787, piece 3): GET /sindri/proposals[?state=] lists them, GET /sindri/proposals/{id} reads one, and POST /sindri/proposals/{id}/reject is the owner's
/// (a session gets SND-AUTH-002). A bad id or state is a 400 SND-CLI-003.
/// </summary>
internal static class ProposalEndpoints
{
    internal static RouteGroupBuilder MapProposalReads(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/proposals", static async (string? state, IService<ListProposalsRequest, ListProposalsResponse> service, CancellationToken ct) =>
        {
            var parsed = State(state);
            return parsed.IsError
                ? ((Result<ListProposalsResponse>)parsed.Error).ToHttpResult()
                : (await service.Handle(new ListProposalsRequest(parsed.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        _ = group.MapGet("/proposals/{id}", static async (string id, IService<GetProposalRequest, GetProposalResponse> service, CancellationToken ct) =>
        {
            var parsed = Id(id);
            return parsed.IsError
                ? ((Result<GetProposalResponse>)parsed.Error).ToHttpResult()
                : (await service.Handle(new GetProposalRequest(parsed.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }

    internal static RouteGroupBuilder MapProposalWrites(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/proposals/{id}/reject", static async (string id, IService<RejectProposalRequest, RejectProposalResponse> service, CancellationToken ct) =>
        {
            var parsed = Id(id);
            return parsed.IsError
                ? ((Result<RejectProposalResponse>)parsed.Error).ToHttpResult()
                : (await service.Handle(new RejectProposalRequest(parsed.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }

    private static Result<ProposalId> Id(string id) =>
        ProposalId.TryFrom(id, out var parsed) ? parsed : SindriErrors.InvalidInput.With("'" + SafeText.Line(SafeText.Escape(id), 80) + "' isn't a proposal id (prp_…)");

    // why: 'state' is optional; when given it must name a state, so a typo never reads as "all" and a number never reads as a state
    private static Result<ProposalState?> State(string? state)
    {
        if (state is null)
        {
            return (ProposalState?)null;
        }

        foreach (var value in Enum.GetValues<ProposalState>())
        {
            if (string.Equals(value.ToString(), state, StringComparison.OrdinalIgnoreCase))
            {
                return (ProposalState?)value;
            }
        }

        return SindriErrors.InvalidInput.With("'" + SafeText.Line(SafeText.Escape(state), 40) + "' isn't a proposal state (proposed, staged, held, published, merged, rejected)");
    }
}
