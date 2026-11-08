using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>The owner's scope over the approved profile, as <see cref="LinearScopes"/> builds it.</summary>
/// <param name="scopes">The scopes the Linear reads run under.</param>
internal sealed class ContextLinearWriteScope(LinearScopes scopes) : ILinearWriteScope
{
    public ValueTask<Result<ContextScope>> Owner(CancellationToken cancellationToken) => scopes.Read(owner: true, cancellationToken);
}
