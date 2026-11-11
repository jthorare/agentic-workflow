using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>The write scope over the approved profile, as <see cref="LinearScopes.Write"/> builds it: the owner's, behind the caller's own check.</summary>
/// <param name="scopes">The scopes the Linear reads run under.</param>
internal sealed class ContextLinearWriteScope(LinearScopes scopes) : ILinearWriteScope
{
    public ValueTask<Result<ContextScope>> Write(CancellationToken cancellationToken) => scopes.Write(cancellationToken);
}
