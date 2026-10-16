using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Features.DbRun;

/// <summary>Opens the database read-only and runs the vetted queries in order, stopping at the first the database refuses or that breaks.</summary>
internal sealed class DbQueryRunner(IDatabaseReader reader)
{
    internal const string Action = "query";

    /// <summary>Runs the queries; the error is why the database could not be opened (then no query ran).</summary>
    internal async ValueTask<Result<Unit>> RunAsync(VettedDb vetted, DbCollected collected, CancellationToken cancellationToken)
    {
        var opened = await reader.OpenAsync(vetted.Target, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        await using (opened.Value.ConfigureAwait(false))
        {
            for (var i = 0; i < vetted.Script.QueryList.Count; i++)
            {
                await RunOneAsync(opened.Value, vetted, i, collected, cancellationToken).ConfigureAwait(false);
            }
        }

        return Unit.Value;
    }

    private static async ValueTask RunOneAsync(IReadSession session, VettedDb vetted, int position, DbCollected collected, CancellationToken cancellationToken)
    {
        var (index, query) = (position + 1, vetted.Script.QueryList[position]);
        if (collected.Stopped)
        {
            collected.Skip(index, query.Sql);
            return;
        }

        var rows = await session.QueryAsync(query.Sql, vetted.Script.EffectiveRowLimit, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            var refused = string.Equals(rows.Error.Code, EvidenceErrors.DatabaseRefused.Code, StringComparison.Ordinal);
            collected.Stop(index, query.Sql, refused ? DbStepStatus.Refused : DbStepStatus.Broken, rows.Error);
            return;
        }

        collected.Record(index, query.Sql, query.Expect, rows.Value, vetted.Goldens[position]);
    }
}
