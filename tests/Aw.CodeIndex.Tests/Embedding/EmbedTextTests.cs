using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Embedding;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Tests.Embedding;

[Trait("Lane", "unit")]
public sealed class EmbedTextTests
{
    private static readonly SymbolRef _ref = SymbolRef.From("api:src/A.cs#A.Run");

    [Fact]
    public void The_text_is_the_name_then_the_signature_then_the_body()
        => Assert.Equal("Run\nvoid Run()\n{ Stop(); }", EmbedText.Of(new EmbedCandidate(_ref, "Run", "void Run()", "{ Stop(); }")));

    [Fact]
    public void A_long_text_is_cut_at_the_embed_limit()
    {
        var text = EmbedText.Of(new EmbedCandidate(_ref, "Run", "void Run()", new string('x', CodeIndexRules.EmbedTextMaxCharacters + 1_000)));

        Assert.Equal(CodeIndexRules.EmbedTextMaxCharacters, text.Length);
        Assert.StartsWith("Run\nvoid Run()\nxxx", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_text_at_the_limit_is_kept_whole()
    {
        var body = new string('x', CodeIndexRules.EmbedTextMaxCharacters - "Run\nvoid Run()\n".Length);

        Assert.Equal(CodeIndexRules.EmbedTextMaxCharacters, EmbedText.Of(new EmbedCandidate(_ref, "Run", "void Run()", body)).Length);
    }
}
