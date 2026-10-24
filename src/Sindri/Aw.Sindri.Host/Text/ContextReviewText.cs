using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Context.Core.Trust;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Text;

/// <summary>Plain text for the review verbs: words, never colour, so NO_COLOR and pipes read the same.</summary>
internal static partial class ContextReviewText
{
    // How many written override ids a result spells out (an apply-file or a multi-undo stays readable); a bulk accept past it gets a pointer instead.
    private const int ListedIds = 50;

    // How many skipped entries a result lists one per line; past it they're counted per reason, with this many ids as examples.
    private const int ListedSkips = 10;
    private const int SkipExamples = 5;

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    internal static string Queue(ReviewQueueView queue)
    {
        var counts = queue.Counts;
        var text = new StringBuilder(Invariant($"draft {queue.Draft} · {queue.Undecided} undecided ({counts.Deterministic} deterministic, {counts.Model} model, {counts.Orphaned} orphaned), {queue.Decided} decided, {queue.Proposals} proposals\n"));
        var cut = new[] { (queue.Truncated.Deterministic, "deterministic"), (queue.Truncated.Model, "model"), (queue.Truncated.Orphaned, "orphaned") }.Where(t => t.Item1).Select(t => t.Item2).ToList();
        if (cut.Count > 0)
        {
            _ = text.AppendLine($"showing the first entries of each group; cut: {string.Join(", ", cut)} (fix: raise --limit)");
        }

        if (queue.Entries.Count == 0)
        {
            return text.AppendLine("nothing left to decide: 'sindri context map promote' makes the draft current").ToString();
        }

        for (var i = 0; i < queue.Entries.Count; i++)
        {
            _ = text.Append(Entry(queue.Entries[i], i + 1, queue.Entries.Count));
        }

        return text.ToString();
    }

    internal static string Entry(ReviewEntry entry, int index, int total)
    {
        var files = entry.Files.Count == 0 ? string.Empty : "  files: " + string.Join(", ", entry.Files.Select(f => f.Value));
        var link = entry.Link is { } id ? $"  link: {id.Value}" : string.Empty;
        var orphan = entry.Override is { } o ? $"  override: {o.Value}" : string.Empty;
        var label = entry.Trust == Trust.Untrusted ? UntrustedFence.Wrap("map", author: null, entry.Label) : entry.Label;
        return Invariant($"{index}/{total} {entry.Group.ToString().ToLowerInvariant()} {entry.Confidence:0.00}  {label}  ({entry.Node.Value}, impact {entry.Impact}){files}{link}{orphan}\n");
    }

    internal static string History(ReviewHistoryView history)
    {
        if (history.Entries.Count == 0)
        {
            return "no decisions yet\n";
        }

        var text = new StringBuilder();
        foreach (var e in history.Entries)
        {
            _ = text.AppendLine(Invariant($"{e.Id.Value}  {e.Action.ToString().ToLowerInvariant()} {e.Link?.Value ?? e.Target.Value}{(e.Label is null ? string.Empty : $" '{e.Label}'")}  by {e.Who} at {e.At:O}{(e.Undoes is { } u ? $"  (undoes {u.Value})" : string.Empty)}{(e.Undone ? "  undone" : string.Empty)}"));
        }

        return (history.Truncated ? text.AppendLine("older decisions were cut (fix: raise --limit)") : text).ToString();
    }

    internal static string Result(string verb, ReviewResult result)
    {
        var text = new StringBuilder(Invariant($"{verb}: {result.Written.Count} {(result.Written.Count == 1 ? "row" : "rows")} written, {result.Undecided} undecided\n"));
        if (result.Written.Count is > 0 and <= ListedIds)
        {
            _ = text.Append("  ").AppendJoin(", ", result.Written.Select(w => w.Value)).AppendLine();
        }
        else if (result.Written.Count > ListedIds)
        {
            // a bulk accept writes thousands: the count and the verb that lists them, not a line of ids
            _ = text.AppendLine(Invariant($"  the newest {ReviewLimits.Max}: sindri context map review --history --limit {ReviewLimits.Max}; every id: add --json to this command"));
        }

        if (result.Skipped.Count <= ListedSkips)
        {
            foreach (var skipped in result.Skipped)
            {
                _ = text.Append("  skipped: ").AppendLine(skipped);
            }
        }
        else
        {
            Summarize(text, result.Skipped);
        }

        return text.ToString();
    }

