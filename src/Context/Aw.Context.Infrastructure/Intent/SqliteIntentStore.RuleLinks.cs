using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Intent;

/// <summary>One rule's links of a record, replaced without touching its other links or the map version it was linked under (concept pins spec §3.4).</summary>
internal sealed partial class SqliteIntentStore
{
    public ValueTask<Result<Unit>> ReplaceRuleLinks(RecordKey record, LinkRule rule, IReadOnlyList<IntentLink> links, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(links);
        return Write(async (session, token) =>
        {
            var dropped = await SqliteQueries.Execute(session, new SqlStatement(IntentSql.DeleteRuleLinks, c => c.Bind("@record", record.Value).Bind("@rule", IntentMappers.Of(rule))), token).ConfigureAwait(false);
            if (dropped.IsError)
            {
                return dropped.Error;
            }

            foreach (var link in links)
            {
                var done = await SqliteQueries.Execute(session, new SqlStatement(IntentSql.InsertRuleLink, c => c.Bind("@record", record.Value).Bind("@target", link.Target.Value).Bind("@rule", IntentMappers.Of(rule))
                    .Bind("@confidence", link.Confidence).Bind("@span", link.SpanHash)), token).ConfigureAwait(false);
                if (done.IsError)
                {
                    return done.Error;
                }
            }

            return Unit.Value;
        }, cancellationToken);
    }
}
