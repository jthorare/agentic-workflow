using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.Launching;

/// <summary>Reads the ledger from the launcher's cursor (UI spec E-1): a request with no outcome becomes pending, an outcome settles its launch.</summary>
/// <param name="ledger">The ledger.</param>
/// <param name="events">Reads the payloads.</param>
/// <param name="work">What the launcher remembers.</param>
internal sealed class LaunchIntake(ILedgerStore ledger, EventFactory events, LaunchWork work)
{
    private const int Page = 200;

    /// <summary>Reads every event recorded since the cursor.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Success, or the read's or a payload's error.</returns>
    public async ValueTask<Result<Unit>> Read(CancellationToken cancellationToken)
    {
        while (true)
        {
            var page = await ledger.ReadAfter(work.Cursor, Page, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var recorded in page.Value)
            {
                var noted = Note(recorded);
                if (noted.IsError)
                {
                    return noted.Error;
                }
            }

            if (page.Value.Count > 0)
            {
                work.Cursor = page.Value[^1].Sequence;
            }

            if (page.Value.Count < Page)
            {
                return Unit.Value;
            }
        }
    }

    private Result<Unit> Note(LedgerEvent recorded)
    {
        switch (recorded.Envelope.Type)
        {
            case EventTypes.ItemOpenRequested:
                var request = events.Payload<ItemOpenRequested>(recorded);
                if (request.IsError)
                {
                    return request.Error;
                }

                work.Add(new PendingLaunch(request.Value, recorded.Sequence, recorded.Envelope.OccurredAt));
                break;
            case EventTypes.LaunchStarted:
                var started = events.Payload<LaunchStarted>(recorded);
                if (started.IsError)
                {
                    return started.Error;
                }

                work.Settle(started.Value.Launch);
                break;
            case EventTypes.LaunchFailed:
                var failed = events.Payload<LaunchFailed>(recorded);
                if (failed.IsError)
                {
                    return failed.Error;
                }

                work.Settle(failed.Value.Launch);
                break;
            default:
                break;
        }

        return Unit.Value;
    }
}
