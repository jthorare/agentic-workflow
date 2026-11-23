using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// The human's list (API spec §3.1, §3.6): the 'needs_you' rows in
/// <see cref="NeedsYouOrder"/>. With no row the sentence counts the running and queued items there are; stale views replace it. A decision row carries its pending decision, joined at read time, and the answer is tagged so an unchanged list answers 304 (#2789) before any folded row is read (#2882).
/// </summary>
/// <param name="views">The 'needs_you', 'decisions' and 'items' views.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
/// <param name="live">The rows read at request time, not from the view (the setup rows).</param>
/// <param name="json">The host's JSON options: the tag hashes each row as the client receives it.</param>
/// <param name="lease">The lease this serve process holds: its epoch goes into the tag (#3883).</param>
internal sealed class GetNeedsYouService(IViewReader views, IProjectorStatus status, ICallerContextAccessor callers, NeedsYouLiveRows live, JsonSerializerOptions json, HeldLease lease) : IService<GetNeedsYouRequest, GetNeedsYouResponse>
{
    public async ValueTask<Result<GetNeedsYouResponse>> Handle(GetNeedsYouRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the needs-you list needs a caller");
        }

        var current = status.Current;
        var setup = await live.Read(cancellationToken).ConfigureAwait(false);   // at most two rows, read at request time: hashed as served, so every input they read (jobs, profile, consent, the mcp switch) is in the tag
        var marks = await Marks(setup, current, cancellationToken).ConfigureAwait(false);
        if (marks.IsError)
        {
            return marks.Error;
        }

        var tag = TagOf(marks.Value, setup, current, json);
        if (request.IfNoneMatch is not null && string.Equals(request.IfNoneMatch, tag, StringComparison.Ordinal))
        {
            return new GetNeedsYouResponse([], Empty: null, current.AsOf, Tag: tag, NotModified: true);
        }

        // why: the marks are read before the rows, so a write between the two gives a body newer than its tag: the next poll misses once, never a stale 304
        var rows = await ReadRows(setup, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var empty = await EmptySentence(current, rows.Value, cancellationToken).ConfigureAwait(false);
        return empty.IsError ? empty.Error : new GetNeedsYouResponse(rows.Value, empty.Value, current.AsOf, tag);
    }

    /// <summary>The folded rows with the setup rows, sorted, each decision row joined to its pending decision.</summary>
    private async ValueTask<Result<List<NeedsYouRow>>> ReadRows(IReadOnlyList<NeedsYouRow> setup, CancellationToken cancellationToken)
    {
        var records = await views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, cancellationToken).ConfigureAwait(false);
        if (records.IsError)
        {
            return records.Error;
        }

        var folded = records.Value.Select(static r => r.Row).ToList();
        var seen = folded.Select(static r => r.Id).ToHashSet();
        var read = setup.Where(r => !seen.Contains(r.Id));   // a stalled run's row gives way to the park row the run's own failure folds
        return await JoinDecisions(NeedsYouOrder.Sort([.. folded, .. read]), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The serve process's lease epoch (#3883), then the marks of the views the answer reads (#2882): 'needs_you' (the rows),
    /// 'decisions' (the joined decision) and, only while the list can show its empty sentence, 'items' (that sentence's counts). Each
    /// mark is one aggregate query that reads no row and moves on any write or delete of its view.
    /// </summary>
    private async ValueTask<Result<string>> Marks(IReadOnlyList<NeedsYouRow> setup, ViewsStatus current, CancellationToken cancellationToken)
    {
        // why (#3883): a views rebuild replays the same ledger, so the marks come back the same even when a new build folds other row text; each serve takes the lease at a new epoch and keeps it while it runs
        var marks = new StringBuilder("serve=").Append((lease.Current?.Epoch ?? 0).ToString(CultureInfo.InvariantCulture)).Append(';');
        var rows = await Append(marks, ViewNames.NeedsYou, cancellationToken).ConfigureAwait(false);
        var decisions = rows.IsError ? rows : await Append(marks, ViewNames.Decisions, cancellationToken).ConfigureAwait(false);
        if (decisions.IsError)
        {
            return decisions;
        }

        if (!MayShowCounts(setup, current, rows.Value))
        {
            return marks.ToString();   // #3883: a list with rows (or a stale-views sentence) never shows the counts, so an item write keeps its tag
        }

        var items = await Append(marks, ViewNames.Items, cancellationToken).ConfigureAwait(false);
        return items.IsError ? items : marks.ToString();
    }

    /// <summary>Reads one view's mark and, when it reads, adds it to the tag text.</summary>
    private async ValueTask<Result<string>> Append(StringBuilder marks, string view, CancellationToken cancellationToken)
    {
        var mark = await views.Mark(view, cancellationToken).ConfigureAwait(false);
        if (!mark.IsError)
        {
            _ = marks.Append(view).Append('=').Append(mark.Value).Append(';');
        }

        return mark;
    }

    /// <summary>
    /// Whether the answer can be the items' empty sentence: no stale-views sentence, no setup row and no folded row, the last known from
    /// the needs_you mark's count without reading a row. A mark that names no count keeps the items in: a needless 200, never a stale 304.
    /// </summary>
    private static bool MayShowCounts(IReadOnlyList<NeedsYouRow> setup, ViewsStatus current, string rowsMark) =>
        current.Text is null && setup.Count == 0 && RowCount(rowsMark) is null or 0;

    /// <summary>The row count a view's mark ends with (<see cref="IViewReader.Mark"/>: '…:&lt;count&gt;'), or null when it names none.</summary>
    private static long? RowCount(string mark) =>
        long.TryParse(mark.AsSpan(mark.LastIndexOf(':') + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : null;

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

    /// <summary>
    /// The tag (#2882): a hash of every input of the answer, each read in O(1) rows. The serve's lease epoch and the marks of the views it reads stand for the
    /// folded rows, the joined decisions and the empty sentence's counts. The setup rows are read at request time, so each is hashed as
    /// the client receives it (every field). Then the projector's state word, its stale-views sentence (a rebuild's names the cursor)
    /// and whether the views are behind. Never the as-of time or the cursor alone: an event that writes none of the three views keeps
    /// the tag. No input moves with the clock: the folded rows are served as stored (a question's deadline is a fixed time in its text),
    /// and the turn-on row keeps its first sighting (#2938). A timeout's inSeconds would count down every second, so it hashes as 0.
    /// </summary>
    internal static string TagOf(string marks, IReadOnlyList<NeedsYouRow> setup, ViewsStatus current, JsonSerializerOptions json)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(current);
        var info = json.GetTypeInfo(typeof(NeedsYouRow));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(marks));
        foreach (var row in setup)
        {
            var hashed = row.Timeout is { } timeout ? row with { Timeout = timeout with { InSeconds = 0 } } : row;
            hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(hashed, info));
            hash.AppendData(";"u8);
        }

        var state = current.Stalled ? "stalled" : current.Rebuilding ? "rebuilding" : "ok";
        var behind = current.Cursor < current.LedgerHead;
        hash.AppendData(Encoding.UTF8.GetBytes($"|{current.Text}|{state}|{(behind ? "behind" : "current")}"));
        return Convert.ToHexStringLower(hash.GetHashAndReset().AsSpan(0, 16));
    }
}
