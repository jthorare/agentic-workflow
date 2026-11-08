using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Core.Linear;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="ILinearCheckStore"/>; passes <see cref="LinearCheckStoreContract"/>.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because the port is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can not see.")]
internal sealed class FakeLinearCheckStore : ILinearCheckStore
{
    private LinearStoredChecks _checks = new(Read: null, Write: null);

    /// <inheritdoc/>
    public ValueTask<Result<LinearStoredChecks>> Read(CancellationToken cancellationToken) => ValueTask.FromResult<Result<LinearStoredChecks>>(_checks);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> RecordRead(LinearStoredCheck check, CancellationToken cancellationToken)
    {
        _checks = _checks with { Read = check };
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> RecordWrite(LinearStoredCheck check, CancellationToken cancellationToken)
    {
        _checks = _checks with { Write = check };
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> ClearWrite(CancellationToken cancellationToken)
    {
        _checks = _checks with { Write = null };
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
