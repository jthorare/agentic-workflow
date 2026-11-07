using Aw.Context.Core.Facade;
using Aw.Context.Core.Records;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearSideFacts;

/// <summary>The side-facts port over Context's IContextIssues (Linear hub spec 2026-11-04 §4.2, §4.3).</summary>
/// <param name="approvals">The approved profiles.</param>
/// <param name="callers">The current caller.</param>
/// <param name="engine">Context's facade.</param>
/// <param name="identities">Who the owner is in Linear.</param>
internal sealed class ContextLinearSideFacts(IApprovedProfile approvals, ICallerContextAccessor callers, IContextEngine engine, ISourceIdentityStore identities) : ILinearSideFacts
{
    public ValueTask<Result<LinearLookupsResponse>> Lookups(string team, CancellationToken cancellationToken)
    {
        _ = (approvals, callers, engine, identities, team, cancellationToken);
        throw new NotSupportedException("red: not written yet");
    }

    public ValueTask<Result<IReadOnlyList<LinearSinceItem>?>> Since(DateTimeOffset after, CancellationToken cancellationToken)
    {
        _ = (after, cancellationToken);
        throw new NotSupportedException("red: not written yet");
    }
}
