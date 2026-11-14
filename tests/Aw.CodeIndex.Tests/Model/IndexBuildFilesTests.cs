using Aw.CodeIndex.Core.Facade;

namespace Aw.CodeIndex.Tests.Model;

// The structure progress line a build writes and Sindri's setup list reads back (#3112).
public sealed class IndexBuildFilesTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_structure_line_reads_back_as_the_same_counts()
    {
        var files = new IndexBuildFiles(7000, 15223);

        Assert.Equal("structure 7,000 of 15,223 files", files.Line);
        Assert.Equal(files, IndexBuildFiles.From(files.Line));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("graph")]
    [InlineData("embeddings")]
    [InlineData("structure 3 of x files")]
    [InlineData("structure 5 of 3 files")]
    [InlineData("structure 1 of 3 files and more")]
    [InlineData("extracting 1/3 files")]
    [InlineData("")]
    public void Any_other_line_carries_no_counts(string line) => Assert.Null(IndexBuildFiles.From(line));

    [Fact, Trait("Lane", "unit")]
    public void Newest_takes_the_last_structure_line_past_later_lines_of_other_kinds()
    {
        string[] lines = ["structure 500 of 1,200 files", "structure 1,000 of 1,200 files", "graph", "embeddings"];

        Assert.Equal(new IndexBuildFiles(1000, 1200), IndexBuildFiles.Newest(lines));
    }

    [Fact, Trait("Lane", "unit")]
    public void Newest_is_null_without_a_structure_line() => Assert.Null(IndexBuildFiles.Newest(["graph"]));
}
