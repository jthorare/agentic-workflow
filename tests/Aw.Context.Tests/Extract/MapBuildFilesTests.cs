using Aw.Context.Core.Extract;

namespace Aw.Context.Tests.Extract;

public sealed class MapBuildFilesTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_files_line_reads_back_as_the_same_counts()
    {
        var files = new MapBuildFiles(150, 412);

        Assert.Equal("extracting 150/412 files", files.Line);
        Assert.Equal(files, MapBuildFiles.From(files.Line));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("labeling 12 features")]
    [InlineData("graphify running, 5 min")]
    [InlineData("waiting for the running map build")]
    [InlineData("extracting 3/x files")]
    [InlineData("extracting 5/3 files")]
    [InlineData("extracting -1/3 files")]
    [InlineData("extracting 1/3 files and more")]
    [InlineData("")]
    public void Any_other_line_carries_no_counts(string line) => Assert.Null(MapBuildFiles.From(line));

    [Fact, Trait("Lane", "unit")]
    public void Newest_takes_the_last_files_line_past_later_lines_of_other_kinds()
    {
        string[] lines = ["starting", "extracting 50/412 files", "extracting 100/412 files", "clusters 3/9", "labeling 12 features"];

        Assert.Equal(new MapBuildFiles(100, 412), MapBuildFiles.Newest(lines));
    }

    [Fact, Trait("Lane", "unit")]
    public void Newest_is_null_without_a_files_line() => Assert.Null(MapBuildFiles.Newest(["starting", "clusters 3/9"]));
}
