using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.LinearWriteThrough;

/// <summary>The <see cref="ILinearWriteThrough"/> over the slice. Its caller is a singleton and the slice is scoped, so the registration hands it a handler that runs the slice in a scope of its own.</summary>
/// <param name="handle">Runs the slice in a fresh scope.</param>
internal sealed class ScopedLinearWriteThrough(Func<LinearWriteThroughRequest, CancellationToken, ValueTask<Result<LinearWriteThroughResponse>>> handle) : ILinearWriteThrough
{
    /// <inheritdoc />
    public async ValueTask<bool> Store(ContextScope scope, ExternalId issueId, LinearWrittenFields written, CancellationToken cancellationToken)
    {
        var stored = await handle(new LinearWriteThroughRequest(scope, issueId, written), cancellationToken).ConfigureAwait(false);
        return stored.IsSuccess && stored.Value.Key is not null;
    }
}
