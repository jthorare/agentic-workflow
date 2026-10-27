using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Questions;

[Lane(TestLane.Unit)]
public sealed class UntrustedTextFilesTests
{
    private static ModelPickFile File(string path, int chars) => new(path, ModelPickJson.Lines(chars));

    [Fact]
    public void Files_that_fit_are_all_kept_in_the_order_given()
    {
        var files = new[] { File("docs/a.md", 1_000), File("src/b.cs", 1_000) };

        var capped = UntrustedText.CappedFiles(files, 10_000);

        Assert.Equal(files, capped.Kept);
        Assert.Empty(capped.Omitted);
        Assert.Empty(capped.Cut);
    }

    [Fact]
    public void Files_under_src_are_kept_first_and_the_rest_are_named_with_their_sizes()
    {
        var files = new[] { File("docs/a.md", 6_000), File("src/b.cs", 6_000), File("lib/src/c.cs", 3_000) };

        var capped = UntrustedText.CappedFiles(files, 10_000);

        Assert.Equal(["src/b.cs", "lib/src/c.cs"], capped.Kept.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal([new ModelPickOmitted("docs/a.md", 6_000, "cap")], capped.Omitted);
        Assert.Empty(capped.Cut);
    }

    [Fact]
    public void A_file_that_does_not_fit_is_skipped_and_a_smaller_one_after_it_still_fits()
    {
        var files = new[] { File("src/a.cs", 6_000), File("src/b.cs", 6_000), File("src/c.cs", 3_000) };

        var capped = UntrustedText.CappedFiles(files, 10_000);

        Assert.Equal(["src/a.cs", "src/c.cs"], capped.Kept.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal([new ModelPickOmitted("src/b.cs", 6_000, "cap")], capped.Omitted);
    }

    [Fact]
    public void When_no_file_fits_the_first_in_order_is_cut_at_a_line_and_the_others_are_named()
    {
        var files = new[] { File("docs/a.md", 20_000), File("src/b.cs", 15_000) };

        var capped = UntrustedText.CappedFiles(files, 10_000);

        var kept = Assert.Single(capped.Kept);
        Assert.Equal("src/b.cs", kept.Path);
        Assert.Equal(10_000, kept.Text.Length);   // why: 100-character lines, so the last whole line ends exactly at the cap
        Assert.EndsWith("\n", kept.Text, StringComparison.Ordinal);
        Assert.Equal([new ModelPickCut("src/b.cs", 10_000, 15_000)], capped.Cut);
        Assert.Equal([new ModelPickOmitted("docs/a.md", 20_000, "cap")], capped.Omitted);
    }

    [Fact]
    public void A_single_line_longer_than_the_cap_is_cut_at_the_cap()
    {
        var capped = UntrustedText.CappedFiles([new ModelPickFile("src/min.js", new string('x', 500))], 100);

        Assert.Equal(100, Assert.Single(capped.Kept).Text.Length);
        Assert.Equal([new ModelPickCut("src/min.js", 100, 500)], capped.Cut);
    }

    [Fact]
    public void A_cap_of_zero_keeps_nothing_and_names_everything()
    {
        var capped = UntrustedText.CappedFiles([File("src/a.cs", 1_000)], 0);

        Assert.Empty(capped.Kept);
        Assert.Empty(capped.Cut);
        Assert.Equal([new ModelPickOmitted("src/a.cs", 1_000, "cap")], capped.Omitted);
    }

    [Fact]
    public void No_files_is_an_empty_result()
    {
        var capped = UntrustedText.CappedFiles([], 100);

        Assert.Empty(capped.Kept);
        Assert.Empty(capped.Omitted);
        Assert.Empty(capped.Cut);
    }
}
