using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Proposals;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Writes a scoping run's surfaces to the latest draft (context spec §8.5; README X63): the engine adds 'propose' rows (idempotent,
/// never again after a reject, never the current map), and 'context.map.proposed' is appended for the rows the run has in the draft (once per set of rows, so a retry
/// after a failed append writes the missing event, and a promote or a return to an earlier set of surfaces writes none). A job-class
/// command run synchronously: it is fast, and the app refreshes on the event.
/// </summary>
internal sealed class ProposeContextMapService(ContextScopes scopes, IScopeProposals engine, HeldLease held, ILedgerStore ledger, EventFactory events)
    : IService<ProposeContextMapRequest, ProposeContextMapResponse>
{
    private const int MaxRun = 200;

    public async ValueTask<Result<ProposeContextMapResponse>> Handle(ProposeContextMapRequest request, CancellationToken cancellationToken)
    {
        var proposal = Read(request);
        if (proposal.IsError)
        {
            return proposal.Error;
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");   // before any row is written: a retry must find nothing half done
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var proposed = await engine.Propose(proposal.Value, scope.Value, cancellationToken).ConfigureAwait(false);
        if (proposed.IsError)
        {
            return proposed.Error;
        }

        var standing = await engine.Proposals(proposal.Value.Run, scope.Value, cancellationToken).ConfigureAwait(false);
        if (standing.IsError)
        {
            return standing.Error;
        }

        // The event follows the rows the run has, not what this call added: a retry after a failed append finds the rows and writes the event they lack.
        return proposed.Value.RowsOrEmpty.Count == 0 && proposed.Value.WithdrawnRows is not { Count: > 0 }
            ? new ProposeContextMapResponse(proposed.Value, Sequence: null, standing.Value.Surfaces)
            : await Record(proposal.Value, proposed.Value, standing.Value.Surfaces, grant, cancellationToken).ConfigureAwait(false);
    }

    private static Result<ScopeProposal> Read(ProposeContextMapRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Run) || request.Run.Length > MaxRun)
        {
            return SindriErrors.InvalidInput.With($"run: the scoping run's id is 1 to {MaxRun} characters");
        }

        return ContextRepoName.TryFrom(request.Repo, out var repo)
            ? new ScopeProposal(request.Run, repo, request.Surfaces ?? [])
            : ContextErrors.RepoOutsideScope.With($"'{request.Repo}' is not a repo this scope can see (fix: name a repo of the approved profile)");
    }

    private async ValueTask<Result<ProposeContextMapResponse>> Record(ScopeProposal proposal, ProposeResult result, IReadOnlyList<GraphStatus> surfaces, LeaseGrant grant, CancellationToken cancellationToken)
    {
        // The counts of the call that wrote the rows, recomputed from the rows themselves: a retry that finds them already there counts them as existing, not added.
        var rowCount = result.RowsOrEmpty.Count;
        var withdrawn = result.WithdrawnRows ?? [];
        var payload = new ContextMapProposed(proposal.Run, proposal.Repo.Value, result.Draft?.Number ?? 0, rowCount, result.Existing + result.Added - rowCount, result.Rejected, withdrawn.Count > 0 ? withdrawn.Count : null);
        // The run and the rows it has for these surfaces (a hash of their ids), not the draft: the same rows are one event however often they are sent and across a promote, and a return to an earlier set of surfaces is that set's event.
        // A withdrawal is keyed by the undo rows it wrote, so a retry of the same withdrawal is the same event.
        var rows = string.Join(',', result.RowsOrEmpty.Select(id => id.Value).Concat(withdrawn.Select(id => "-" + id.Value)));
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rows)))[..16];
        var key = $"{EventTypes.ContextMapProposed}:{proposal.Run}:{hash}";
        var appended = await ledger.Append(events.Entry(EventTypes.ContextMapProposed, payload, key), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ProposeContextMapResponse(result, appended.Value.AlreadyRecorded ? null : appended.Value.Sequence, surfaces);
    }
}
