using Aw.Sindri.Core.LensPacks;
using Xunit;

namespace Aw.Sindri.Testing.LensPacks;

/// <summary>The contract every <see cref="IPackDirectories"/> honours: every file under the directory by its '/' separated path inside it, the entries that resolve outside it named and unread, an error for a directory it can't read.</summary>
public abstract class PackDirectoriesContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The adapter over a directory holding 'pack.json' ("{}") and 'lenses/a.md' ("alpha"), and one entry 'lenses/out.md' that points outside it.</summary>
    /// <returns>The adapter and the directory.</returns>
    protected abstract ValueTask<(IPackDirectories Port, string Directory)> Create();

    /// <summary>An adapter and a directory it can't read.</summary>
    /// <returns>The adapter and the directory.</returns>
    protected abstract ValueTask<(IPackDirectories Port, string Directory)> CreateMissing();

    /// <summary>The files come back by their path inside the pack.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_files_come_back_by_their_path_inside_the_pack()
    {
        var (port, directory) = await Create();

        var read = await port.Read(directory, Token);

        Expect.True(read.IsSuccess);
        Expect.Equal("{}", read.Value.Files["pack.json"]);
        Expect.Equal("alpha", read.Value.Files["lenses/a.md"]);
    }

    /// <summary>An entry that points outside the pack is named, not read.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_entry_pointing_outside_is_named_and_not_read()
    {
        var (port, directory) = await Create();

        var read = await port.Read(directory, Token);

        Expect.True(read.IsSuccess);
        Expect.Sequence(["lenses/out.md"], read.Value.Outside);
        Expect.True(!read.Value.Files.ContainsKey("lenses/out.md"));
    }

    /// <summary>A directory that can't be read is an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_directory_that_cannot_be_read_is_an_error()
    {
        var (port, directory) = await CreateMissing();

        var read = await port.Read(directory, Token);

        Expect.True(read.IsError);
    }
}
