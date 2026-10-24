using System.Globalization;
using Aw.Context.Core.Code;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Code;

public sealed class CodeLensMergeTests
{
    private static readonly CodeLexical _row = new("fixture-notes", "a41b07d", 2, 3, 3);

    private static CodeView Base(params string[] anchors) =>
        CodeLensBuilder.Build(CodeGraphs.Basis, CodeGraphs.Sharing(), anchors.Length == 0 ? ["feature:sharing"] : anchors).Value;

    private static CodeLexicalFindings Found(IReadOnlyList<LexicalTest>? tests = null, IReadOnlyList<LexicalSchemaFile>? schema = null, int unlocated = 0, bool capped = false) =>
        new(tests ?? [], schema ?? [], [_row], unlocated, capped);

    private static string Path(string prefix, int i) => string.Create(CultureInfo.InvariantCulture, $"{prefix}{i:D3}.sql");

    [Fact, Trait("Lane", "unit")]
    public void Lexical_tests_are_added_when_no_edge_exists_as_suggested_and_update_testedBy()
    {
        var view = CodeLensMerge.With(Base(), Found([new LexicalTest("tests/share-extra.test.ts", CodeBasis.Imports, ["src/sharing/links.ts"])]));

        var added = Assert.Single(view.Tests, t => t.Path == "tests/share-extra.test.ts");
        Assert.Equal((CodeBasis.Imports, false, true, Trust.Trusted, NodeKind.Code), (added.Basis, added.Confirmed, added.Suggested, added.Trust, added.Kind));
        Assert.False(added.LinkId.HasValue);
        Assert.Empty(added.Tests);
        Assert.Equal(["src/sharing/links.ts"], added.Named, StringComparer.Ordinal);
        Assert.Equal(["feature:sharing"], added.Anchors.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Equal(["tests/sharing.test.ts", "tests/share-extra.test.ts"], view.Files.Single(f => f.Path == "src/sharing/links.ts").TestedBy, StringComparer.Ordinal);
        Assert.Equal(["tests/sharing.test.ts"], view.Files.Single(f => f.Path == "src/sharing/routes.ts").TestedBy, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Edge_and_lexical_for_the_same_path_is_one_entry_basis_edge()
    {
        var view = CodeLensMerge.With(Base(), Found([new LexicalTest("tests/sharing.test.ts", CodeBasis.Imports, ["src/sharing/links.ts"])]));

        var test = Assert.Single(view.Tests);
        Assert.Equal((CodeBasis.Edge, true), (test.Basis, test.Confirmed));
    }

    [Fact, Trait("Lane", "unit")]
    public void Imports_beats_sibling_and_the_named_files_are_joined()
    {
        var view = CodeLensMerge.With(Base(), Found(
        [
            new LexicalTest("tests/links.test.ts", CodeBasis.Sibling, ["src/sharing/links.ts"]),
            new LexicalTest("tests/links.test.ts", CodeBasis.Imports, ["src/sharing/routes.ts"]),
        ]));

        var test = Assert.Single(view.Tests, t => t.Path == "tests/links.test.ts");
        Assert.Equal(CodeBasis.Imports, test.Basis);
        Assert.Equal(["src/sharing/links.ts", "src/sharing/routes.ts"], test.Named, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lexical_test_for_a_file_the_view_does_not_hold_names_nothing_and_is_dropped()
    {
        var view = CodeLensMerge.With(Base(), Found([new LexicalTest("tests/y.test.ts", CodeBasis.Imports, ["src/other/x.ts"])]));

        Assert.DoesNotContain(view.Tests, t => t.Path == "tests/y.test.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public void Schema_files_name_their_thing_and_carry_its_anchors()
    {
        var view = CodeLensMerge.With(Base(), Found(schema:
        [
            new LexicalSchemaFile(NodeKey.From("thing:shared-link"), "migrations/002_sharing.sql", "shared_link"),
            new LexicalSchemaFile(NodeKey.From("thing:other-thing"), "migrations/009.sql", "other_thing"),
        ]));

        var file = Assert.Single(view.Data.Files);
        Assert.Equal(("migrations/002_sharing.sql", "migrations/002_sharing.sql", NodeKind.Code, "thing:shared-link", "shared_link"), (file.Path, file.Label, file.Kind, file.Thing.Value, file.Matched));
        Assert.Equal(["feature:sharing"], file.Anchors.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Equal(1, view.Counts.SchemaFiles);
    }

    [Fact, Trait("Lane", "unit")]
    public void Counts_truncation_and_the_lexical_rows_follow_the_findings()
    {
        var view = CodeLensMerge.With(Base(), Found([new LexicalTest("tests/z.test.ts", CodeBasis.Sibling, ["src/sharing/links.ts"])], unlocated: 2, capped: true));

        Assert.Equal((2, 2, 0, 1), (view.Counts.Tests, view.Counts.Unlocated, view.Counts.FilesWithoutTest, view.Lexical.Count));
        Assert.True(view.Truncated.SearchedFiles);
        Assert.Equal(_row, view.Lexical[0]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Lists_are_not_cut_and_schema_files_cap_at_100_per_thing()
    {
        var tests = Enumerable.Range(0, 101).Select(i => new LexicalTest(Path("tests/t", i), CodeBasis.Imports, ["src/sharing/links.ts"])).ToList();
        var schema = Enumerable.Range(0, 101).Select(i => new LexicalSchemaFile(NodeKey.From("thing:shared-link"), Path("migrations/m", i), "shared_link")).ToList();

        var view = CodeLensMerge.With(Base(), Found(tests, schema));

        Assert.Equal((102, false, 102), (view.Tests.Count, view.Truncated.Tests, view.Counts.Tests));   // no list cut (owner q93, 2026-10-23)
        Assert.Equal((100, true, 101), (view.Data.Files.Count, view.Truncated.SchemaFiles, view.Counts.SchemaFiles));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_capped_candidate_scan_says_tests_are_truncated_even_when_the_list_is_not_full()
    {
        var found = Found([new LexicalTest("tests/t.test.ts", CodeBasis.Imports, ["src/sharing/links.ts"])]) with { TestsTruncated = true };

        var view = CodeLensMerge.With(Base(), found);

        Assert.True(view.Truncated.Tests);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_capped_schema_scan_says_schema_files_are_truncated()
    {
        var found = Found(schema: [new LexicalSchemaFile(NodeKey.From("thing:shared-link"), "migrations/m001.sql", "shared_link")]) with { SchemaTruncated = true };

        var view = CodeLensMerge.With(Base(), found);

        Assert.True(view.Truncated.SchemaFiles);
    }

    [Fact, Trait("Lane", "unit")]
    public void Merging_no_findings_keeps_the_graph_half()
    {
        var view = Base();

        var merged = CodeLensMerge.With(view, new CodeLexicalFindings([], [], [], 0, SearchedFilesCapped: false));

        Assert.Equal(view.Files.Select(f => (f.Path, string.Join('|', f.TestedBy))), merged.Files.Select(f => (f.Path, string.Join('|', f.TestedBy))));
        Assert.Equal(view.Tests.Select(t => t.Path), merged.Tests.Select(t => t.Path), StringComparer.Ordinal);
        Assert.Equal(view.Data.Things.Count, merged.Data.Things.Count);
        Assert.Empty(merged.Data.Files);
    }
}
