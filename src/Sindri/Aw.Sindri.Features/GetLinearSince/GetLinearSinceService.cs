using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Linear;

namespace Aw.Sindri.Features.GetLinearSince;

/// <summary>
/// The strip's changes (Linear hub spec 2026-11-04 §3, §4.3), owner only: a session is refused SND-AUTH-002. No marker reads from the start of what is stored; a marker that isn't one this route gave is SND-API-013.
/// The answer's marker is the newest change's instant, or the asked marker when nothing is newer, so passing it back answers no changes. While the owner's Linear user isn't known yet there is nothing to show: count 0.
/// </summary>
/// <param name="facts">The stored Linear data.</param>
/// <param name="callers">The current caller.</param>
internal sealed class GetLinearSinceService(ILinearSideFacts facts, ICallerContextAccessor callers) : IService<GetLinearSinceRequest, LinearSinceResponse>
{
    internal const string Refusal = "what changed on the owner's Linear issues is the owner's to read (fix: use the app)";

    public ValueTask<Result<LinearSinceResponse>> Handle(GetLinearSinceRequest request, CancellationToken cancellationToken)
    {
        _ = (facts, callers, request, cancellationToken, LinearSinceMarkers.Format(default));
        throw new NotSupportedException("red: the since read is not written yet");
    }
}
