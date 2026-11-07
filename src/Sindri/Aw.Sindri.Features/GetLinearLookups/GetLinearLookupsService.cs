using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Linear;

namespace Aw.Sindri.Features.GetLinearLookups;

/// <summary>
/// A team's pickers (Linear hub spec 2026-11-04 §3, §4.2): the stored states, members, labels and cycles, owner only. A session is refused SND-AUTH-002 before anything is read, and Context's own refusal maps to the same.
/// A team never synced is a 200 with a null sync time and empty lists, not an error.
/// </summary>
/// <param name="facts">The stored Linear data.</param>
/// <param name="callers">The current caller.</param>
internal sealed class GetLinearLookupsService(ILinearSideFacts facts, ICallerContextAccessor callers) : IService<GetLinearLookupsRequest, LinearLookupsResponse>
{
    internal const string Refusal = "Linear's teams, states and members are the owner's to read; a session reads issues through context_* (fix: use the app)";

    public ValueTask<Result<LinearLookupsResponse>> Handle(GetLinearLookupsRequest request, CancellationToken cancellationToken)
    {
        _ = (facts, callers, request, cancellationToken);
        throw new NotSupportedException("red: the lookups read is not written yet");
    }
}
