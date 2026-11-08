using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>The scope Linear writes run under: the owner's, over the approved profile.</summary>
internal interface ILinearWriteScope
{
    /// <summary>Reads the owner's scope.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scope, or the error when no profile is approved.</returns>
    public ValueTask<Result<ContextScope>> Owner(CancellationToken cancellationToken);
}
