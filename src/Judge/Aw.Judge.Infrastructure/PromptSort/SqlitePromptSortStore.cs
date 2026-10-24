using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.PromptSort;
using Aw.Judge.Infrastructure.Sqlite;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Judge.Infrastructure.PromptSort;

/// <summary>IPromptSortStore over the 'prompt_sort_axes' and 'prompt_sort_runs' tables (migration 0003) of decisions.sqlite.</summary>
/// <param name="database">The judge database.</param>
internal sealed class SqlitePromptSortStore(JudgeDatabase database) : IPromptSortStore
{
    /// <inheritdoc />
    public async ValueTask<Result<Unit>> Save(PromptSortRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        var begun = await database.BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        var work = begun.Value;
        await using (work.ConfigureAwait(false))
        {
            var statements = new[] { PromptSortSql.ClearAxes(record.Decision) }
                .Concat(record.Axes.Select(axis => PromptSortSql.Axis(record.Decision, axis)))
                .Append(PromptSortSql.Run(record.Decision, record.Run));
            foreach (var statement in statements)
            {
                var done = await SqliteQueries.Execute(work, statement, cancellationToken).ConfigureAwait(false);
                if (done.IsError)
                {
                    return done.Error;
                }
            }

            return await work.Commit(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<PromptSortRecord>> Find(DecisionId decision, CancellationToken cancellationToken)
    {
        var begun = await database.BeginRead(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var run = await SqliteQueries.One(begun.Value, PromptSortSql.FindRunOf(decision), PromptSortMappers.Run, cancellationToken).ConfigureAwait(false);
            if (run.IsError)
            {
                return run.Error.Code == DataErrors.NotFound.Code ? JudgeErrors.UnknownDecision.With($"the sorter kept nothing for decision {decision.Value}") : run.Error;
            }

            var axes = await SqliteQueries.List(begun.Value, PromptSortSql.Axes(decision), PromptSortMappers.Axis, cancellationToken).ConfigureAwait(false);
            return axes.IsError ? axes.Error : new PromptSortRecord(decision, axes.Value, run.Value);
        }
    }
}
