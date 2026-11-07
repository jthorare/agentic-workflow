using Aw.Context.Core;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
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
    private const int Shown = 40;

    public async ValueTask<Result<LinearSinceResponse>> Handle(GetLinearSinceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current?.Class is not (CallerClass.Human or CallerClass.Service))
        {
            return SindriErrors.AuthRefused.With(Refusal);
        }

        var after = Marker(request.After);
        if (after.IsError)
        {
            return after.Error;
        }

        var read = await facts.Since(after.Value, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return Refused(read.Error);
        }

        var items = read.Value ?? [];
        return new LinearSinceResponse(LinearSinceMarkers.Format(items.Count == 0 ? after.Value : items.Max(static i => i.At)), items.Count, items);
    }

    private static Result<DateTimeOffset> Marker(string? asked)
    {
        if (string.IsNullOrEmpty(asked))
        {
            return DateTimeOffset.MinValue;
        }

        if (LinearSinceMarkers.TryParse(asked, out var at))
        {
            return at;
        }

        var text = asked.Length > Shown ? asked[..Shown] + "…" : asked;
        return SindriErrors.InvalidQuery.With($"after is a marker this route gave ('lin_' and 16 hex digits), not '{text}'");
    }

    // why: Context refuses a session with SND-CONTEXT-020; on this owner-only route that is SND-AUTH-002, as the caller check above
    private static Error Refused(Error error) =>
        string.Equals(error.Code, ContextErrors.SessionsDisabled.Code, StringComparison.Ordinal) ? SindriErrors.AuthRefused.With(Refusal) : error;
}
