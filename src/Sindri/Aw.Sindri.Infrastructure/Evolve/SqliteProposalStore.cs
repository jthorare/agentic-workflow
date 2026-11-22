using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Evolve;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Evolve;

/// <summary>
/// The 'proposal', 'proposal_source' and 'proposal_public' tables of the ledger file (migration 22, issue 3787). A ledger older than 22 reads as no proposals.
/// The caller scrubs text before it saves; the store writes what it is given.
/// </summary>
/// <param name="database">The ledger file.</param>
/// <param name="ids">Makes new proposal ids.</param>
/// <param name="clock">Stamps the rows.</param>
internal sealed class SqliteProposalStore(LedgerDatabase database, IIdGenerator ids, TimeProvider clock) : IProposalStore
{
    public ValueTask<Result<ProposalSaved>> Save(ProposalToSave proposal, CancellationToken cancellationToken) => throw new NotSupportedException(string.Join(' ', database.GetType().Name, ids.GetType().Name, clock.GetType().Name));

    public ValueTask<Result<IReadOnlyList<StoredProposal>>> List(ProposalState? state, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<StoredProposal?>> Find(ProposalId id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<StoredProposal>> Reject(ProposalId id, CancellationToken cancellationToken) => throw new NotSupportedException();
}
