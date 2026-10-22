using System.Text;
using Aw.Packets.Core;
using Xunit;

namespace Aw.Packets.Testing;

/// <summary>The ISealSource contract, run against the in-memory fake and the disk adapter.</summary>
public abstract class SealSourceContract
{
    /// <summary>The bytes of a seal file as Sindri writes it, naming <see cref="LinePacketBuilder.Hash"/> as the set.</summary>
    protected static byte[] SealBytes(string setHash = "") => Encoding.UTF8.GetBytes(
        $"{{ \"schemaVersion\": 1, \"phase\": \"p1\", \"setHash\": \"{(setHash.Length == 0 ? LinePacketBuilder.Hash : setHash)}\", \"planHash\": \"{LinePacketBuilder.OtherHash}\" }}\n");

    /// <summary>A good file reads as the set hash it names.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_seal_file_reads_as_its_set_hash()
    {
        var read = await Create("seal.json", SealBytes()).Read("seal.json", CancellationToken.None);

        Expect.That(read.IsSuccess && read.Value.Value == LinePacketBuilder.Hash, "the set hash is as written");
    }

    /// <summary>A missing file is AW-PACKETS-010.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_file_is_AW_PACKETS_010()
    {
        var read = await Create("seal.json", SealBytes()).Read("elsewhere.json", CancellationToken.None);

        Expect.That(read.IsError && read.Error.Code == PacketsErrors.SealUnusable.Code, "a missing file is AW-PACKETS-010");
    }

    /// <summary>Each way a file can be wrong is AW-PACKETS-010.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\": 2, \"setHash\": \"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}")]
    [InlineData("{\"schemaVersion\": 1}")]
    [InlineData("{\"schemaVersion\": 1, \"setHash\": \"sha256:aa\"}")]
    [InlineData("{\"schemaVersion\": 1, \"setHash\": 7}")]
    public async Task A_bad_file_is_AW_PACKETS_010(string text)
    {
        var read = await Create("seal.json", Encoding.UTF8.GetBytes(text)).Read("seal.json", CancellationToken.None);

        Expect.That(read.IsError && read.Error.Code == PacketsErrors.SealUnusable.Code, "a bad file is AW-PACKETS-010");
    }

    /// <summary>Creates a source holding one file.</summary>
    /// <param name="path">Its path.</param>
    /// <param name="bytes">Its bytes.</param>
    /// <returns>The source under test.</returns>
    protected abstract ISealSource Create(string path, byte[] bytes);
}
