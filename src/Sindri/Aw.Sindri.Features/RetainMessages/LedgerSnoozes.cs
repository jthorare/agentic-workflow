using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.RetainMessages;

/// <summary>
/// The rows with a live snooze, folded straight from the ledger's Home row events (messages spec §8.1, §9.4; Home spec §3.1): 'home.row.snoozed' starts or
/// replaces a snooze until its time, and 'home.row.woke', '.done' and '.cleared' end it. It never reads views.db, so `sindri views rebuild` or a stalled
/// projector can't make a snoozed thread look unsnoozed. Each payload names its row in 'id' and a snooze its end in 'until' (ISO-8601); a snooze without
/// 'until' holds until it ends. A payload it can't read is an error, so retention deletes nothing rather than guess.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
internal sealed class LedgerSnoozes(ILedgerReader ledger)
{
    internal const string Snoozed = "home.row.snoozed";
    internal const string Woke = "home.row.woke";
    internal const string Done = "home.row.done";
    internal const string Cleared = "home.row.cleared";

    private const int Page = 500;

    private static readonly LedgerFilter Filter = new(TypeFilter.Parse($"{Snoozed},{Woke},{Done},{Cleared}").Value, Item: null);

    /// <summary>The row ids whose snooze is live at <paramref name="now"/>.</summary>
    /// <param name="now">The time.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The ids, or the ledger's error, or SND-SCHED-001 for a payload it can't read.</returns>
    public async ValueTask<Result<IReadOnlySet<string>>> Live(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        var until = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        for (var cursor = 0L; cursor < head.Value;)
        {
            var scan = await ledger.Scan(cursor, Page, Filter, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                return scan.Error;
            }

            foreach (var row in scan.Value.Events)
            {
                var folded = Fold(until, row);
                if (folded.IsError)
                {
                    return folded.Error;
                }
            }

            if (scan.Value.ScannedTo <= cursor)
            {
                break;
            }

            cursor = scan.Value.ScannedTo;
        }

        return until.Where(s => s.Value > now).Select(static s => s.Key).ToHashSet(StringComparer.Ordinal);
    }

    private static Result<Unit> Fold(Dictionary<string, DateTimeOffset> until, LedgerEvent row)
    {
        string? id;
        DateTimeOffset? end = null;
        try
        {
            using var payload = JsonDocument.Parse(row.Envelope.Payload);
            id = payload.RootElement.TryGetProperty("id", out var idValue) && idValue.ValueKind == JsonValueKind.String ? idValue.GetString() : null;
            if (payload.RootElement.TryGetProperty("until", out var untilValue) && untilValue.ValueKind == JsonValueKind.String)
            {
                end = DateTimeOffset.Parse(untilValue.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            }
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return Unreadable(row, exception.GetType().Name);
        }

        if (string.IsNullOrEmpty(id))
        {
            return Unreadable(row, "no 'id'");
        }

        if (string.Equals(row.Envelope.Type, Snoozed, StringComparison.Ordinal))
        {
            until[id] = end ?? DateTimeOffset.MaxValue;
        }
        else
        {
            _ = until.Remove(id);
        }

        return Results.Ok();
    }

    private static Error Unreadable(LedgerEvent row, string why) => SindriErrors.ServiceFailed.With(
        string.Create(CultureInfo.InvariantCulture, $"messages-retention: the {row.Envelope.Type} event at sequence {row.Sequence} can't be read ({why}); nothing was deleted"));
}
