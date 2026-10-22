using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.PrWatch;

/// <summary>The poller's state file (PR babysitter spec §6.2): written whole, atomically and owner-only, and read back as written.</summary>
[Lane(TestLane.Integration)]
public sealed class HomePrWatchStateTests : IDisposable
{
    private static readonly DateTimeOffset _at = new(2026, 10, 22, 8, 30, 0, TimeSpan.Zero);
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact]
    public async Task A_missing_file_reads_as_empty()
    {
        var read = await State().Read(TestContext.Current.CancellationToken);

        Assert.Empty(read.Value.Pulls);
        Assert.Empty(read.Value.Hosts);
    }

    [Fact]
    public async Task What_is_written_is_read_back()
    {
        var file = new PrWatchFile(
            new Dictionary<string, PrWatchEntry>(StringComparer.Ordinal) { [PrWatchRig.Link] = new("t1", "aaa", "Open", "Pending", 3, _at) },
            new Dictionary<string, PrWatchHost>(StringComparer.Ordinal) { ["forge.test"] = new(2, _at.AddMinutes(2)) });
        var written = await State().Write(file, TestContext.Current.CancellationToken);

        var read = await State().Read(TestContext.Current.CancellationToken);

        Assert.False(written.IsError);
        Assert.Equal(new PrWatchEntry("t1", "aaa", "Open", "Pending", 3, _at), read.Value.Pulls[PrWatchRig.Link]);
        Assert.Equal(new PrWatchHost(2, _at.AddMinutes(2)), read.Value.Hosts["forge.test"]);
    }

    [Fact]
    public async Task The_file_is_owner_only_and_a_second_write_replaces_it_leaving_no_temporary_file()
    {
        var ct = TestContext.Current.CancellationToken;
        var state = State();
        var path = new WorkflowsHome(_home.Path).PrWatchFile;
        _ = await state.Write(new PrWatchFile(new Dictionary<string, PrWatchEntry>(StringComparer.Ordinal) { ["a"] = new("d1", "h", "Open", "None", 0, _at) }, PrWatchFile.Empty.Hosts), ct);

        _ = await state.Write(PrWatchFile.Empty, ct);

        Assert.Empty((await state.Read(ct)).Value.Pulls);
        Assert.Equal([path], Directory.GetFiles(Path.GetDirectoryName(path)!));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
    }

    [Fact]
    public async Task A_file_that_does_not_parse_is_an_error_that_names_the_file_and_the_fix()
    {
        var path = new WorkflowsHome(_home.Path).PrWatchFile;
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{ not json", TestContext.Current.CancellationToken);

        var read = await State().Read(TestContext.Current.CancellationToken);

        Assert.True(read.IsError);
        Assert.Contains("pr-watch.json", read.Error.Message, StringComparison.Ordinal);
        Assert.Contains("fix:", read.Error.Message, StringComparison.Ordinal);
    }

    private HomePrWatchState State() => new(new WorkflowsHome(_home.Path));
}
