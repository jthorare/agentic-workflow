using System.Text;
using System.Text.RegularExpressions;
using Aw.Context.Core.Linking;

namespace Aw.Context.Tests.Linking;

/// <summary>The repo-path rule's hand scan finds exactly what the regex it replaced found (#3816), and it runs no regex, so no clock.</summary>
public sealed partial class RulesLinkerPathScanTests
{
    public static TheoryData<string> Texts =>
    [
        "Implemented in src/layout/SplitPane.ts, nothing else.",
        "see src/a.ts. then b/c.d.e and x/y.tar.gz",
        "src/a.b.c/x",
        "src/a.b_c",
        "src/a.b-c and src/a.b/c and src/a.b.c-d",
        "a//b.ts /abs/path.md ./rel/x.ts ../up/y.ts",
        "src/.hidden/x.ts src/x.ts/ src/x.ts- src/x.ts_",
        "no/ext no.ext plain words",
        "é/ü.ts src/naïve/file.md src/日本/x.ts",
        "a/b.ts/c.ts a/b.ts.c/d.ts a.b/c.d",
        "src/x.ts,src/y.ts;src/z.ts(src/w.ts)",
        "src/x.é src/x.té src/x.1.2.3",
        "",
        "/",
        "a/.b",
        "a/b..ts a/b.ts.. a/-b.ts a/b-.ts-",
    ];

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Texts))]
    public void The_scan_finds_what_the_regex_found(string text) => Assert.Equal(OldMatches(text), Scan(text));

    [Fact, Trait("Lane", "unit")]
    public void The_scan_finds_what_the_regex_found_on_random_text_from_path_chars()
    {
        const string Alphabet = "aZ9_-./ é́,x.";
        var random = new Random(3816);
        for (var n = 0; n < 5_000; n++)
        {
            var text = new StringBuilder();
            for (var i = random.Next(1, 30); i > 0; i--)
            {
                _ = text.Append(Alphabet[random.Next(Alphabet.Length)]);
            }

            Assert.Equal(OldMatches(text.ToString()), Scan(text.ToString()));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_word_char_is_what_the_regex_calls_one_for_every_char()
    {
        Span<char> one = stackalloc char[1];
        for (var c = 0; c <= char.MaxValue; c++)
        {
            one[0] = (char)c;
            Assert.True(WordChar().IsMatch(one) == RulesLinker.IsWordChar((char)c), $"U+{c:X4}");
        }
    }

    private static List<string> Scan(string text) => [.. RulesLinker.RepoPaths(text).Select(p => text.Substring(p.Index, p.Length))];

    private static List<string> OldMatches(string text) => [.. OldRepoPath().Matches(text).Select(m => m.Value)];

    // The pattern the scan replaced, kept here as the answer key.
    [GeneratedRegex(@"(?<![\w/.\-])(?:[\w.\-]+/)+[\w\-]+(?:\.[A-Za-z0-9]+)+(?![\w/\-])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 10_000)]
    private static partial Regex OldRepoPath();

    [GeneratedRegex(@"^\w$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 10_000)]
    private static partial Regex WordChar();
}
