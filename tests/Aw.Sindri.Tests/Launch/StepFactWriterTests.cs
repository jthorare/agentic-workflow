using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The fact files land in the run directory, none can land outside it, and the readers of '--facts' find them.</summary>
[Lane(TestLane.Unit)]
public sealed class StepFactWriterTests : IDisposable
{
    private readonly string _run = Directory.CreateTempSubdirectory("aw-facts-").FullName;

    public void Dispose() => Directory.Delete(_run, recursive: true);

    [Fact]
    public async Task The_files_are_written_into_the_run_directory()
    {
        var problem = await StepFactWriter.Write(_run, new Dictionary<string, string>(StringComparer.Ordinal) { ["workspace.json"] = "\"/work/docs\"", ["docsScope.json"] = """{"base":"abc"}""" }, CancellationToken.None);

        Assert.Null(problem);
        Assert.Equal("\"/work/docs\"", await File.ReadAllTextAsync(Path.Combine(_run, "workspace.json"), TestContext.Current.CancellationToken));
        Assert.Equal("""{"base":"abc"}""", await File.ReadAllTextAsync(Path.Combine(_run, "docsScope.json"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_retry_rewrites_a_file_a_first_launch_left()
    {
        _ = await StepFactWriter.Write(_run, new Dictionary<string, string>(StringComparer.Ordinal) { ["brief.json"] = "\"old\"" }, CancellationToken.None);

        _ = await StepFactWriter.Write(_run, new Dictionary<string, string>(StringComparer.Ordinal) { ["brief.json"] = "\"new\"" }, CancellationToken.None);

        Assert.Equal("\"new\"", await File.ReadAllTextAsync(Path.Combine(_run, "brief.json"), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("../escape.json")]
    [InlineData("a/b.json")]
    [InlineData("/etc/escape.json")]
    public async Task A_name_that_is_not_one_file_in_the_directory_is_refused_and_nothing_is_written(string name)
    {
        var problem = await StepFactWriter.Write(_run, new Dictionary<string, string>(StringComparer.Ordinal) { ["ok.json"] = "1", [name] = "2" }, CancellationToken.None);

        Assert.NotNull(problem);
        Assert.Contains(name, problem.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_run));
    }
}
