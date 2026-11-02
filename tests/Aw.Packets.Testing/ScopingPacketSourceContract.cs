using Aw.Packets.Core;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Scoping;
using Xunit;

namespace Aw.Packets.Testing;

/// <summary>The IScopingPacketSource contract, run against the in-memory fake and the disk adapter.</summary>
public abstract class ScopingPacketSourceContract
{
    /// <summary>Every listed file is read with its bytes, and nothing is unlisted.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_scope_packet_reads_all_fourteen_files()
    {
        var files = A.ScopingPacket().Build();

        var read = await Create(files).Read(Directory, CancellationToken.None);

        Expect.That(read.IsSuccess, "a well-formed directory reads");
        Expect.That(read.Value.Present.Count == PacketKinds.Scoping.Files.Count, "all fourteen files are present");
        Expect.That(files.All(f => read.Value.Present[f.Key].Span.SequenceEqual(f.Value)), "each file's bytes are as written");
        Expect.That(read.Value.Unlisted.Count == 0, "nothing else is in the directory");
    }

    /// <summary>A missing listed file is absent from the files, not an error: validate reports it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_file_is_absent_not_an_error()
    {
        var files = A.ScopingPacket().Without("lenses.json").Build();

        var read = await Create(files).Read(Directory, CancellationToken.None);

        Expect.That(read.IsSuccess && !read.Value.Present.ContainsKey("lenses.json") && read.Value.Present.Count == 14, "the other fourteen are read");
    }

    /// <summary>Other entries are named, sorted, with a sub-directory ending in '/'.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Other_entries_are_unlisted_sorted_and_a_directory_ends_in_a_slash()
    {
        var files = A.ScopingPacket().With("notes.txt", "x").With("zeta.md", "x").With("attachments/a.png", "x").Build();

        var read = await Create(files).Read(Directory, CancellationToken.None);

        Expect.That(read.IsSuccess && read.Value.Unlisted.SequenceEqual(["attachments/", "notes.txt", "zeta.md"], StringComparer.Ordinal),
            "unlisted entries are the other top-level names, sorted by ordinal");
    }

    /// <summary>A directory that doesn't exist is AW-PACKETS-002.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_directory_is_AW_PACKETS_002()
    {
        var read = await Create(A.ScopingPacket().Build()).Read(MissingDirectory, CancellationToken.None);

        Expect.That(read.IsError && read.Error.Code == PacketsErrors.NotFound.Code, "a missing directory is AW-PACKETS-002");
    }

    /// <summary>The packet directory this contract reads.</summary>
    protected abstract string Directory { get; }

    /// <summary>A packet directory that doesn't exist.</summary>
    protected abstract string MissingDirectory { get; }

    /// <summary>Creates the source with <paramref name="files"/> at <see cref="Directory"/>.</summary>
    /// <param name="files">The files by relative path.</param>
    /// <returns>The source under test.</returns>
    protected abstract IScopingPacketSource Create(IReadOnlyDictionary<string, byte[]> files);
}
