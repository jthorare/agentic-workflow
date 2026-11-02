using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Questions;

/// <summary>An over-cap diff never loses a path silently: a first file that does not fit is cut at a line and named, whatever else is dropped is listed.</summary>
[Lane(TestLane.Unit)]
public sealed class ResolutionDiffFallbackTests
{
    [Fact]
    public void A_single_file_over_the_cap_is_cut_at_a_line_and_named()
    {
        var diff = FileDiff("src/Big.cs", lines: 50, width: 40);

        var capped = UntrustedText.CappedDiff(diff, cap: 500);

        Assert.StartsWith("diff --git a/src/Big.cs b/src/Big.cs\n", capped, StringComparison.Ordinal);
        Assert.Contains("[diff cut: src/Big.cs after ", capped, StringComparison.Ordinal);
        Assert.Contains($" of {diff.Length} chars]", capped, StringComparison.Ordinal);
        var kept = capped[..capped.IndexOf("[diff cut", StringComparison.Ordinal)];
        Assert.True(kept.Length <= 500);
        Assert.EndsWith("\n", kept, StringComparison.Ordinal);
    }

    [Fact]
    public void When_no_file_fits_the_src_file_is_cut_and_the_others_are_listed_as_dropped()
    {
        var diff = FileDiff("tests/BigTests.cs", lines: 50, width: 40) + FileDiff("src/Big.cs", lines: 50, width: 40);

        var capped = UntrustedText.CappedDiff(diff, cap: 500);

        Assert.StartsWith("diff --git a/src/Big.cs b/src/Big.cs\n", capped, StringComparison.Ordinal);
        Assert.DoesNotContain("diff --git a/tests/BigTests.cs", capped, StringComparison.Ordinal);
        Assert.Contains("[diff cut: src/Big.cs after ", capped, StringComparison.Ordinal);
        Assert.Contains("[omitted 1 of 2 files, over the 500-char cap: tests/BigTests.cs (", capped, StringComparison.Ordinal);
    }

    [Fact]
    public void A_diff_that_fits_is_unchanged()
    {
        var diff = FileDiff("src/Small.cs", lines: 3, width: 10);

        Assert.Equal(diff, UntrustedText.CappedDiff(diff, cap: 5_000));
    }

    private static string FileDiff(string path, int lines, int width) =>
        $"diff --git a/{path} b/{path}\n--- a/{path}\n+++ b/{path}\n@@ -1 +1 @@\n" + string.Concat(Enumerable.Range(0, lines).Select(i => "+" + new string('x', width) + "\n"));
}
