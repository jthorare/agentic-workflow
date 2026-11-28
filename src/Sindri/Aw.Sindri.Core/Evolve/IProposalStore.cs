using Aw.Kernel;

namespace Aw.Sindri.Core.Evolve;

/// <summary>The proposals (issue 3787, piece 3): typed records in the local ledger; nothing here edits the repo.</summary>
public interface IProposalStore
{
    /// <summary>Saves a proposal, or merges it into the open one with the same target and <see cref="ProposalTitles.Normalized"/> title.</summary>
    /// <param name="proposal">The proposal and its sources.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Where it landed.</returns>
    public ValueTask<Result<ProposalSaved>> Save(ProposalToSave proposal, CancellationToken cancellationToken);

    /// <summary>Lists proposals, newest first.</summary>
    /// <param name="state">Only this state, or null for all.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The proposals.</returns>
    public ValueTask<Result<IReadOnlyList<StoredProposal>>> List(ProposalState? state, CancellationToken cancellationToken);

    /// <summary>Finds one proposal.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The proposal, or null when there is none.</returns>
    public ValueTask<Result<StoredProposal?>> Find(ProposalId id, CancellationToken cancellationToken);

    /// <summary>Marks a proposal rejected; the same target and title is never saved again.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The proposal as rejected; SND-PROP-001 when there is none, SND-PROP-002 when it is published, merged or rejected already.</returns>
    public ValueTask<Result<StoredProposal>> Reject(ProposalId id, CancellationToken cancellationToken);
}
