using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The project read (projects page spec §6.1).</summary>
internal sealed partial class SqliteRecordStore
{
    public async ValueTask<Result<IReadOnlyList<ProjectRow>>> ProjectFacts(SourceId source, RecordAudience audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audience);
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<IReadOnlyList<ProjectRow>>([]);
        }

        await using (session.ConfigureAwait(false))
        {
            var statement = new SqlStatement(RecordSql.ProjectFacts, c => c
                .Bind("@source", source.Value)
                .Bind("@audiences", SqliteLexicalLeg.JsonArray(audience.Allowed.Select(k => k.Value)))
                .Bind("@records", SqliteLexicalLeg.JsonArray(audience.Records.Select(k => k.Value))));
            var rows = await SqliteQueries.List(session, statement, RecordMappers.Project, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows;
        }
    }
}
