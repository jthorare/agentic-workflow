using System.Globalization;
using System.Text;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Core.Subgraph;
using Aw.Context.Core.Trust;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Plain text for 'context lenses' and 'context filemap': every status is a word ('suggested', 'unmapped', 'conflict'), never colour.
/// A human's answer carries clean labels beside their trust, so the text fences the untrusted ones itself; a session's answer was
/// fenced by the server already.
/// </summary>
internal static class ContextLensText
{
    internal static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    internal static string Lenses(LensesView view, bool fence)
    {
        var text = new StringBuilder(Invariant($"lenses of {view.Anchor.Key.Value} ({Fenced(view.Anchor.Label, view.Anchor.Trust, fence)}) · map {MapName(view.MapVersion, view.MapCommit)}{(view.Draft ? " (draft)" : string.Empty)} · depth {view.Depth}\n"));
        return Body(text, new LensParts(view.Bands, view.Edges, view.Conflicts, view.Coverage, view.Searched), fence);
    }

    internal static string LensesSet(LensesSetView view, bool fence)
    {
        var anchors = string.Join(", ", view.Anchors.Select(a => Invariant($"{a.Key.Value} ({Fenced(a.Label, a.Trust, fence)})")));
        var text = new StringBuilder(Invariant($"lenses of {view.Anchors.Count} anchors ({anchors}) · map {MapName(view.MapVersion, view.MapCommit)}{(view.Draft ? " (draft)" : string.Empty)} · depth {view.Depth}\n"));
        foreach (var missing in view.MissingAnchors)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"missing anchor: {missing}");
        }

        return Body(text, new LensParts(view.Bands, view.Edges, view.Conflicts, view.Coverage, view.Searched), fence);
    }

    internal static string Subgraph(SubgraphView view, bool fence)
    {
        var text = new StringBuilder(Invariant($"subgraph of {view.Nodes.Count} nodes · map {MapName(view.MapVersion, view.MapCommit)} · depth {view.Depth}\n"));
        foreach (var missing in view.Missing)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"missing: {missing}");
        }

        return Body(text, new LensParts(view.Bands, view.Edges, view.Conflicts, view.Coverage, view.Searched), fence);
    }

    private readonly record struct LensParts(LensBands Bands, IReadOnlyList<LensEdge> Edges, IReadOnlyList<LensConflict> Conflicts, LensCoverage Coverage, LensSearched Searched);

    private static string MapName(string version, string? commit) => commit is null ? version : version + "@" + commit;

    private static string Body(StringBuilder text, LensParts parts, bool fence)
    {
        var (bands, edges, conflicts, coverage, searched) = parts;
        Intent(text, bands.Intent, fence);
        Product(text, bands.Product, fence);
        Code(text, bands.Code);
        foreach (var edge in edges)
        {
            _ = text.AppendLine(Invariant($"edge {edge.From.Value} {EdgeRules.Name(edge.Type)} {edge.To.Value}  {edge.Provenance.ToString().ToLowerInvariant()} {(edge.Confirmed ? "confirmed" : "suggested")} {edge.Confidence:0.00}  {edge.Link.Value}"));
        }

        foreach (var conflict in conflicts)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"conflict {conflict.Kind.ToString().ToLowerInvariant()}: {conflict.Decision.Value} over {conflict.Other} on {conflict.Node.Value}");
        }

        _ = text.Append(Searched(searched)).Append(Coverage(coverage));
        if (coverage.ByAnchor is { } by)
        {
            foreach (var (key, c) in by)
            {
                _ = text.AppendLine(Invariant($"  {key}: intent {c.Intent.Covered} of {c.Intent.Of} · product {c.Product.Confirmed} of {c.Product.Of} confirmed · code {c.Code.Implemented} of {c.Code.Of} implemented"));
            }
        }

        return text.ToString();
    }

    /// <summary>What was looked at, one word per source state ('onyx degraded (SND-CONTEXT-010)'), never colour.</summary>
    internal static string Searched(LensSearched searched)
    {
        var text = new StringBuilder(Invariant($"searched: map {searched.MapVersion} · depth {searched.Depth} · hops {string.Join(", ", searched.Hops)}\n"));
        foreach (var source in searched.Sources)
        {
            var why = string.Join(", ", new[] { source.Reason, source.Code }.Where(static w => w is not null));
            var sync = source.Sync is { } s ? Invariant($" · sync {s.State} {s.JobId.Value}") : string.Empty;
            _ = text.AppendLine(Invariant($"  {source.Id.Value} {StateWord(source.State)}{(why.Length == 0 ? string.Empty : " (" + why + ")")}{sync}"));
            _ = text.Append(SymbolWords.Pending(source.CatchingUp));
        }

        foreach (var (name, band) in new[] { ("intent", searched.Bands.Intent), ("product", searched.Bands.Product), ("code", searched.Bands.Code) })
        {
            if (band.Empty)
            {
                _ = text.AppendLine(CultureInfo.InvariantCulture, $"  {name} band: empty");
            }
        }

        return text.ToString();
    }

    internal static string StateWord(SearchedState state) => state switch
    {
        SearchedState.NotConfigured => "not-configured",
        SearchedState.Ok => "ok",
        SearchedState.Unavailable => "unavailable",
        SearchedState.Degraded => "degraded",
        _ => state.ToString().ToLowerInvariant(),
    };

    /// <summary>The coverage block, one line per count and one per gap; 'map status --anchor' prints it too.</summary>
    /// <param name="c">The coverage.</param>
    /// <returns>The lines.</returns>
    internal static string Coverage(LensCoverage c)
    {
        var text = new StringBuilder(Invariant($"coverage: scope {c.Scope} · intent {c.Intent.Covered} of {c.Intent.Of} · product {c.Product.Confirmed} of {c.Product.Of} confirmed · code {c.Code.Implemented} of {c.Code.Of} implemented ({c.Code.Level} level), {c.Code.Suggested} suggested\n"));
        if (c.Map is { } m)
        {
            _ = text.AppendLine(Invariant($"map coverage: {m.Paths.Count} paths{(m.Truncated ? " (truncated)" : string.Empty)}, {m.Files} files, {m.Unmapped} unmapped, {m.Suggested} suggested · map created {m.Created:yyyy-MM-ddTHH:mm:ssZ}{(m.AgeDays < 0 ? string.Empty : Invariant($" ({m.AgeDays} days old)"))}"));
        }

        foreach (var gap in c.Gaps)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"gap: {gap.Node.Value} missing {string.Join(", ", gap.Missing.Select(m => m.ToString().ToLowerInvariant()))}{(gap.Note is { } note ? " (" + note + ")" : string.Empty)}");
        }

        return text.ToString();
    }

    internal static string FileMap(FileMapView view)
    {
        var text = new StringBuilder(Invariant($"{view.Repo.Value} {view.Path} · map {view.MapVersion}{(view.Draft ? " (draft)" : string.Empty)} · at {view.Sha}\n"));
        if (view.Entries.Count == 0)
        {
            _ = text.AppendLine("  nothing here");
        }

        foreach (var entry in view.Entries)
        {
            var features = entry.Features.Count == 0 ? string.Empty : "  " + string.Join(", ", entry.Features.Select(f => $"{f.Edge.ToString().ToLowerInvariant()} {f.Key.Value}{(f.Confirmed ? string.Empty : " (suggested)")}"));
            var intent = entry.Intent is { } i ? Invariant($"  intent: {i.Records} records, {i.Decisions} decisions") : string.Empty;
            _ = text.AppendLine(Invariant($"  {entry.Name + (entry.Type == FileEntryType.Dir ? "/" : string.Empty),-28} {entry.Status.ToString().ToLowerInvariant(),-10} {entry.Files} {(entry.Files == 1 ? "file" : "files")}{features}{intent}"));
        }

        foreach (var s in view.Suggested)
        {
            _ = text.AppendLine(Invariant($"  suggested {s.File} implements {s.Feature.Value} {s.Confidence:0.00}  accept: sindri context filemap accept {s.LinkId.Value}"));
        }

        MoreLine(text, view);
        var t = view.Totals;
        _ = text.AppendLine(Invariant($"{t.Mapped} mapped, {t.Suggested} suggested, {t.Unmapped} unmapped · symbols: {view.Symbols.State.ToString().ToLowerInvariant()}{SymbolWords.Why(view.Symbols.Reason, view.Symbols.Commit, view.Symbols.AsOfCommit)}"));
        return text.ToString();
    }

    private static void MoreLine(StringBuilder text, FileMapView view)
    {
        if (view.NextCursor is { } next)
        {
            _ = text.AppendLine(Invariant($"  more: {view.Entries.Count} of {view.Total} shown  next page: --cursor '{next}'"));
        }
    }

    private static void Intent(StringBuilder text, IntentBand band, bool fence)
    {
        _ = text.AppendLine(Invariant($"intent (records {band.Counts.Records}, decisions {band.Counts.Decisions}):"));
        if (band.Records.Count + band.Decisions.Count == 0)
        {
            _ = text.AppendLine("  none");
        }

        foreach (var record in band.Records)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"  record  {Fenced(record.Label, record.Trust, fence)}  {record.Source}  {record.Key.Value}  mentions {string.Join(", ", record.Mentions.Select(m => m.Value))}");
        }

        foreach (var d in band.Decisions)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"  decision  {Fenced(d.Text, d.Trust, fence)}  {d.Provenance.ToString().ToLowerInvariant()} {(d.Confirmed ? "confirmed" : "suggested")}  {d.Node.Value}  decides {string.Join(", ", d.Decides.Select(k => k.Value))}{(d.Supersedes.Count == 0 ? string.Empty : "  supersedes " + string.Join(", ", d.Supersedes.Select(k => k.Value)))}");
        }
    }

    private static void Product(StringBuilder text, ProductBand band, bool fence)
    {
        _ = text.AppendLine(Invariant($"product (own {band.Counts.Own}, affects {band.Counts.Affects}):"));
        foreach (var item in band.Own)
        {
            _ = text.AppendLine(Invariant($"  own  {Fenced(item.Label, item.Trust, fence)}  {item.Kind.ToString().ToLowerInvariant()}  {item.Provenance.ToString().ToLowerInvariant()} {item.Confidence:0.00}  {(item.Confirmed ? "confirmed" : "suggested")}  {item.Node.Value}"));
        }

        foreach (var (name, reaches) in new[] { ("neighbor", band.Neighbors), ("affects", band.Affects) })
        {
            foreach (var r in reaches)
            {
                _ = text.AppendLine(Invariant($"  {name}  {Fenced(r.Label, r.Trust, fence)}  {r.Kind.ToString().ToLowerInvariant()}  {r.Provenance.ToString().ToLowerInvariant()} {r.Confidence:0.00}  {(r.Confirmed ? "confirmed" : "suggested")}  {r.Node.Value}  depth {r.Depth} via {string.Join(" > ", r.Path)}"));
            }
        }
    }

    private static void Code(StringBuilder text, CodeBand band)
    {
        _ = text.AppendLine(Invariant($"code (files {band.Counts.Files}, features with no code {band.Counts.FeaturesWithNoCode}; symbols {band.Symbols.State.ToString().ToLowerInvariant()}{(band.Symbols.Reason is null ? string.Empty : $": {band.Symbols.Reason}")}):"));
        if (band.Files.Count == 0)
        {
            _ = text.AppendLine("  none");
        }

        foreach (var file in band.Files)
        {
            var links = string.Join("; ", file.Implements.Select(k => "implements " + k.Value).Concat(file.Tests.Select(k => "tests " + k.Value)));
            var accept = file.LinkId is { } id ? $"  accept: sindri context filemap accept {id.Value}" : string.Empty;
            _ = text.AppendLine(Invariant($"  {file.Path}  {links}  {file.Provenance.ToString().ToLowerInvariant()} {file.Confidence:0.00}  {(file.Suggested ? "suggested" : "confirmed")}{accept}"));
        }
    }

    /// <summary>The label as it may reach a model: an untrusted one inside the fence (unless the server fenced it already for a session).</summary>
    internal static string Fenced(string label, Trust trust, bool fence) =>
        fence && trust == Trust.Untrusted ? UntrustedFence.Wrap("map", author: null, label) : label;
}
