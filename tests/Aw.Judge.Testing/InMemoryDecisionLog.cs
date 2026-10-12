using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Testing;

/// <summary>The in-memory IDecisionLog fake; it passes DecisionLogContract.</summary>
public sealed class InMemoryDecisionLog : IDecisionLog
{
    private static readonly Error _unavailable = new("AW-DATA-002", ErrorKind.Unavailable, "the in-memory decision log is set unavailable");
    private readonly List<DecisionRecord> _decisions = [];
    private readonly List<FailureRecord> _failures = [];

    /// <summary>When true every call returns an error, as a store that can't be opened.</summary>
    public bool Unavailable { get; init; }

    /// <summary>The decisions recorded so far, in insertion order.</summary>
    public IReadOnlyList<DecisionRecord> Decisions => _decisions;

    /// <summary>The failures recorded so far, in insertion order.</summary>
    public IReadOnlyList<FailureRecord> Failures => _failures;

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Record(DecisionRecord decision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (Unavailable)
        {
            return Done<Unit>(_unavailable);
        }

        if (IndexOf(decision.Id) >= 0)
        {
            return Done<Unit>(JudgeErrors.DecisionExists.With($"decision already recorded: {decision.Id.Value}"));
        }

        _decisions.Add(decision);
        return Done(Results.Ok());
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> RecordFailure(FailureRecord failure, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Done<Unit>(_unavailable);
        }

        _failures.Add(failure);
        return Done(Results.Ok());
    }

    /// <inheritdoc />
    public ValueTask<Result<DecisionRecord>> Find(DecisionId id, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Done<DecisionRecord>(_unavailable);
        }

        var index = IndexOf(id);
        return index < 0 ? Done<DecisionRecord>(Unknown(id)) : Done(Results.Success(_decisions[index]));
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> MarkUndone(DecisionId id, DateTimeOffset at, CancellationToken cancellationToken) =>
        Change(id, decision => decision with { UndoneAt = at });

    /// <inheritdoc />
    public ValueTask<Result<Unit>> MarkApproved(DecisionId id, DateTimeOffset at, CancellationToken cancellationToken) =>
        Change(id, decision => decision with { ApprovedAt = decision.ApprovedAt ?? at });

    /// <inheritdoc />
    public ValueTask<Result<int>> CountFailuresSince(DateTimeOffset since, CancellationToken cancellationToken) =>
        Unavailable ? Done<int>(_unavailable) : Done(Results.Success(_failures.Count(f => f.Ts >= since)));

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<DecisionOutcome>>> LastAttemptOutcomes(int count, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Done<IReadOnlyList<DecisionOutcome>>(_unavailable);
        }

        IReadOnlyList<DecisionOutcome> outcomes = [.. _decisions
            .Select((decision, order) => (decision, order))
            .Where(d => d.decision.Outcome != DecisionOutcome.Escalated && !EvaluationReasons.BeforeChain.Contains(d.decision.ReasonCode, StringComparer.Ordinal))
            .OrderByDescending(d => d.decision.Ts).ThenByDescending(d => d.order)
            .Take(count)
            .Select(d => d.decision.Outcome)];
        return Done(Results.Success(outcomes));
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> PruneDetailsBefore(DateTimeOffset before, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Done<int>(_unavailable);
        }

        var pruned = 0;
        for (var i = 0; i < _decisions.Count; i++)
        {
            if (_decisions[i].Ts < before && _decisions[i].Details is not null)
            {
                _decisions[i] = _decisions[i] with { Details = null };
                pruned++;
            }
        }

        return Done(Results.Success(pruned));
    }

    private static Error Unknown(DecisionId id) => JudgeErrors.UnknownDecision.With($"unknown decision: {id.Value}");

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);

    private static ValueTask<Result<T>> Done<T>(Error error) => ValueTask.FromResult(Results.Failure<T>(error));

    private int IndexOf(DecisionId id) => _decisions.FindIndex(d => d.Id.Value == id.Value);

    private ValueTask<Result<Unit>> Change(DecisionId id, Func<DecisionRecord, DecisionRecord> change)
    {
        if (Unavailable)
        {
            return Done<Unit>(_unavailable);
        }

        var index = IndexOf(id);
        if (index < 0)
        {
            return Done<Unit>(Unknown(id));
        }

        _decisions[index] = change(_decisions[index]);
        return Done(Results.Ok());
    }
}
