using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Sqlite;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Judge.Infrastructure.Eval;

/// <summary>IEvalStore over the 'eval_items' and 'labels' tables (migration 0004) of decisions.sqlite, and the decision tables its reads start from.</summary>
/// <param name="database">The judge database.</param>
/// <param name="redactor">Removes secrets from an item's input before it is stored.</param>
/// <param name="json">The host's Kernel JSON profile, for writing the capped input.</param>
internal sealed class SqliteEvalStore(JudgeDatabase database, IInputRedactor redactor, JsonSerializerOptions json) : IEvalStore
{
    // Strongest first: a hand-set label beats what the user did next, which beats a model's majority.
    private static readonly LabelSource[] _strongestFirst = [LabelSource.Override, LabelSource.Outcome, LabelSource.Adjudicator];

    /// <inheritdoc />
    public async ValueTask<Result<bool>> AddItem(EvalItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        // Redact the parsed values and keys first, then cap, so a secret cut at the cap can't survive.
        var parsed = InputJson.TryParse(item.InputJson, out var node) ? node : JsonValue.Create(item.InputJson);
        var stored = InputCap.Write(redactor.Redact(parsed), InputCap.MaxChars, json);
        return await Write(EvalSql.Insert(item, stored), cancellationToken).Map(static changed => changed == 1).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<EvalItem>> FindItem(EvalItemId id, CancellationToken cancellationToken)
    {
        var found = await Read(session => SqliteQueries.One(session, EvalSql.Find(id), EvalMappers.Item, cancellationToken), cancellationToken).ConfigureAwait(false);
        return found.IsError && found.Error.Code == DataErrors.NotFound.Code ? Unknown(id) : found;
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> RecordLabel(EvalItemId item, LabelSource source, string label, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var begun = await database.BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        var work = begun.Value;
        await using (work.ConfigureAwait(false))
        {
            var exists = await SqliteQueries.Exists(work, EvalSql.Exists(item), cancellationToken).ConfigureAwait(false);
            if (exists.IsError)
            {
                return exists.Error;
            }

            if (!exists.Value)
            {
                return Unknown(item).Error;
            }

            var written = await SqliteQueries.Execute(work, EvalSql.Label(item, source, label, at), cancellationToken).ConfigureAwait(false);
            return written.IsError ? written.Error : await work.Commit(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<EvalItem?>> NextUnlabeled(string? question, LabelSource? source, IReadOnlySet<EvalItemId> exclude, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exclude);
        var found = await Read(session => SqliteQueries.List(session, EvalSql.Next(question, source, exclude), EvalMappers.Item, cancellationToken), cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : Results.Success(found.Value.Count == 0 ? null : found.Value[0]);
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<LabeledItem>>> Labeled(string question, LabelSource? source, CancellationToken cancellationToken)
    {
        var rows = await Read(session => SqliteQueries.List(session, EvalSql.LabeledOf(question, source), EvalMappers.Labeled, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        // The rows are oldest first; an item keeps its strongest label and its place in that order.
        IReadOnlyList<LabeledItem> best = [.. rows.Value
            .GroupBy(static r => r.Item.Id)
            .Select(static g => g.OrderBy(static r => Array.IndexOf(_strongestFirst, r.Source)).First())
            .OrderBy(static r => r.Item.CreatedAt).ThenBy(static r => r.Item.Id.Value, StringComparer.Ordinal)];
        return Results.Success(best);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<LabelCount>>> Counts(CancellationToken cancellationToken) =>
        Read(session => SqliteQueries.List(session, new SqlStatement(EvalSql.Counts), EvalMappers.Count, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<LabelAgreement>> Agreement(string question, CancellationToken cancellationToken) =>
        Read(session => SqliteQueries.One(session, EvalSql.AgreementOf(question), EvalMappers.Agreement, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<DecidedInput>>> DecidedSince(string? question, DateTimeOffset since, CancellationToken cancellationToken) =>
        Read(session => SqliteQueries.List(session, EvalSql.Decided(question, since), EvalMappers.Decided, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<OutcomeCandidate>>> OutcomeCandidates(CancellationToken cancellationToken) =>
        Read(session => SqliteQueries.List(session, new SqlStatement(EvalSql.OutcomeCandidates), EvalMappers.Candidate, cancellationToken), cancellationToken);

    private static Result<EvalItem> Unknown(EvalItemId id) => JudgeErrors.UnknownItem.With($"unknown item: {id.Value}");

    private async ValueTask<Result<T>> Read<T>(Func<ISqliteSession, ValueTask<Result<T>>> query, CancellationToken cancellationToken)
    {
        var begun = await database.BeginRead(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            return await query(begun.Value).ConfigureAwait(false);
        }
    }

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
