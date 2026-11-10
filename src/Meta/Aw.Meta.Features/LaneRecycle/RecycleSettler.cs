using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneRecycle;

/// <summary>Settles the lane's open request for a recycle once the recycle worked: dismisses it like inbox-dismiss.</summary>
/// <param name="messages">The inbox.</param>
/// <param name="sync">Reads the meta's own transcript into the inbox before settling.</param>
/// <param name="clock">The current time.</param>
internal sealed class RecycleSettler(IMessageStore messages, InboxSync sync, TimeProvider clock)
{
    // The lane's open messages from before this run that ask for this recycle (a `DIGEST READY` line, or one naming this digest) are answered by it: dismiss them like `inbox-dismiss`.
    internal async ValueTask<Result<IReadOnlyList<string>>> Settle(RecyclePlan plan, DateTimeOffset started, CancellationToken cancellationToken)
    {
        var synced = await SyncMetaTranscript(cancellationToken).ConfigureAwait(false);
        if (synced.IsError)
        {
            return synced.Error;
        }

        var all = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (all.IsError)
        {
            return all.Error;
        }

        var now = clock.GetUtcNow();
        var reason = string.Create(CultureInfo.InvariantCulture, $"settled by lane-recycle {plan.Lane} at {now.UtcDateTime:yyyy-MM-dd HH:mm:ss}Z");
        var settled = new List<string>();
        foreach (var message in all.Value.Where(m => AsksForThisRecycle(m, plan, started, now)))
        {
            var resolved = await messages.Resolve(message.Id, new MessageResolution(MessageResolutionKind.Dismissed, reason), cancellationToken).ConfigureAwait(false);
            if (resolved.IsError)
            {
                return resolved.Error;
            }

            settled.Add(message.Id.Value);
        }

        return settled;
    }

    // The meta runs a recycle in the turn it reads the lane's DIGEST READY, and only its Stop hook syncs the inbox, so the
    // request is not stored yet (#2647): read the meta session's own transcript first, as stop-check would.
    private ValueTask<Result<Unit>> SyncMetaTranscript(CancellationToken cancellationToken)
    {
        // red (#2647): the meta's transcript is not synced yet.
        _ = (sync, messages, cancellationToken);
        return ValueTask.FromResult(Results.Ok());
    }

    // An open message (or one whose deferral ran out) the lane sent before this run, asking for this recycle.
    private static bool AsksForThisRecycle(InboxMessage m, RecyclePlan plan, DateTimeOffset started, DateTimeOffset now) =>
        m.At <= started
        && string.Equals(m.FromName, plan.Session, StringComparison.OrdinalIgnoreCase)
        && (m.Resolution is null || (m.Resolution is { Kind: MessageResolutionKind.Deferred } && !InboxState.IsDeferred(m, now)))
        && !NoticeRule.IsNotice(m.Body)
        && AsksForThisRecycle(m.Body, plan.Digest);

    private static bool AsksForThisRecycle(string body, string digest) =>
        body.TrimStart().StartsWith("DIGEST READY", StringComparison.OrdinalIgnoreCase)
        || (digest.Length > 0 && body.Contains(digest, StringComparison.Ordinal));
}
