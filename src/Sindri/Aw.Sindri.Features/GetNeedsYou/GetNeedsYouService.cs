using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// The human's list (API spec §3.1, §3.6): the 'needs_you' rows in
/// <see cref="NeedsYouOrder"/>. With no row the sentence counts the running and queued items there are; stale views replace it. A decision row carries its pending decision, joined at read time, and the answer is tagged so an unchanged list answers 304 (#2789).
/// </summary>
/// <param name="views">The 'needs_you', 'decisions' and 'items' views.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
/// <param name="live">The rows read at request time, not from the view (the setup rows).</param>
internal sealed class GetNeedsYouService(IViewReader views, IProjectorStatus status, ICallerContextAccessor callers, NeedsYouLiveRows live) : IService<GetNeedsYouRequest, GetNeedsYouResponse>
{
    public async ValueTask<Result<GetNeedsYouResponse>> Handle(GetNeedsYouRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the needs-you list needs a caller");
        }

        var current = status.Current;
        var records = await views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, cancellationToken).ConfigureAwait(false);
        if (records.IsError)
        {
            return records.Error;
        }

        var folded = records.Value.Select(static r => r.Row).ToList();
        var seen = folded.Select(static r => r.Id).ToHashSet();
        var read = (await live.Read(cancellationToken).ConfigureAwait(false)).Where(r => !seen.Contains(r.Id));   // a stalled run's row gives way to the park row the run's own failure folds
        var sorted = NeedsYouOrder.Sort([.. folded, .. read]);
        var joined = await JoinDecisions(sorted, cancellationToken).ConfigureAwait(false);
        if (joined.IsError)
        {
            return joined.Error;
        }

        var rows = joined.Value;
        var empty = await EmptySentence(current, rows, cancellationToken).ConfigureAwait(false);
        if (empty.IsError)
        {
            return empty.Error;
        }

        var tag = TagOf(rows, empty.Value, current);
        return string.Equals(request.IfNoneMatch, tag, StringComparison.Ordinal)
            ? new GetNeedsYouResponse([], Empty: null, current.AsOf, Tag: tag, NotModified: true)
            : new GetNeedsYouResponse(rows, empty.Value, current.AsOf, tag);
    }

    private async ValueTask<Result<string?>> EmptySentence(ViewsStatus current, List<NeedsYouRow> rows, CancellationToken cancellationToken)
    {
        if (current.Text is not null || rows.Count > 0)
        {
            return current.Text;
        }

        var counted = await Counts(cancellationToken).ConfigureAwait(false);
        if (counted.IsError)
        {
            return counted.Error;
        }

        return EmptyText.NeedsYou(counted.Value.Running, counted.Value.Queued);
    }

    /// <summary>Gives each decision row its pending decision, read once for all rows; a row with no pending decision keeps null.</summary>
    private async ValueTask<Result<List<NeedsYouRow>>> JoinDecisions(IReadOnlyList<NeedsYouRow> rows, CancellationToken cancellationToken)
    {
        if (!rows.Any(static r => r.Kind == NeedsYouKind.Decision))
        {
            return rows.ToList();
        }

        var pending = await views.Where<HumanDecisionRow>(ViewNames.Decisions, ViewColumn.State, "pending", cancellationToken).ConfigureAwait(false);
        if (pending.IsError)
        {
            return pending.Error;
        }

        var byId = new Dictionary<string, NeedsYouDecision>(StringComparer.Ordinal);
        foreach (var row in pending.Value)
        {
            var decision = NeedsYouDecisions.From(row);
            if (decision.IsError)
            {
                return decision.Error;
            }

            byId[row.Id.Value] = decision.Value;
        }

        List<NeedsYouRow> joined = [.. rows.Select(r => r.Kind == NeedsYouKind.Decision && byId.TryGetValue(r.Subject.Id.Value, out var d) ? r with { Decision = d } : r)];
        return joined;
    }

    private async ValueTask<Result<(int Running, int Queued)>> Counts(CancellationToken cancellationToken)
    {
        var running = 0;
        var queued = 0;
        foreach (var state in Enum.GetValues<ItemState>())
        {
            var group = QueueGroups.GroupOf(state);
            if (group is not (QueueGroup.Running or QueueGroup.Queued))
            {
                continue;
            }

            var read = await views.Where<ItemRow>(ViewNames.Items, ViewColumn.State, ItemStateNames.Of(state), cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            if (group == QueueGroup.Running)
            {
                running += read.Value.Count;
            }
            else
            {
                queued += read.Value.Count;
            }
        }

        return (running, queued);
    }

    /// <summary>A hash of what the answer shows: each row's content and its decision's option ids, the empty sentence, the projector's state word and whether the views are behind; never the as-of time or the cursor.</summary>
    private static string TagOf(IReadOnlyList<NeedsYouRow> rows, string? empty, ViewsStatus current)
    {
        var text = new StringBuilder();
        foreach (var row in rows)
        {
            AppendRow(text, row);
        }

        var state = current.Stalled ? "stalled" : current.Rebuilding ? "rebuilding" : "ok";
        var behind = current.Cursor < current.LedgerHead;
        _ = text.Append('|').Append(empty).Append('|').Append(state).Append('|').Append(behind ? "behind" : "current");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return Convert.ToHexStringLower(hash.AsSpan(0, 16));
    }

    private static void AppendRow(StringBuilder text, NeedsYouRow row)
    {
        _ = text.Append(row.Id.Value).Append('~').Append(row.Kind).Append('~').Append(row.Label).Append('~').Append(row.Title).Append('~').Append(row.Reason)
            .Append('~').Append(row.Urgency).Append('~').Append(row.Since.UtcTicks.ToString(CultureInfo.InvariantCulture)).Append('~').Append(row.RecordedOnly)
            .Append('~').Append(row.PrimaryAction?.Value).AppendJoin(',', row.Actions.Select(static a => a.Value)).Append('~').Append(row.Cli)
            .Append('~').Append(row.Stop).Append('~').Append(row.Timeout?.Kind).Append(row.Timeout?.On).Append('~').Append(row.Sandbox);
        if (row.Decision is { } decision)
        {
            _ = text.Append("~d:").Append(decision.Id.Value).Append(':').Append(decision.Question).Append(':').AppendJoin(',', decision.Options.Select(static o => o.Id));
        }

        _ = text.Append(';');
    }
}
