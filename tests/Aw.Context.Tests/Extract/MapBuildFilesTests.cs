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
}
