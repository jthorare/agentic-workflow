using Aw.Context.Core.Engine;
using Aw.Context.Core.Linking;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ContextCalls;

// The tick's last step (concept pins spec 3.7): an alias edit or a new current map queues a RelinkPinAliases job, but only when the earlier steps found nothing to do, so it never races a refresh or a publish.
internal sealed partial class ContextMapRefreshService
{
    internal static JobKind RelinkKind { get; } = JobKind.From("RelinkPinAliases");

    private async ValueTask<PublishOutcome> RelinkStep(ContextScope scope, Tick tick, CancellationToken cancellationToken)
    {
        var pins = PinAliasSet.Of(scope);
        var relinks = await jobs.List(state: null, RelinkKind, cancellationToken).ConfigureAwait(false);
        if (relinks.IsError || pins.IsError)
        {
            return new PublishOutcome(Started: false, Note: null, relinks.IsError ? relinks.Error : null);   // an unreadable profile is the other steps' to report
        }

        if (!PinRelinkDecision.ShouldStart(tick.State.Current, PinAliasSet.Hash(pins.Value), relinks.Value, tick.Refreshes.Concat(tick.Publishes), clock.GetUtcNow()))
        {
            return default;
        }

        var started = await starts.Relink(cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            if (started.Error.Code != SindriErrors.JobConflict.Code)
            {
                return new PublishOutcome(Started: false, Note: null, started.Error);
            }

            _ = Conflict(started.Error);   // 409: the relink is running (quiet), or another refusal that is logged and tried again next tick
            return default;
        }

        return new PublishOutcome(Started: true, $"ok: relink {started.Value.JobId.Value} started", Error: null);
    }
}
