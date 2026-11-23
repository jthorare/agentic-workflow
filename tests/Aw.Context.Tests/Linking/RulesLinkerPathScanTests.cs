using System.Globalization;
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
        "src/\U0001F600/x.ts src/x\U0001F600.ts \U0001D49C/b.ts src/x.t\U0001F600",   // #3864: a surrogate pair is two non-word chars, for the regex and the scan
        "see https://github.com/a/b.ts and http://x.io/y.md or file:///etc/z.conf",   // #3864: a URL's '//' and its host
        "src/a.ts\nsrc/b.ts\r\nlib/c/d.md\n\nsrc/e.ts",   // #3864: newlines end a run
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
            Assert.True(WordChar.IsMatch(one) == RulesLinker.IsWordChar((char)c), $"U+{c:X4}");
        }
    }

    // #3864: a run of path chars with no '/' holds no path, so prose costs the scan nothing but its answer list.
    [Fact, Trait("Lane", "unit")]
    public void A_text_with_no_slash_allocates_only_the_answer()
    {
        var prose = string.Join(' ', Enumerable.Repeat("plain words with dots like v1.2 and names-like-this", 200));
        _ = RulesLinker.RepoPaths(prose);   // warm: the first call may compile

        var before = GC.GetAllocatedBytesForCurrentThread();
        var found = RulesLinker.RepoPaths(prose);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Empty(found);
        Assert.True(allocated < 1024, string.Create(CultureInfo.InvariantCulture, $"{allocated} bytes for {prose.Length} chars of prose"));
    }

    private static List<string> Scan(string text) => [.. RulesLinker.RepoPaths(text).Select(p => text.Substring(p.Index, p.Length))];

    private static List<string> OldMatches(string text) => [.. OldRepoPath.Matches(text).Select(m => m.Value)];

    // The pattern the scan replaced, kept here as the answer key.
    [GeneratedRegex(@"(?<![\w/.\-])(?:[\w.\-]+/)+[\w\-]+(?:\.[A-Za-z0-9]+)+(?![\w/\-])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 10_000)]
    private static partial Regex OldRepoPath { get; }

    [GeneratedRegex(@"^\w$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 10_000)]
    private static partial Regex WordChar { get; }
}
