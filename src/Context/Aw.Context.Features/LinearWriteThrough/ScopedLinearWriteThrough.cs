using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Features.LinearWriteThrough;

/// <summary>The <see cref="ILinearWriteThrough"/> over the slice: it runs the slice in a scope of its own, because its caller is a singleton and the slice is scoped.</summary>
/// <param name="scopes">Makes the scope.</param>
internal sealed class ScopedLinearWriteThrough(IServiceScopeFactory scopes) : ILinearWriteThrough
{
    /// <inheritdoc />
    public async ValueTask<bool> Store(ContextScope scope, ExternalId issueId, LinearWrittenFields written, CancellationToken cancellationToken)
    {
        using var through = scopes.CreateScope();
        var service = through.ServiceProvider.GetRequiredService<IService<LinearWriteThroughRequest, LinearWriteThroughResponse>>();
        var stored = await service.Handle(new LinearWriteThroughRequest(scope, issueId, written), cancellationToken).ConfigureAwait(false);
        return stored.IsSuccess && stored.Value.Key is not null;
    }
}
