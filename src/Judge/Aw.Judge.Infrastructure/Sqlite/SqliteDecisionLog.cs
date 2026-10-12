using System.Text.Json;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>IDecisionLog over decisions.sqlite (the decisions, decision_details and failures tables).</summary>
/// <param name="database">The judge database.</param>
/// <param name="json">The host's Kernel JSON profile, for the JSON text columns.</param>
internal sealed class SqliteDecisionLog(JudgeDatabase database, JsonSerializerOptions json) : IDecisionLog
{
    private readonly DecisionJson _json = new(json);

    public async ValueTask<Result<Unit>> Record(DecisionRecord decision, CancellationToken cancellationToken)
    {
        var begun = await database.BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        var work = begun.Value;
        await using (work.ConfigureAwait(false))
        {
            var inserted = await SqliteQueries.Execute(work, DecisionSql.Insert(decision, _json), cancellationToken).ConfigureAwait(false);
            if (inserted.IsError)
            {
                return inserted.Error.Code == DataErrors.Constraint.Code
                    ? JudgeErrors.DecisionExists.With($"decision already recorded: {decision.Id.Value}")
                    : inserted.Error;
            }

            if (decision.Details is { } details)
            {
                var detailed = await SqliteQueries.Execute(work, DecisionSql.Insert(decision.Id, details, _json), cancellationToken).ConfigureAwait(false);
                if (detailed.IsError)
                {
                    return detailed.Error;
                }
            }

            return await work.Commit(cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask<Result<Unit>> RecordFailure(FailureRecord failure, CancellationToken cancellationToken) =>
        Write(DecisionSql.Insert(failure), cancellationToken).Map(static _ => default(Unit));

    public async ValueTask<Result<DecisionRecord>> Find(DecisionId id, CancellationToken cancellationToken)
    {
        var begun = await database.BeginRead(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var found = await SqliteQueries.One(begun.Value, DecisionSql.ById(DecisionSql.Find, id), new DecisionMapper(_json), cancellationToken).ConfigureAwait(false);
            return found.IsError && found.Error.Code == DataErrors.NotFound.Code ? Unknown(id) : found;
        }
    }

    public ValueTask<Result<Unit>> MarkUndone(DecisionId id, DateTimeOffset at, CancellationToken cancellationToken) =>
        Write(DecisionSql.Undo(id, at), cancellationToken).Bind(changed => OneRow(changed, id));

    public ValueTask<Result<Unit>> MarkApproved(DecisionId id, DateTimeOffset at, CancellationToken cancellationToken) =>
        Write(DecisionSql.Approve(id, at), cancellationToken).Bind(changed => OneRow(changed, id));

    public async ValueTask<Result<int>> CountFailuresSince(DateTimeOffset since, CancellationToken cancellationToken)
    {
        var begun = await database.BeginRead(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var statement = DecisionSql.At(DecisionSql.CountFailuresSince, "$since", since);
            return await SqliteQueries.One(begun.Value, statement, ScalarMapper.Count, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<Result<IReadOnlyList<DecisionOutcome>>> LastAttemptOutcomes(int count, CancellationToken cancellationToken)
    {
        var begun = await database.BeginRead(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            return await SqliteQueries.List(begun.Value, DecisionSql.Last(count), ScalarMapper.Outcome, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<Result<int>> PruneDetailsBefore(DateTimeOffset before, CancellationToken cancellationToken)
    {
        var begun = await database.BeginRead(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        Result<int> found;
        await using (begun.Value.ConfigureAwait(false))
        {
            found = await SqliteQueries.One(begun.Value, DecisionSql.At(DecisionSql.HasDetailsBefore, "$before", before), ScalarMapper.Count, cancellationToken).ConfigureAwait(false);
        }

        if (found.IsError || found.Value == 0)
        {
            return found;
        }

        return await Write(DecisionSql.At(DecisionSql.PruneDetailsBefore, "$before", before), cancellationToken).ConfigureAwait(false);
    }

    private static Result<DecisionRecord> Unknown(DecisionId id) => JudgeErrors.UnknownDecision.With($"unknown decision: {id.Value}");

    private static Result<Unit> OneRow(int changed, DecisionId id) =>
        changed == 0 ? JudgeErrors.UnknownDecision.With($"unknown decision: {id.Value}") : Results.Ok();

    // One statement in its own unit of work: the row count, committed.
    private async ValueTask<Result<int>> Write(SqlStatement statement, CancellationToken cancellationToken)
    {
        var begun = await database.BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        var work = begun.Value;
        await using (work.ConfigureAwait(false))
        {
            var changed = await SqliteQueries.Execute(work, statement, cancellationToken).ConfigureAwait(false);
            if (changed.IsError)
            {
                return changed;
            }

            var committed = await work.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? committed.Error : changed;
        }
    }
}
