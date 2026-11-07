using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Infrastructure.Embedding;

namespace Aw.CodeIndex.Tests.Embedding;

/// <summary>The lock file against the pins the build carries (the 'code-index:lock' doctor row).</summary>
public sealed class FileCodeIndexPinsTests
{
    private static readonly string[] _shipped =
    [
        "# a comment",
        "graphify 0.9.67 2026-09-24T00:00:00Z ebe4304d077efc000bef00c0833d3c0161ff3d9aaae2011652e329c5cd16920a",
        "ollama-model nomic-embed-text:latest sha256:0a109f422b47e3a30ba2b10eca18548e944e8a23073ee3f3e947efcf3c45e59f",
        "need disk-bytes 1073741824",
    ];

    [Fact, Trait("Lane", "unit")]
    public void The_shipped_rows_match() => Assert.Equal(LockState.Matches, FileCodeIndexPins.Compare(_shipped));

    [Fact, Trait("Lane", "unit")]
    public void A_different_model_digest_is_a_mismatch() =>
        Assert.Equal(LockState.Mismatch, FileCodeIndexPins.Compare([_shipped[1], "ollama-model nomic-embed-text:latest sha256:00", _shipped[3]]));

    [Fact, Trait("Lane", "unit")]
    public void A_missing_row_or_an_unknown_one_does_not_parse()
    {
        Assert.Equal(LockState.Unparsable, FileCodeIndexPins.Compare([_shipped[1], _shipped[2]]));
        Assert.Equal(LockState.Unparsable, FileCodeIndexPins.Compare([.. _shipped, "mystery 1"]));
    }
}
