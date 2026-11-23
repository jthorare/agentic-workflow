using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Redaction.Tests;

/// <summary>
/// The rule gates (RuleGate) skip a rule's regex only when it can't match. A gate that skips a text its regex matches
/// leaks a secret, so every gate is checked against its own regex, and the gated scrub against the ungated one (the
/// scan as it ran before the gates), over the whole corpus and a seeded set of generated texts.
/// </summary>
[Trait("Lane", "unit")]
public sealed partial class RuleGateTests
{
    private static readonly Redactor _ungated =
        new([.. BuiltinRules.All.Select(r => r with { Gate = null })], Redactor.DefaultMatchTimeout);

    // A rule without a gate still runs (safe, only slower); a new built-in rule gets its gate derived with it.
    [Fact]
    public void Every_builtin_rule_has_a_gate() => Assert.All(BuiltinRules.All, r => Assert.NotNull(r.Gate));

    [Fact]
    public void A_caseless_rule_compares_letters_only_in_any_case()
    {
        // An exact clause in an IgnoreCase rule would miss "BEARER" for "bearer": only letterless literals may be exact.
        // An inline (?i) turns IgnoreCase on where Options never shows it, so no built-in pattern may hold one.
        Assert.All(BuiltinRules.All, r => Assert.DoesNotContain("(?i", r.Pattern, StringComparison.Ordinal));
        foreach (var rule in BuiltinRules.All.Where(r => r.Options.HasFlag(RegexOptions.IgnoreCase)))
        {
            var exact = rule.Gate!.Clauses.Where(c => !c.Caseless).SelectMany(c => c.Literals);
            Assert.All(exact, literal => Assert.DoesNotContain(literal, char.IsLetter));
        }
    }

    [Fact]
    public void No_gate_skips_a_text_its_regex_matches()
    {
        var checkedMatches = 0;
        foreach (var rule in BuiltinRules.All)
        {
            var regex = new Regex(rule.Pattern, rule.Options | CompiledRules.Always, TimeSpan.FromSeconds(10));
            foreach (var text in Texts().SelectMany(t => (string[])[t, FoldedText.Fold(t).Text]))
            {
                if (regex.IsMatch(text))
                {
                    checkedMatches++;
                    Assert.True(rule.Gate!.MayMatch(text, IsAscii(text)), $"rule {rule.Id} /{rule.Pattern}/ gate skipped a match: {text}");
                }
            }
        }

        Assert.True(checkedMatches > 500, "too few matches to prove the gates: " + checkedMatches.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void The_gated_scrub_returns_what_the_ungated_scrub_returns()
    {
        foreach (var text in Texts())
        {
            var gated = Redactor.Default.Scrub(text);
            var ungated = _ungated.Scrub(text);

            Assert.Equal(ungated.Outcome, gated.Outcome);
            Assert.Equal(ungated.Text, gated.Text);
            Assert.Equal(ungated.Hits, gated.Hits);
        }
    }

    [Theory]
    [InlineData("The build passed on the second try; see the log.")]
    [InlineData("Started review of the login page")]
    [InlineData("waiting for checks to finish")]
    [InlineData("")]
    public void Plain_status_text_skips_every_rule(string text) =>
        Assert.All(BuiltinRules.All, r => Assert.False(r.Gate!.MayMatch(text, IsAscii(text)), r.Id));   // a status line never reaches a regex

    [Fact]
    public void A_caseless_clause_holds_on_any_non_ascii_text()
    {
        // The regex's IgnoreCase takes the Kelvin sign for k, so "toKen" is a token to it.
        var text = "toKen=" + Shapes.Body;
        var clause = RuleGateClause.AnyCase("token");

        Assert.True(clause.MayHold(text, ascii: false));
        Assert.Matches(CaselessToken, text);
    }

    [Fact]
    public void Every_builtin_rule_has_samples_its_regex_matches()
    {
        var unknown = _samples.Keys.Except(BuiltinRules.All.Select(r => r.Id), StringComparer.Ordinal).ToArray();
        Assert.True(unknown.Length == 0, "samples name unknown rule ids: " + string.Join(", ", unknown));
        foreach (var (rule, samples) in RulesWithSamples())
        {
            var regex = RegexFor(rule);
            Assert.All(samples, sample => Assert.True(regex.IsMatch(sample), $"rule {rule.Id} /{rule.Pattern}/ misses its sample: {sample}"));
        }
    }

    [Fact]
    public void Each_gate_clause_is_required_by_its_rule()
    {
        // Pins every clause to its pattern: a sample with all of a clause's literals stripped no longer matches. A clause
        // the pattern doesn't need (a stale gate after a pattern edit, a literal from one branch only) leaves a sample
        // that still matches, and this fails. Lookarounds hold too: the literal is stripped from the whole text.
        foreach (var (rule, samples) in RulesWithSamples())
        {
            var regex = RegexFor(rule);
            foreach (var clause in rule.Gate!.Clauses)
            {
                foreach (var sample in samples)
                {
                    var stripped = Strip(sample, clause);
                    Assert.False(regex.IsMatch(stripped),
                        $"rule {rule.Id} /{rule.Pattern}/ still matches without [{string.Join(' ', clause.Literals)}]: {stripped}");
                }
            }
        }
    }

    // Each built-in rule with its samples: the rules of one id take that id's groups in order. A rule without a group
    // fails with its own id.
    private static IEnumerable<(RedactionRule Rule, string[] Samples)> RulesWithSamples()
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var rule in BuiltinRules.All)
        {
            var nth = seen.GetValueOrDefault(rule.Id);
            seen[rule.Id] = nth + 1;
            var groups = _samples.GetValueOrDefault(rule.Id) ?? [];
            Assert.True(nth < groups.Length, $"rule {rule.Id} (#{(nth + 1).ToString(CultureInfo.InvariantCulture)} with that id) has no samples");
            Assert.NotEmpty(groups[nth]);
            yield return (rule, groups[nth]);
        }
    }

