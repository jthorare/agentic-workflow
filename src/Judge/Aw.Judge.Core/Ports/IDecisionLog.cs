using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Ids;
using Aw.Kernel;

namespace Aw.Judge.Core.Ports;

/// <summary>Where decisions and provider failures are kept. Every call returns an error, never throws, when the store can't be used.</summary>
public interface IDecisionLog
{
    /// <summary>Records a decision and its detail row in one unit of work.</summary>
    /// <param name="decision">The decision.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or AW-JUDGE-003 when the id is already recorded.</returns>
    public ValueTask<Result<Unit>> Record(DecisionRecord decision, CancellationToken cancellationToken);

    /// <summary>Records a provider failure.</summary>
    /// <param name="failure">The failure.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or the store's error.</returns>
    public ValueTask<Result<Unit>> RecordFailure(FailureRecord failure, CancellationToken cancellationToken);

    /// <summary>Finds a decision with its detail row.</summary>
    /// <param name="id">The decision id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The decision, or AW-JUDGE-002 when there is none.</returns>
    public ValueTask<Result<DecisionRecord>> Find(DecisionId id, CancellationToken cancellationToken);

    /// <summary>Sets a decision's undone time.</summary>
    /// <param name="id">The decision id.</param>
    /// <param name="at">When it was undone.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or AW-JUDGE-002 when there is no such decision.</returns>
    public ValueTask<Result<Unit>> MarkUndone(DecisionId id, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Sets a decision's approved time, unless it is already approved (approving twice keeps the first time).</summary>
    /// <param name="id">The decision id.</param>
    /// <param name="at">When it was approved.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or AW-JUDGE-002 when there is no such decision.</returns>
    public ValueTask<Result<Unit>> MarkApproved(DecisionId id, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Counts the failures at or after a time.</summary>
    /// <param name="since">The start of the window, inclusive.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The count.</returns>
    public ValueTask<Result<int>> CountFailuresSince(DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>The outcomes of the latest model attempts (decided or failed, never escalated, never a row written before the chain: EvaluationReasons.BeforeChain), newest first.</summary>
    /// <param name="count">How many at most.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The outcomes.</returns>
    public ValueTask<Result<IReadOnlyList<DecisionOutcome>>> LastAttemptOutcomes(int count, CancellationToken cancellationToken);

    /// <summary>Deletes the detail rows of decisions made before a time; the decisions stay.</summary>
    /// <param name="before">The cut-off, exclusive.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How many detail rows were deleted.</returns>
    public ValueTask<Result<int>> PruneDetailsBefore(DateTimeOffset before, CancellationToken cancellationToken);
}
