using System.Globalization;
using System.Text;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.Sindri.Features.CodeIndexReads;

namespace Aw.Sindri.Host.Text;

/// <summary>
/// The text form of the code-index reads (code-index spec §7.1, §10): every state is a word, never colour. Names, signatures and paths are repo text:
/// control and bidirectional-override characters are stripped from them. A session's answer is fenced as untrusted by the server (CodeIndexEgress) and printed as it comes.
/// </summary>
internal static class CodeIndexText
{
    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    internal static string Status(CodeIndexStatusView view)
    {
        var text = new StringBuilder();
        foreach (var repo in view.Repos)
        {
            var layers = string.Join(" · ", repo.Layers.Select(static l => Invariant($"{LayerWord(l.Layer)} {StateWord(l.State)}{(l.Code is null ? string.Empty : " (" + l.Code + ")")}")));
            var counts = repo.Counts is { } c ? Invariant($" · {c.Files} files, {c.Symbols} symbols, {c.Edges} edges") : string.Empty;
            _ = text.AppendLine(Invariant($"{Clean(repo.Repo.Value)}: {StateWord(repo.State)}{(repo.Commit is { } commit ? " @ " + Short(commit.Value) : string.Empty)}{(repo.BuiltAt is { } at ? " · built " + at.ToString("u", CultureInfo.InvariantCulture) : string.Empty)}{(layers.Length == 0 ? string.Empty : " · " + layers)}{counts}"));
        }

        return text.Length == 0 ? "no repos in scope\n" : text.ToString();
    }

    internal static string Symbols(FindSymbolsAnswer answer)
    {
        var text = new StringBuilder(Invariant($"{answer.Hits.Count} {(answer.Hits.Count == 1 ? "symbol" : "symbols")}{(answer.Truncated ? " (truncated)" : string.Empty)} · {Stamp(answer.Stamp)}\n"));
        foreach (var hit in answer.Hits)
        {
            _ = text.AppendLine(Invariant($"  {Clean(hit.Name)}  {hit.Kind.ToString().ToLowerInvariant()}  {Clean(hit.File)}:{hit.StartLine}-{hit.EndLine}{(hit.Exported ? "  exported" : string.Empty)}  {Clean(hit.Signature)}"));
        }

        foreach (var note in answer.Notes ?? [])
        {
            _ = text.AppendLine(Clean(note));
        }

        return text.ToString();
    }

    internal static string Calls(CodeCallsView view, string direction)
    {
        var text = new StringBuilder(Invariant($"{direction} of {Clean(view.Subject.Value)} · coverage {CoverageWord(view.Coverage)} · {view.Edges.Count} {(view.Edges.Count == 1 ? "edge" : "edges")}{(view.Truncated ? " (truncated)" : string.Empty)} · {Stamp(view.Stamp)}\n"));
        _ = text.AppendLine(view.Note);
        foreach (var edge in view.Edges)
        {
            var other = edge.Other is { } symbol ? Clean(symbol.Ref.Value) : "(not resolved)";
            _ = text.AppendLine(Invariant($"  level {edge.Level}  {other}  {Clean(edge.Name)}  {edge.Kind.ToString().ToLowerInvariant()}  {edge.Confidence.ToString().ToLowerInvariant()} ({edge.Basis.ToString().ToLowerInvariant()}, {CoverageWord(edge.Coverage)})  line {edge.Line}{(edge.Candidates > 1 ? Invariant($"  1 of {edge.Candidates}") : string.Empty)}"));
        }

        if (view.UnresolvedCount > 0)
        {
            _ = text.AppendLine(Invariant($"{view.UnresolvedCount} calls not resolved"));
        }

        return text.ToString();
    }

    private static string Stamp(AnswerStamp stamp)
        => Invariant($"{Clean(stamp.Repo.Value)} @ {Short(stamp.Commit.Value)}, built {stamp.BuiltAt:u}, {LayerWord(stamp.Layer.Layer)} {StateWord(stamp.Layer.State)}");

    /// <summary>The text without control and bidirectional-override characters (spec §7.1).</summary>
    internal static string Clean(string value) => string.Concat(value.Where(static c => char.GetUnicodeCategory(c) is not (UnicodeCategory.Control or UnicodeCategory.Format)));

    private static string Short(string commit) => commit.Length > 7 ? commit[..7] : commit;

    private static string StateWord(IndexState state) => state switch
    {
        IndexState.Ok => "ok",
        IndexState.Damaged => "damaged",
        IndexState.Missing => "missing",
        _ => "missing",
    };

    private static string StateWord(LayerState state) => state switch
    {
        LayerState.Ok => "ok",
        LayerState.Unavailable => "unavailable",
        LayerState.Disabled => "disabled",
        LayerState.Pending => "pending",
        _ => "pending",
    };

    private static string LayerWord(LayerName layer) => layer.ToString().ToLowerInvariant();

    private static string CoverageWord(Coverage coverage) => coverage switch
    {
        Coverage.Full => "full",
        Coverage.Partial => "partial",
        Coverage.Unavailable => "unavailable",
        _ => "unavailable",
    };
}
