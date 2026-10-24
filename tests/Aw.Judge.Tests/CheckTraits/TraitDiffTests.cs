using Aw.Judge.Core.Traits;

namespace Aw.Judge.Tests.CheckTraits;

public sealed class TraitDiffTests
{
    private static readonly string _diff = string.Join(
        '\n',
        "diff --git a/src/a.ts b/src/a.ts",
        "index 111..222 100644",
        "--- a/src/a.ts",
        "+++ b/src/a.ts",
        "@@ -1,2 +1,3 @@",
        " keep",
        "+added",
        " keep too",
        "@@ -10 +11,2 @@",
        "-old",
        "+new",
        "+newer",
        "diff --git a/gone.ts b/gone.ts",
        "--- a/gone.ts",
        "+++ /dev/null",
        "@@ -1 +0,0 @@",
        "-bye");

    [Fact, Trait("Lane", "unit")]
    public void Parse_reads_each_file_and_hunk_and_skips_a_deleted_file()
    {
        var files = TraitDiff.Parse(_diff);

        var file = Assert.Single(files);
        Assert.Equal("src/a.ts", file.Path);
        Assert.Equal([(1, 3), (11, 12)], file.Hunks.Select(hunk => (hunk.Start, hunk.End)));
        Assert.Equal("@@ -1,2 +1,3 @@\n keep\n+added\n keep too", file.Hunks[0].Text);
    }

    [Fact, Trait("Lane", "unit")]
    public void Chunk_joins_small_hunks_into_one_source()
    {
        var source = Assert.Single(TraitDiff.Chunk(TraitDiff.Parse(_diff)[0], 20_000));

        Assert.Equal(("src/a.ts", 1, 12), (source.Path, source.StartLine, source.EndLine));
        Assert.Contains("+newer", source.Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Chunk_splits_an_oversized_hunk_by_lines_with_each_pieces_own_span()
    {
        var file = TraitDiff.Whole("big.ts", string.Join('\n', Enumerable.Range(1, 40).Select(i => "line " + i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture))) + "\n");

        var sources = TraitDiff.Chunk(file, 120);

        Assert.True(sources.Count > 2);
        Assert.Equal(1, sources[0].StartLine);
        Assert.Equal(40, sources[^1].EndLine);
        Assert.All(sources, source => Assert.True(source.Text.Length <= 120));
        Assert.Equal(sources.Select(s => s.StartLine).Order(), sources.Select(s => s.StartLine));
    }

    [Fact, Trait("Lane", "unit")]
    public void Whole_makes_a_file_one_all_added_hunk()
    {
        var file = TraitDiff.Whole("a.ts", "one\ntwo\n");

        var hunk = Assert.Single(file.Hunks);
        Assert.Equal((1, 2, "@@ -0,0 +1,2 @@\n+one\n+two"), (hunk.Start, hunk.End, hunk.Text));
    }

    [Fact, Trait("Lane", "unit")]
    public void Ignored_reads_the_leading_comment_block_only()
    {
        var head = "#!/usr/bin/env node\n// trait-ignore-file no-todo: generated\n\n// trait-ignore-file other-rule: legacy\nconst a = 1;\n// trait-ignore-file late: too late\n";

        Assert.Equal(["no-todo", "other-rule"], TraitDiff.Ignored(head).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ignored_wants_a_reason() => Assert.Empty(TraitDiff.Ignored("// trait-ignore-file no-todo:\n"));
}
