using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.History;

/// <summary>The pure rules of the history lens: the 'before' and 'since' cut, the union across anchors, the order (S-H decisions 3, 5, 6).</summary>
public static class HistoryCut
{
    /// <summary>Cuts and orders records and decisions. The same item reached by two anchors becomes one entry listing both.</summary>
    /// <param name="records">Records, one per (record, anchor) or already unioned.</param>
    /// <param name="decisions">Decisions, likewise.</param>
    /// <param name="window">The window.</param>
    /// <returns>The kept lists and counts.</returns>
    public static HistoryCutResult Apply(IReadOnlyList<HistoryRecord> records, IReadOnlyList<HistoryDecision> decisions, HistoryWindow window)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(window);
        var (keptRecords, outcomes, droppedRecords) = CutRecords(records, window);
        var (cutDecisions, droppedDecisions) = CutDecisions(decisions, window, outcomes);
        var orderedRecords = keptRecords.OrderByDescending(r => r.UpdatedAt.HasValue).ThenByDescending(r => r.UpdatedAt).ThenBy(r => r.Key.Value, StringComparer.Ordinal).ToList();
        var orderedDecisions = cutDecisions
            .Select(d => d with { Supersedes = [.. d.Supersedes.Where(s => !droppedDecisions.Contains(s.Value))] })
            .OrderByDescending(d => d.At.HasValue).ThenByDescending(d => d.At).ThenBy(d => d.Node.Value, StringComparer.Ordinal).ToList();
        return new HistoryCutResult(orderedRecords, orderedDecisions, orderedRecords.Count, orderedDecisions.Count, droppedRecords + droppedDecisions.Count);
    }

    /// <summary>Unions changes by repo and sha, orders them.</summary>
    /// <param name="changes">The changes, possibly one per anchor.</param>
    /// <returns>The list.</returns>
    public static HistoryChangeList Changes(IReadOnlyList<HistoryChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var merged = changes.GroupBy(c => (c.Repo.Value, c.Sha)).Select(g => g.First() with { Anchors = Union(g.SelectMany(c => c.Anchors)) })
            .OrderByDescending(c => c.At).ThenBy(c => c.Repo.Value, StringComparer.Ordinal).ThenBy(c => c.Sha, StringComparer.Ordinal).ToList();
        return new HistoryChangeList(merged, merged.Count);
    }

    // Each record's outcome by key: leaky or not; null = dropped by the 'before' cut (a decision that quotes it needs to know).
    private static (List<HistoryRecord> Kept, Dictionary<string, bool?> Outcomes, int Dropped) CutRecords(IReadOnlyList<HistoryRecord> records, HistoryWindow window)
    {
        var dropped = 0;
        var outcomes = new Dictionary<string, bool?>(StringComparer.Ordinal);
        var kept = new List<HistoryRecord>();
        foreach (var record in records.GroupBy(r => r.Key.Value, StringComparer.Ordinal).Select(g => g.First() with { Anchors = Union(g.SelectMany(r => r.Anchors)) }))
        {
            if (record.CreatedAt > window.Before)
            {
                dropped++;
                outcomes[record.Key.Value] = null;
                continue;
            }

            var leaky = window.Before is { } cut && (record.CreatedAt is null || record.UpdatedAt is null || record.UpdatedAt > cut);
            outcomes[record.Key.Value] = leaky;
            if (!Aged(record, window))
            {
                kept.Add(record with { Leaky = leaky });
            }
        }

        return (kept, outcomes, dropped);
    }

    private static (List<HistoryDecision> Kept, HashSet<string> Dropped) CutDecisions(IReadOnlyList<HistoryDecision> decisions, HistoryWindow window, Dictionary<string, bool?> outcomes)
    {
        var kept = new List<HistoryDecision>();
        var dropped = new HashSet<string>(StringComparer.Ordinal);
        foreach (var decision in decisions.GroupBy(d => d.Node.Value, StringComparer.Ordinal).Select(g => g.First() with { Anchors = Union(g.SelectMany(d => d.Anchors)) }))
        {
            if (Decide(decision, window, outcomes) is { } found)
            {
                kept.Add(found);
            }
            else
            {
                _ = dropped.Add(decision.Node.Value);
            }
        }

        return (kept, dropped);
    }

    // 'since' cuts what ages: a record by its update (its creation when the update is unknown); an unknown time is kept.
    private static bool Aged(HistoryRecord record, HistoryWindow window) => window.Since is { } since && (record.UpdatedAt ?? record.CreatedAt) < since;

    // A decision made by a person is cut by its own instant (no instant: kept, leaky). One a model or the extractor made follows the record it quotes, because its own instant is the extraction run, not the decision.
    private static HistoryDecision? Decide(HistoryDecision decision, HistoryWindow window, Dictionary<string, bool?> outcomes)
    {
        if (window.Before is not { } before)
        {
            return decision with { Leaky = false };
        }

        if (decision.Provenance == Provenance.Human)
        {
            return decision.At > before ? null : decision with { Leaky = decision.At is null };   // an unknown time can't vouch for the cut, as with a record
        }

        return decision.Quote is { } quote && outcomes.TryGetValue("record:" + quote.Record.Value, out var leaky)
            ? leaky is { } known ? decision with { Leaky = known } : null
            : decision with { Leaky = true };
    }

    private static List<NodeKey> Union(IEnumerable<NodeKey> keys) => [.. keys.DistinctBy(k => k.Value, StringComparer.Ordinal).OrderBy(k => k.Value, StringComparer.Ordinal)];
}
