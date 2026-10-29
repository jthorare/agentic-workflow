using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Records;

/// <summary>Who the owner is in each source, in context.db (migration 0020_project_fields): one 'source_identity' row per source.</summary>
internal sealed class SqliteSourceIdentityStore(ContextDatabase database) : ISourceIdentityStore
{
    public ValueTask<Result<SourceIdentity?>> Read(SourceId source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        throw new NotSupportedException();
    }

    public ValueTask<Result<Unit>> Write(SourceIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        throw new NotSupportedException();
    }
}
