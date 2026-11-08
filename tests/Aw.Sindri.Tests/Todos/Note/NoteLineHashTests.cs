using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>The hash behind a removed todo's lastLineHash.</summary>
[Lane(TestLane.Unit)]
public sealed class NoteLineHashTests
{
    [Fact]
    public void It_is_the_lowercase_hex_sha256_of_the_utf8_line() =>
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", NoteLineHash.Of("abc"), StringComparer.Ordinal);

    [Fact]
    public void Different_lines_hash_differently() =>
        Assert.NotEqual(NoteLineHash.Of("- [ ] a"), NoteLineHash.Of("- [ ] a "), StringComparer.Ordinal);
}
