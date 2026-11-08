using System.Diagnostics.CodeAnalysis;
using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Features.LinearIssues;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="ILinearWriteScope"/>: an owner's scope over no settings and no repos.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because ILinearWriteScope is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class FakeLinearWriteScope : ILinearWriteScope
{
    /// <inheritdoc/>
    public ValueTask<Result<ContextScope>> Owner(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ContextScope>>(new ContextScope(Settings: null, [], new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Http)));
}
