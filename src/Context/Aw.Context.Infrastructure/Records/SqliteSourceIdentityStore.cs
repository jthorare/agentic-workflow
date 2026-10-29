using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

/// <summary>Who the owner is in each source, in context.db (migration 0020_project_fields): one 'source_identity' row per source.</summary>
internal sealed class SqliteSourceIdentityStore(ContextDatabase database) : ISourceIdentityStore
{
    public async ValueTask<Result<SourceIdentity?>> Read(SourceId source, CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : Results.Success<SourceIdentity?>(value: null);
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(read.Value, new SqlStatement(RecordSql.ReadIdentity, c => c.Bind("@source", source.Value)), RecordMappers.Identity, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
        }
    }

    public async ValueTask<Result<Unit>> Write(SourceIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var statement = new SqlStatement(RecordSql.WriteIdentity, c => c
                .Bind("@source", identity.Source.Value)
                .Bind("@userRef", identity.UserRef)
                .Bind("@name", identity.Name)
                .Bind("@readAt", identity.ReadAt));
            var done = await SqliteQueries.Execute(begun.Value, statement, cancellationToken).ConfigureAwait(false);
            var committed = done.IsError ? done.Error : await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            database.RestrictFiles();
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : Unit.Value;
        }
    }
}
