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
    // why: this runs after the mutation landed, so a cancel or a store failure must read as not stored (SND-LINEAR-013), never escape and get a landed write retried
    public async ValueTask<bool> Store(ContextScope scope, ExternalId issueId, LinearWrittenFields written, CancellationToken cancellationToken)
    {
        try
        {
            var stored = await handle(new LinearWriteThroughRequest(scope, issueId, written), CancellationToken.None).ConfigureAwait(false);
            return stored.IsSuccess && stored.Value.Key is not null;
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or TimeoutException)   // the write already landed in Linear; the next tick syncs it
        {
            return false;
        }
    }
}
