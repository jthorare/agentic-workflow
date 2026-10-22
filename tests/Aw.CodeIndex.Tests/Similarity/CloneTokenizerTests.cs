using Aw.CodeIndex.Core.Similarity;

namespace Aw.CodeIndex.Tests.Similarity;

public sealed class CloneTokenizerTests
{
    [Fact, Trait("Lane", "unit")]
    public void Tokenize_keeps_identifiers_and_splits_punctuation()
        => Assert.Equal(["int", "Add", "(", "int", "a", ",", "int", "b", ")", "{", "return", "a", "+", "b", ";", "}"],
            CloneTokenizer.Tokenize("int Add(int a, int b) { return a + b; }"));

    [Fact, Trait("Lane", "unit")]
    public void Tokenize_blanks_numbers_and_strings()
        => Assert.Equal(["x", "=", "0", ";", "y", "=", "\"", ";", "z", "=", "\"", ";"],
            CloneTokenizer.Tokenize("x = 42; y = \"a \\\" b\"; z = 'c';"));

    [Fact, Trait("Lane", "unit")]
    public void Tokenize_drops_line_block_and_hash_comments()
        => Assert.Equal(["a", ";", "b", ";", "c", ";"],
            CloneTokenizer.Tokenize("a; // one\n/* two\nlines */ b;\n# three\nc;"));

    [Fact, Trait("Lane", "unit")]
    public void Tokenize_survives_unterminated_strings_and_comments()
    {
        Assert.Equal(["a", "\""], CloneTokenizer.Tokenize("a \"never closed\nb"));
        Assert.Equal(["a"], CloneTokenizer.Tokenize("a /* never closed"));
        Assert.Empty(CloneTokenizer.Tokenize(""));
    }

    [Fact, Trait("Lane", "unit")]
    public void TokenizeLines_reads_only_the_span()
    {
        const string text = "one\ntwo\r\nthree\nfour";

        Assert.Equal(["two", "three"], CloneTokenizer.TokenizeLines(text, 2, 3));
        Assert.Equal(["four"], CloneTokenizer.TokenizeLines(text, 4, 99));
        Assert.Empty(CloneTokenizer.TokenizeLines(text, 9, 12));
        Assert.Empty(CloneTokenizer.TokenizeLines(text, 3, 2));
    }
}
