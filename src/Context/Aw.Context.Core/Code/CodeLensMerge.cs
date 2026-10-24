using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;

namespace Aw.Context.Core.Code;

/// <summary>Adds the lexical findings to a graph-half code view (S-CL): pure.</summary>
public static class CodeLensMerge
{
    /// <summary>Adds lexical tests and schema files; an edge test is never replaced, imports outranks sibling.</summary>
    /// <param name="view">The graph-half view.</param>
    /// <param name="findings">The lexical findings.</param>
    /// <returns>The view with tests, schema files, testedBy, counts, truncation and the lexical rows filled.</returns>
    public static CodeView With(CodeView view, CodeLexicalFindings findings)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(findings);
        var (existing, added) = Tie(view, findings.Tests);
        var tests = JoinNamed(view.Tests, existing).Concat(added).ToList();
        var files = TestedBy(view.Files, tests);
        var (listedSchema, schemaTotal, schemaCut) = SchemaFiles(view.Data, findings.SchemaFiles);
        var total = view.Counts.Tests + added.Count;
        var withoutTest = view.Counts.FilesWithoutTest - files.Count(f => f.TestedBy.Count > 0) + view.Files.Count(f => f.TestedBy.Count > 0);
        return view with
        {
            Files = files,
            Tests = tests,
            Data = view.Data with { Files = listedSchema },
            Counts = view.Counts with { Tests = total, SchemaFiles = schemaTotal, FilesWithoutTest = withoutTest, Unlocated = findings.Unlocated },
            Truncated = view.Truncated with { Tests = findings.TestsTruncated, SchemaFiles = schemaCut || findings.SchemaTruncated, SearchedFiles = findings.SearchedFilesCapped },
            Lexical = findings.Searched,
            Searched = Searched(view, findings, tests.Count > 0, listedSchema.Count > 0),
            Degraded = [.. view.Degraded.Concat(Degraded(findings))],
        };
    }

    private static LensSearched Searched(CodeView view, CodeLexicalFindings findings, bool tests, bool schema)
    {
        SearchedSource?[] rows = [findings.TestsRow, findings.SchemaRow];
        var codeFound = view.Files.Count > 0 || tests || view.Data.Things.Count > 0 || schema;
        return view.Searched with
        {
            Sources = [.. view.Searched.Sources.Concat(rows.OfType<SearchedSource>())],
            Bands = view.Searched.Bands with { Code = new BandSearched(!codeFound) },
        };
    }

    private static IEnumerable<string> Degraded(CodeLexicalFindings findings) =>
        new[] { findings.TestsRow, findings.SchemaRow }.OfType<SearchedSource>().Where(r => r.State == SearchedState.Degraded)
            .Select(r => $"{r.Id.Value}: {r.Code ?? "degraded"}");

    // Lexical tests grouped by path (imports outranks sibling, the named files are joined): those the edges already hold, and the new ones.
    private static (Dictionary<string, List<string>> Existing, List<CodeTest> Added) Tie(CodeView view, IReadOnlyList<LexicalTest> lexical)
    {
        var known = view.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var edgePaths = view.Tests.Select(t => t.Path).ToHashSet(StringComparer.Ordinal);
        var existing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var added = new List<CodeTest>();
        foreach (var group in lexical.GroupBy(t => t.Path, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var named = group.SelectMany(t => t.Named).Where(known.ContainsKey).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            if (named.Count == 0)
            {
                continue;
            }

            if (edgePaths.Contains(group.Key))
            {
                existing[group.Key] = named;
                continue;
            }

            var anchors = view.Anchors.Select(a => a.Key).Where(a => named.Exists(n => known[n].Anchors.Any(k => k.Value == a.Value))).ToList();
            added.Add(new CodeTest(group.Key, group.Key, NodeKind.Code, group.Min(t => t.Basis), [], named, Confirmed: false, Suggested: true, LinkId: null, Results.Trust.Trusted, anchors));
        }

        return (existing, added);
    }

    private static IEnumerable<CodeTest> JoinNamed(IReadOnlyList<CodeTest> edgeTests, Dictionary<string, List<string>> existing) =>
        edgeTests.Select(t => existing.TryGetValue(t.Path, out var named) ? t with { Named = [.. t.Named.Concat(named).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)] } : t);

    private static List<CodeFile> TestedBy(IReadOnlyList<CodeFile> files, List<CodeTest> tests) =>
        [.. files.Select(f => f with { TestedBy = [.. f.TestedBy.Concat(tests.Where(t => t.Named.Contains(f.Path, StringComparer.Ordinal)).Select(t => t.Path)).Distinct(StringComparer.Ordinal)] })];

    private static (List<CodeDataFile> Listed, int Total, bool Truncated) SchemaFiles(CodeData data, IReadOnlyList<LexicalSchemaFile> found)
    {
        var all = new List<CodeDataFile>();
        var cut = false;
        foreach (var thing in data.Things)
        {
            var own = found.Where(f => f.Thing.Value == thing.Key.Value).DistinctBy(f => f.Path, StringComparer.Ordinal).OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
            cut |= own.Count > CodeRules.MaxFilesPerThing;
            all.AddRange(own.Select(f => new CodeDataFile(f.Path, f.Path, NodeKind.Code, thing.Key, f.Matched, Results.Trust.Trusted, thing.Anchors)));
        }

        return ([.. ListedPerThing(all)], all.Count, cut);
    }

    private static IEnumerable<CodeDataFile> ListedPerThing(List<CodeDataFile> all) =>
        all.GroupBy(f => f.Thing.Value, StringComparer.Ordinal).SelectMany(g => g.Take(CodeRules.MaxFilesPerThing));
}
