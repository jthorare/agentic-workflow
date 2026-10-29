using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Testing.Records;

/// <summary>The in-memory <see cref="ISourceIdentityStore"/>: one identity per source behind a lock. It passes <see cref="SourceIdentityStoreContract{TStore}"/>.</summary>
public sealed class FakeSourceIdentityStore : ISourceIdentityStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<SourceId, SourceIdentity> _rows = [];

    /// <inheritdoc />
    public ValueTask<Result<SourceIdentity?>> Read(SourceId source, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<SourceIdentity?>>(_rows.GetValueOrDefault(source));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Write(SourceIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        lock (_gate)
        {
            _rows[identity.Source] = identity;
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }
}
