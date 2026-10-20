using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.PlanContext;

/// <summary>LN-CTX1: after the owner's approve, the design's surfaces go to the map as proposals (C9's port, run = the plan instance) and stop 2's answers as owner decision records (C14's ingest).</summary>
/// <param name="proposals">The run's proposals on the context map.</param>
/// <param name="decisions">The owner's answers as decision records.</param>
internal sealed class ProposePlanContextService(IScopeRunProposals proposals, IOwnerDecisions decisions) : IService<ProposePlanContextRequest, ProposePlanContextResponse>
{
    public async ValueTask<Result<ProposePlanContextResponse>> Handle(ProposePlanContextRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Approved)
        {
            return new ProposePlanContextResponse("skipped", "the owner has not approved the design", [], 0);
        }

        var facts = new LineFacts(request.Facts);
        var reasons = new List<string>();
        var statuses = await Propose(request, PlanContextRecords.Surfaces(facts), reasons, cancellationToken).ConfigureAwait(false);
        var records = PlanContextRecords.Decisions(facts);
        var stored = 0;
        if (records.Count > 0)
        {
            var ingested = await decisions.Ingest(request.Run, request.Packet, records, cancellationToken).ConfigureAwait(false);
            if (ingested.IsError)
            {
                return ingested.Error;
            }

            stored = ingested.Value.Stored;
            AddReason(reasons, ingested.Value.Skipped);
        }

        return new ProposePlanContextResponse("proposed", reasons.Count == 0 ? null : string.Join("; ", reasons), statuses, stored);
    }

    // An empty list would withdraw the run's proposals, so nothing is sent when the design adds no surface; a context engine that cannot answer costs the proposals, not the Step.
    private async Task<IReadOnlyList<ScopeSurfaceStatus>> Propose(ProposePlanContextRequest request, List<ProposedScopeSurface> surfaces, List<string> reasons, CancellationToken cancellationToken)
    {
        if (surfaces.Count == 0)
        {
            return [];
        }

        var proposed = await proposals.Replace(request.Run, request.Repo, surfaces, cancellationToken).ConfigureAwait(false);
        if (proposed.IsError)
        {
            reasons.Add($"the context engine could not take the surfaces: {proposed.Error.Message}");
            return [];
        }

        return proposed.Value;
    }

    private static void AddReason(List<string> reasons, string? reason)
    {
        if (reason is not null)
        {
            reasons.Add(reason);
        }
    }
}
