using System.Globalization;
using System.Text;
using Aw.Context.Core.Code;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Core.Symbols;
using Aw.Context.Core.Trust;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Plain text for 'context code': every status is a word ('suggested', 'unavailable', 'capped'), never colour. A human's answer carries clean labels beside their trust, so the text fences the untrusted ones itself.</summary>
internal static class ContextCodeText
{
    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    internal static string Code(CodeView view, bool fence)
    {
        var anchors = string.Join(", ", view.Anchors.Select(a => Invariant($"{a.Key.Value} ({Fenced(a.Label, a.Trust, fence)})")));
        var text = new StringBuilder(Invariant($"code of {view.Anchors.Count} {(view.Anchors.Count == 1 ? "anchor" : "anchors")} ({anchors}) · map {view.MapVersion}{(view.Draft ? " (draft)" : string.Empty)}\n"));
        foreach (var missing in view.MissingAnchors)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"missing anchor: {missing}");
        }

        Files(text, view);
        Tests(text, view);
        Data(text, view, fence);
        _ = text.AppendLine(Invariant($"callers {Word(view.Callers.State)}{SymbolWords.Why(view.Callers.Reason, view.Callers.Commit, view.Callers.AsOfCommit)}{Count(view.Callers)} · callees {Word(view.Callees.State)}{SymbolWords.Why(view.Callees.Reason, view.Callees.Commit, view.Callees.AsOfCommit)}{Count(view.Callees)}"));
        foreach (var lexical in view.Lexical)
        {
            _ = text.AppendLine(Invariant($"lexical: {lexical.Repo} at {lexical.Commit} · {lexical.SearchedFiles} files used, {lexical.Candidates} candidates, {lexical.Read} read"));
        }

        return text.Append(Searched(view)).Append(ContextLensText.Coverage(view.Coverage)).ToString();
    }

    private static void Files(StringBuilder text, CodeView view)
    {
        _ = text.AppendLine(Invariant($"files ({view.Counts.Files}{(view.Truncated.Files ? ", truncated" : string.Empty)}; {view.Counts.FilesWithoutTest} without a test):"));
        None(text, view.Files.Count);
        foreach (var file in view.Files)
        {
            var accept = file.LinkId is { } id ? $"  accept: sindri context filemap accept {id.Value}" : string.Empty;
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"  {file.Path}  implements {string.Join(", ", file.Implements.Select(k => k.Value))}  {(file.Suggested ? "suggested" : "confirmed")}  tested by {(file.TestedBy.Count == 0 ? "none" : string.Join(", ", file.TestedBy))}{accept}");
        }
    }

    private static void Tests(StringBuilder text, CodeView view)
    {
        _ = text.AppendLine(Invariant($"tests ({view.Counts.Tests}{(view.Truncated.Tests ? ", truncated" : string.Empty)}):"));
        None(text, view.Tests.Count);
        foreach (var test in view.Tests)
        {
            var accept = test.LinkId is { } id ? $"  accept: sindri context filemap accept {id.Value}" : string.Empty;
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"  {test.Path}  {BasisWord(test.Basis)}  {(test.Suggested ? "suggested" : "confirmed")}  names {string.Join(", ", test.Named)}{accept}");
        }
    }

    private static void Data(StringBuilder text, CodeView view, bool fence)
    {
        _ = text.AppendLine(Invariant($"data things ({view.Counts.Things}{(view.Truncated.Things ? ", truncated" : string.Empty)}):"));
        None(text, view.Data.Things.Count);
        foreach (var thing in view.Data.Things)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"  {Fenced(thing.Label, thing.Trust, fence)}  {thing.Key.Value}  {thing.Access.ToString().ToLowerInvariant()} via {string.Join(", ", thing.Via.Select(k => k.Value))}{(thing.Searchable ? string.Empty : "  not searchable")}");
        }

        _ = text.AppendLine(Invariant($"schema files ({view.Counts.SchemaFiles}{(view.Truncated.SchemaFiles ? ", truncated" : string.Empty)}):"));
        None(text, view.Data.Files.Count);
        foreach (var file in view.Data.Files)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"  {file.Path}  names {file.Thing.Value} as '{file.Matched}'");
        }
    }

    private static string Searched(CodeView view)
    {
        var searched = view.Searched;
        var text = new StringBuilder(Invariant($"searched: map {searched.MapVersion}\n"));
        foreach (var source in searched.Sources)
        {
            var why = source.Code ?? source.Reason;
            _ = text.AppendLine(Invariant($"  {source.Id.Value} {SourceWord(source.State)}{(why is null ? string.Empty : " (" + why + ")")}"));
        }

        foreach (var line in view.Degraded)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"degraded: {line}");
        }

        return text.ToString();
    }

    private static void None(StringBuilder text, int count)
    {
        if (count == 0)
        {
            _ = text.AppendLine("  none");
        }
    }

    private static string BasisWord(CodeBasis basis) => basis switch
    {
        CodeBasis.Edge => "edge",
        CodeBasis.Imports => "imports",
        CodeBasis.Sibling => "sibling",
        _ => basis.ToString().ToLowerInvariant(),
    };

    private static string Count(CodeSymbols symbols) => symbols.Items.Count == 0 ? string.Empty : Invariant($" {symbols.Items.Count}");

    private static string Word(SymbolsState state) => state.ToString().ToLowerInvariant();

    private static string SourceWord(SearchedState state) => state switch
    {
        SearchedState.NotConfigured => "not-configured",
        SearchedState.Ok => "ok",
        SearchedState.Unavailable => "unavailable",
        SearchedState.Degraded => "degraded",
        _ => state.ToString().ToLowerInvariant(),
    };

    private static string Fenced(string label, Trust trust, bool fence) =>
        fence && trust == Trust.Untrusted ? UntrustedFence.Wrap("map", author: null, label) : label;
}
