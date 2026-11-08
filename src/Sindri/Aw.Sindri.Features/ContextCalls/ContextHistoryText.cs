using System.Globalization;
using System.Text;
using Aw.Context.Core.History;
using static Aw.Sindri.Features.ContextCalls.ContextLensText;

namespace Aw.Sindri.Features.ContextCalls;

#pragma warning disable MA0182 // used by Host's verbs through InternalsVisibleTo; MA0182 only looks inside this assembly
/// <summary>
/// Plain text for 'context history': every state is a word ('ok', 'not-configured', 'leaky'), never colour. A human's answer carries clean labels beside their trust, so the text
/// fences the untrusted ones itself; a session's answer was fenced by the server already.
/// </summary>
internal static class ContextHistoryText
{
    internal static string History(HistoryView view, bool fence)
    {
        var text = new StringBuilder(Header(view, fence));
        foreach (var missing in view.MissingAnchors)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"missing anchor: {missing}");
        }

        Changes(text, view, fence);
        Intent(text, view, fence);
        _ = text.AppendLine(Invariant($"incidents: {StateWord(view.Incidents.State)} ({view.Incidents.Reason})"));
        Footer(text, view);
        _ = text.Append(Searched(view.Searched)).Append(Coverage(view.Coverage));
        return text.ToString();
    }

    private static string Header(HistoryView view, bool fence)
    {
        var anchors = string.Join(", ", view.Anchors.Select(a => Invariant($"{a.Key.Value} ({Fenced(a.Label, a.Trust, fence)})")));
        var since = Instant(view.Window.Since, "the beginning");
        var before = Instant(view.Window.Before, "now");
        return Invariant($"history of {view.Anchors.Count} {(view.Anchors.Count == 1 ? "anchor" : "anchors")} ({anchors}) · map {view.MapVersion}{(view.Draft ? " (draft)" : string.Empty)} · since {since} · before {before}\n");
    }

    private static void Footer(StringBuilder text, HistoryView view)
    {
        foreach (var git in view.Git)
        {
            _ = text.AppendLine(Invariant($"git {git.Repo.Value}: head {git.Head}{(git.UpTo is null ? string.Empty : " up to " + git.UpTo)}{(git.Shallow ? " (shallow clone)" : string.Empty)}, {git.Files} files searched, {git.Changes} changes"));
        }

        var c = view.Counts;
        _ = text.AppendLine(Invariant($"counts: {c.Records} records, {c.Decisions} decisions, {c.Changes} changes, {c.Files} files ({c.Unlocated} unlocated), {c.AfterCut} dropped by the cut"));
        var cut = new[] { ("records", view.Truncated.Records), ("decisions", view.Truncated.Decisions), ("changes", view.Truncated.Changes), ("files", view.Truncated.Files) }.Where(static t => t.Item2).Select(static t => t.Item1).ToList();
        if (cut.Count > 0)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"truncated: {string.Join(", ", cut)}");
        }

        foreach (var line in view.Degraded)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"degraded: {line}");
        }
    }

    private static string Instant(DateTimeOffset? at, string none) => at is { } value ? value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) : none;

    private static void Changes(StringBuilder text, HistoryView view, bool fence)
    {
        _ = text.AppendLine(Invariant($"changes ({view.Counts.Changes}{(view.Truncated.Changes ? ", truncated" : string.Empty)}):"));
        if (view.Changes.Count == 0)
        {
            _ = text.AppendLine("  none");
        }

        foreach (var change in view.Changes)
        {
            var pr = change.Pr is { } n ? Invariant($"  #{n}") : string.Empty;
            var branch = change.Branch is { } b ? "  branch " + Fenced(b, change.Trust, fence) : string.Empty;
            _ = text.AppendLine(Invariant($"  {change.At:yyyy-MM-ddTHH:mm:ssZ}  {change.Sha[..7]}  {change.Kind.ToString().ToLowerInvariant()}{pr}  {Fenced(change.Subject, change.Trust, fence)}{branch}  files {string.Join(", ", change.Files)}"));
        }
    }

    private static void Intent(StringBuilder text, HistoryView view, bool fence)
    {
        _ = text.AppendLine(Invariant($"intent (records {view.Counts.Records}, decisions {view.Counts.Decisions}{(view.Truncated.Records || view.Truncated.Decisions ? ", truncated" : string.Empty)}):"));
        if (view.Records.Count + view.Decisions.Count == 0)
        {
            _ = text.AppendLine("  none");
        }

        foreach (var record in view.Records)
        {
            _ = text.AppendLine(Invariant($"  record  {Fenced(record.Label, record.Trust, fence)}  {record.Source}  {record.Key.Value}  updated {Instant(record.UpdatedAt, "unknown")}{(record.Leaky ? "  leaky" : string.Empty)}"));
        }

        foreach (var d in view.Decisions)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"  decision  {Fenced(d.Text, d.Trust, fence)}  {d.Provenance.ToString().ToLowerInvariant()} {(d.Confirmed ? "confirmed" : "suggested")}  {d.Node.Value}  at {Instant(d.At, "unknown")}{(d.Leaky ? "  leaky" : string.Empty)}");
        }
    }
}
#pragma warning restore MA0182
