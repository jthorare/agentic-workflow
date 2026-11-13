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
/// <param name="obligations">Writes obligation.closed for each reply it settles; null to write none.</param>
internal sealed class RecycleSettler(IMessageStore messages, InboxSync sync, TimeProvider clock, ObligationEventWriter? obligations = null)
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
            var owed = obligations is not null && await obligations.IsOwed(message.Id.Value, cancellationToken).ConfigureAwait(false);
            var resolved = await messages.Resolve(message.Id, new MessageResolution(MessageResolutionKind.Dismissed, reason), cancellationToken).ConfigureAwait(false);
            if (resolved.IsError)
            {
                return resolved.Error;
            }

            if (owed && obligations is not null)
            {
                var who = message.FromName.Length > 0 ? message.FromName : message.From;
                await obligations.Closed(message.Id.Value, who, cancellationToken).ConfigureAwait(false);
            }

            settled.Add(message.Id.Value);
        }

        return settled;
    }

    // The meta runs a recycle in the turn it reads the lane's DIGEST READY, and only its Stop hook syncs the inbox, so the
    // request is not stored yet (#2647): read the meta session's own transcript first, as stop-check would.
    private async ValueTask<Result<Unit>> SyncMetaTranscript(CancellationToken cancellationToken)
    {
        // The live session carrying the registered name, found without an offsets row: the meta's first turn after /clear (#2683).
        var own = await sync.RunMeta(cancellationToken).ConfigureAwait(false);
        if (own.IsError)
        {
            return own.Error;
        }

        if (own.Value)
        {
            return Results.Ok();
        }

        // No resolver or no file found: fall back to the transcripts the store already knows by the role's session id.
        return await SyncKnownTranscripts(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> SyncKnownTranscripts(CancellationToken cancellationToken)
    {
        var role = await messages.Role(cancellationToken).ConfigureAwait(false);
        if (role.IsError)
        {
            return role.Error;
        }

        if (role.Value is not { } meta)
        {
            return Results.Ok();
        }

        var transcripts = await messages.Transcripts(cancellationToken).ConfigureAwait(false);
        if (transcripts.IsError)
        {
            return transcripts.Error;
        }

        var suffix = "/" + meta.Session.Value + ".jsonl";
        foreach (var transcript in transcripts.Value.Where(t => t.EndsWith(suffix, StringComparison.Ordinal)))
        {
            var read = await sync.Run(transcript, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }
        }

        return Results.Ok();
    }

    // An open message (or one whose deferral ran out) the lane sent before this run, asking for this recycle.
    private static bool AsksForThisRecycle(InboxMessage m, RecyclePlan plan, DateTimeOffset started, DateTimeOffset now) =>
        m.At <= started
        && string.Equals(m.FromName, plan.Session, StringComparison.OrdinalIgnoreCase)
        && (m.Resolution is null || (m.Resolution is { Kind: MessageResolutionKind.Deferred } && !InboxState.IsDeferred(m, now)))
        && !NoticeRule.IsNotice(m.Body)
        && AsksForThisRecycle(m.Body, plan.Digest);

    private const string DigestReady = "DIGEST READY";

    // A DIGEST READY line (after an optional `<lane> -> <lane>:` prefix and STATUS:/ACTION: tag, as the classifier reads it)
    // asks for this recycle when it names no path or names this digest; one for another path stays open (#2647). Any other
    // line asks for it only when it holds the digest's full path.
    private static bool AsksForThisRecycle(string body, string digest)
    {
        var text = NoticeRule.StripTag(NoticeRule.StripSender(body.Trim()));
        if (!text.StartsWith(DigestReady, StringComparison.OrdinalIgnoreCase))
        {
            return digest.Length > 0 && body.Contains(digest, StringComparison.Ordinal);
        }

        // "DIGEST READY: <path>", "DIGEST READY - <path>" and "DIGEST READY (<path>)." name the path too.
        var rest = text[DigestReady.Length..].TrimStart(':', '-', '\u2014', '\u2013', ' ', '\t');
        var named = rest.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.TrimEnd('.', ',', ';', ':').Trim('`', '"', '\'', '(', ')', '<', '>').TrimEnd('.', ',', ';', ':') ?? string.Empty;
        return named.Length == 0 || NamesDigest(named, digest);
    }

    // The same full path; a ~/ path names the digest when the digest ends with it after the ~ (the meta's HOME is not the
    // lane's to spell); a bare file name names it when the digest's file has that name.
    private static bool NamesDigest(string named, string digest)
    {
        if (named.StartsWith("~/", StringComparison.Ordinal))
        {
            return digest.EndsWith(named[1..], StringComparison.Ordinal);
        }

        if (named.Contains('/', StringComparison.Ordinal))
        {
            return string.Equals(named, digest, StringComparison.Ordinal);
        }

        return string.Equals(named, digest[(digest.LastIndexOf('/') + 1)..], StringComparison.Ordinal);
    }
}