    private static Regex RegexFor(RedactionRule rule) =>
        new(rule.Pattern, rule.Options | CompiledRules.Always, TimeSpan.FromSeconds(10));

    // Removes every literal of the clause, again until none is left (a removal can glue a new one together).
    private static string Strip(string sample, RuleGateClause clause)
    {
        var comparison = clause.Caseless ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var text = sample;
        for (var pass = 0; pass < 100 && clause.MayHold(text, ascii: true); pass++)
        {
            foreach (var literal in clause.Literals)
            {
                text = text.Replace(literal, string.Empty, comparison);
            }
        }

        return text;
    }

    [GeneratedRegex("token", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CaselessToken { get; }

    private static bool IsAscii(string text) => !text.AsSpan().ContainsAnyExceptInRange('\u0000', '\u007F');

    // The corpus inputs, the pass-through texts, edge texts and the generated texts.
    private static IEnumerable<string> Texts()
    {
        var corpus = Corpus.Plain().Concat(Corpus.LongAndShellShapes()).Concat(Corpus.Evasions())
            .Select(row => row.Data.Input);
        var clean = PassThroughTests.NotSecrets().Select(row => row.Data);
        string[] edges =
        [
            string.Empty,
            new string('a', 20_000),
            "sk-" + Shapes.Body,
            Shapes.Body + " sk-" + Shapes.Body,
            "BEARER " + Shapes.Body,
            "mr" + "n 1234567",
            "PASSWORD=" + Shapes.Body,
            "x\n  Client.Secret: " + Shapes.Body + "\n",
            "été api_key=" + Shapes.Body,
        ];
        return corpus.Concat(clean).Concat(edges).Concat(Generated());
    }

    // Seeded texts glued from segments of a name or prefix, a separator and a value, taken from the rules' literals and
    // look-alikes, in mixed case, so every rule matches some of them and misses others close to a match.
    private static IEnumerable<string> Generated()
    {
        string[] names =
        [
            "sk-", "sk-ant-", "sk-proj-", "AKIA", "ASIA", "ghp_", "gho_", "github_pat_", "xoxb-", "sk_live_", "rk_test_", "AIza",
            "lin_api_", "lin_oauth_", "eyJ", "ya29.", "Bearer", "Authorization", "proxy-authorization", "https://u", "redis://",
            "API_KEY", "api-key", "Secret", "CLIENT_SECRET", "TOKEN", "my.password", "private_key", "access_key", "X-Api-Key",
            "aws_secret_access_key", "aws_session_token", "MRN", "SSN 123", "-----BEGIN RSA PRIV" + "ATE KEY-----", "-----END ",
            "[REDACTED:github-token]", "export token", "?access_token", "&tok", "#token", "to\u212Aen", "\u017Fecret", "\u0130",
            "\u0455k_live_", "g\u04BBp_", "\uFF47hp_", "gh\u200Bp_", "word", string.Empty,
        ];
        string[] separators =
        [
            string.Empty, "=", ":", " = ", ": ", "=\"", ": '", " ", "\t", "\n", "\r\n", ":-", "-", "@", ":x@", "\\n", "\u00A0", ".",
        ];
        string[] values =
        [
            Shapes.Body, Shapes.Body + Shapes.Body, Shapes.Tail, "abcdefghijklmnopqrstuvwxyz", "45-6789", "12345678",
            "Ab3dE6gH9jK2mN5p." + Shapes.Body, "ABCDEFGHIJKLMNOP", "Bas" + "ic dXNlcjpw" + "YXNzd29yZDEyMzQ=", "host/x", "\u00E9t\u00E9",
            "." + Shapes.Body + "." + Shapes.Body, string.Empty,
        ];
        var random = new Random(2900);
        for (var i = 0; i < 4_000; i++)
        {
            var text = new System.Text.StringBuilder();
            for (var segment = random.Next(1, 4); segment > 0; segment--)
            {
                var part = names[random.Next(names.Length)] + separators[random.Next(separators.Length)] + values[random.Next(values.Length)];
                _ = text.Append(random.Next(4) switch
                {
                    0 => part.ToUpperInvariant(),
                    1 => part.ToLowerInvariant(),
                    _ => part,
                }).Append(separators[random.Next(separators.Length)]);
            }

            yield return text.ToString();
        }
    }
}
