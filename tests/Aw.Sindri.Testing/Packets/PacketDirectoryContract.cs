using Aw.Sindri.Core.Packets;
using Xunit;

namespace Aw.Sindri.Testing.Packets;

/// <summary>The contract every <see cref="IPacketDirectory"/> honours: what <see cref="IPacketDirectory.Create"/> wrote reads back, a replay replaces it, a bad name or a missing directory is an error.</summary>
public abstract class PacketDirectoryContract
{
    private const string Address = "T-1/build/1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Dictionary<string, string> _files = new(StringComparer.Ordinal) { ["phase.json"] = "{}", ["design.md"] = "# d" };

    /// <summary>A port over an empty store.</summary>
    /// <returns>The port.</returns>
    protected abstract IPacketDirectory Create();

    /// <summary>A directory no packet was ever written to.</summary>
    protected abstract string Nothing { get; }

    /// <summary>The files a create wrote read back from the directory it returned.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task What_was_created_reads_back()
    {
        var files = Create();

        var made = await files.Create(Address, "packets/p1-a1", _files, Token);
        var read = await files.Read(made.Value, Token);

        Expect.True(made.IsSuccess);
        Expect.True(read.IsSuccess);
        Expect.Equal(2, read.Value.Count);
        Expect.Equal("# d", read.Value["design.md"]);
    }

    /// <summary>A second create of the same name replaces the first's files.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_second_create_replaces_the_first()
    {
        var files = Create();
        _ = await files.Create(Address, "packets/p1-a1", _files, Token);

        var made = await files.Create(Address, "packets/p1-a1", new Dictionary<string, string>(StringComparer.Ordinal) { ["phase.json"] = "{\"x\":1}" }, Token);
        var read = await files.Read(made.Value, Token);

        Expect.Equal("{\"x\":1}", read.Value["phase.json"]);
    }

    /// <summary>A name that climbs out of the run area, or a file name with a separator, is an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_name_that_climbs_out_is_refused()
    {
        var files = Create();

        var name = await files.Create(Address, "../packets", _files, Token);
        var file = await files.Create(Address, "packets/p1", new Dictionary<string, string>(StringComparer.Ordinal) { ["a/b.json"] = "{}" }, Token);

        Expect.True(name.IsError);
        Expect.True(file.IsError);
    }

    /// <summary>A directory that does not exist is an error, not an empty packet.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_directory_is_an_error()
    {
        var read = await Create().Read(Nothing, Token);

        Expect.True(read.IsError);
    }
}
