using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Settings;
using Aw.Evidence.Core.Stack;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.CheckProvenance;

/// <summary>
/// Checks the database against the settings' seed policy (unconfigured, it matches only fictional example.com
/// addresses, so an install fails closed). Never an error: a failed query is 'unknown' with the reason.
/// With a worktree, the slot doctor checks is read the same way and its database is the one queried (SKILL 'Worktree stacks');
/// a worktree without a usable slot is 'unknown' with doctor's reason, never a fallback to the main stack's database.
/// </summary>
internal sealed class CheckProvenanceService(IProvenanceQuery query, IEvidenceSettings settings, IRunFiles files) : IService<CheckProvenanceRequest, CheckProvenanceResponse>
{
    public async ValueTask<Result<CheckProvenanceResponse>> Handle(CheckProvenanceRequest request, CancellationToken cancellationToken)
    {
        string? database = null;
        if (request.Worktree is { } worktree)
        {
            var slot = await WorktreeSlot.ReadAsync(settings.Slots, files, worktree.TrimEnd(trimChar: '/'), cancellationToken).ConfigureAwait(false);
            if (slot.IsError)
            {
                return new CheckProvenanceResponse(DbProvenance.Unknown, slot.Error.Message);
            }

            database = slot.Value.Database;
        }

        var finding = await ProvenanceCheck.RunAsync(query, settings.SeedPolicy, database, cancellationToken).ConfigureAwait(false);
        return new CheckProvenanceResponse(finding.Provenance, finding.Reason);
    }
}
