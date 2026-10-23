using System.Text;
using Aw.Context.Core.WholeMap;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>Plain text for 'context map whole': the counts line, then one line per area, biggest first, so a terminal sees the overview the page draws.</summary>
internal static class ContextWholeText
{
    internal static string Whole(WholeMapView view, bool fence)
    {
        var commit = view.MapCommit is null ? string.Empty : "@" + view.MapCommit;
        var fallback = view.Fallback is null ? string.Empty : " (" + view.Fallback + ")";
        var text = new StringBuilder(ContextLensText.Invariant($"whole map {view.MapVersion}{commit}{(view.Draft ? " (draft)" : string.Empty)} · {view.Counts.Nodes} nodes, {view.Counts.Edges} edges · detail: {view.Detail.ToString().ToLowerInvariant()}{fallback}\n"));
        _ = text.Append("  ").AppendJoin(", ", view.Counts.ByKind.Where(k => k.Value > 0).Select(k => ContextLensText.Invariant($"{k.Value} {k.Key}"))).AppendLine();
        Areas(text, view, fence);
        Opened(text, view, fence);
        foreach (var placed in view.Located ?? [])
        {
            _ = text.AppendLine(ContextLensText.Invariant($"  {placed.Key} → {placed.Area ?? "not in the map"}"));
        }

        return text.ToString();
    }

    private static void Areas(StringBuilder text, WholeMapView view, bool fence)
    {
        foreach (var area in view.Areas.OrderByDescending(a => a.Members).ThenBy(a => a.Key, StringComparer.Ordinal))
        {
            var kinds = string.Join(", ", area.ByKind.Select(k => ContextLensText.Invariant($"{k.Value} {k.Key}")));
            _ = text.AppendLine(ContextLensText.Invariant($"  {ContextLensText.Fenced(area.Label, area.Trust, fence)} · {area.Members} {(area.Members == 1 ? "member" : "members")} · {kinds}"));
        }

        if (view.Truncated && view.Area is null)
        {
            _ = text.AppendLine("  more areas than the answer holds; ask for one by key");
        }
    }

    private static void Opened(StringBuilder text, WholeMapView view, bool fence)
    {
        if (view.Area is not { } opened)
        {
            return;
        }

        _ = text.AppendLine(ContextLensText.Invariant($"  area {opened.Key}: {view.Nodes?.Count ?? 0} of {opened.Counts.Nodes} nodes{(opened.Truncated ? " (cut at the cap)" : string.Empty)}"));
        foreach (var node in view.Nodes ?? [])
        {
            _ = text.AppendLine(ContextLensText.Invariant($"    {node.Key.Value}  {ContextLensText.Fenced(node.Label, node.Trust, fence)}"));
        }
    }
}