    // A bulk apply skips thousands (links of a feature the same file rejected): one line per reason, the reason being the line with its
    // ids blanked, counted, with the first few ids; a reason with one entry keeps its line. --json carries every entry.
    private static void Summarize(StringBuilder text, IReadOnlyList<string> skipped)
    {
        foreach (var reason in skipped.GroupBy(static s => Ids.Replace(s, "…"), StringComparer.Ordinal).OrderByDescending(static g => g.Count()).ThenBy(static g => g.Key, StringComparer.Ordinal))
        {
            var count = reason.Count();
            _ = count == 1
                ? text.AppendLine(Invariant($"  skipped 1: {reason.First()}"))
                : text.AppendLine(Invariant($"  skipped {count}: {reason.Key}; e.g. {string.Join(", ", reason.Take(SkipExamples).Select(static s => Ids.Match(s).Value.TrimEnd(':')))}"));
        }

        _ = text.AppendLine("  every skipped entry: add --json to this command");
    }

    // A node key or link id in a skipped line ('feature:tabs', 'edge:0123…'); 'fix: …' isn't one, a space follows its colon.
    [GeneratedRegex(@"\b[a-z][a-z0-9-]*:[^\s,()']+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Ids { get; }

    internal static string Review(ReviewContextMapResponse response) =>
        Result("review", response.Result) + (response.Sequence is { } seq ? Invariant($"recorded as event {seq}\n") : string.Empty);

    internal static string Exported(ReviewExported exported) =>
        Invariant($"Exported the review queue ({exported.Entries} entries) to {exported.Path}\nedit the 'decision' of each entry, then: sindri context map review --apply {exported.Path}\n");

    internal static string Promote(PromoteContextMapResponse response) =>
        Invariant($"Promoted {response.Draft} to current for {response.Promoted.Version.Repo.Value} (v{response.Promoted.Version.Number}){(response.Promoted.Retained is { } old ? Invariant($"; v{old.Number} is retained") : string.Empty)}; recorded as event {response.Sequence}\n");

    internal static string Proposals(ProposalsView view, bool fence)
    {
        var text = new StringBuilder(Invariant($"run {view.Run} · map {view.Map}{(view.Draft is { } d ? $" · draft {d}" : string.Empty)}\n"));
        foreach (var s in view.Surfaces)
        {
            _ = text.AppendLine($"  {s.Status.ToString().ToLowerInvariant(),-9} {Fenced(s, fence)}{(s.Node is { } node ? " → " + node.Value : string.Empty)}");
        }

        return text.ToString();
    }

    private static string ProposedHeader(ProposeResult result, string recorded)
    {
        if (result.Draft is not { } draft)
        {
            return "No draft map: nothing to withdraw\n";
        }

        var lead = result.Withdrawn > 0 || result.Kept > 0 ? Invariant($"Withdrew {result.Withdrawn} undecided proposals ({result.Kept} a person decided stay) in draft") : "Proposed into draft";
        return Invariant($"{lead} v{draft.Number}: {result.Added} added, {result.Existing} already mapped, {result.Rejected} rejected earlier{recorded}\n");
    }

    internal static string Proposed(ProposeContextMapResponse response, bool fence)
    {
        var recorded = response.Sequence is { } seq ? Invariant($"; recorded as event {seq}") : string.Empty;
        var result = response.Result;
        var text = new StringBuilder(ProposedHeader(result, recorded));
        foreach (var s in response.Surfaces)
        {
            _ = text.AppendLine($"  {s.Status.ToString().ToLowerInvariant(),-9} {Fenced(s, fence)}{(s.Node is { } node ? " → " + node.Value : string.Empty)}");
        }

        return text.ToString();
    }

    // A human's answer carries the clean name beside its trust, so the text fences it itself; a session's answer was fenced by the server already.
    private static string Fenced(GraphStatus status, bool fence) => fence && status.Trust == Trust.Untrusted ? UntrustedFence.Wrap("map", author: null, status.Surface) : status.Surface;
}
