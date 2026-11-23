using Aw.Kernel.Testing;
using Aw.Sindri.Core.Evolve;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Tests.Evolve;

/// <summary>The leak gate: the rewrite and judge checks, then the deny layers in order; per layer a hit, a near miss and look-alike spellings.</summary>
[Lane(TestLane.Unit)]
public sealed class LeakGateTests
{
    private static readonly HashSet<string> _english = new(["ledger", "client", "service", "billing", "review"], StringComparer.Ordinal);

    private sealed class SecretScrubber : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("sk-live-123456", "[REDACTED]", StringComparison.Ordinal);
    }

    private static PrivacyRules Rules(string[]? terms = null, string[]? patterns = null, string[]? names = null, IReadOnlySet<string>? words = null) =>
        new(terms ?? [], patterns ?? [], names ?? [], words ?? _english);

    private static LeakVerdict Check(Proposal rewrite, PrivacyRules? rules = null, LeakSource[]? sources = null, Proposal? original = null, bool judge = false) =>
        new LeakGate(new SecretScrubber()).Check(new GateInput(original ?? rewrite, rewrite, judge, sources ?? []), rules ?? Rules());

    [Fact]
    public void A_general_rewrite_passes() => Assert.Equal(LeakVerdict.Clean, Check(Proposals.Saying("Run the tests before saying done")));

    [Fact]
    public void A_rewrite_that_changes_the_target_is_held_by_rewrite()
    {
        var original = Proposals.Saying("Run the tests");
        Assert.Equal(LeakLayer.Rewrite, Check(original with { Target = "skill:judge" }, original: original).Layer);
    }

    [Fact]
    public void A_rewrite_that_changes_the_evidence_is_held_by_rewrite()
    {
        var original = Proposals.Saying("Run the tests");
        Assert.Equal(LeakLayer.Rewrite, Check(original with { Evidence = ["transcript:ffffffff#1"] }, original: original).Layer);
    }

    [Fact]
    public void A_rewrite_that_reorders_the_evidence_passes()
    {
        var original = Proposals.Saying("Run the tests") with { Evidence = ["transcript:a#1", "transcript:b#2"] };
        Assert.False(Check(original with { Evidence = ["transcript:b#2", "transcript:a#1"] }, original: original).Held);
    }

    [Fact]
    public void A_judge_leak_holds_before_any_deny_layer() =>
        Assert.Equal(LeakLayer.Judge, Check(Proposals.Saying("Ask before touching Zorblax"), Rules(terms: ["Zorblax"]), judge: true).Layer);

    [Theory]
    [InlineData("Ask before touching AcmeCare")]
    [InlineData("Ask before touching acme care")]
    [InlineData("Ask before touching acme-care")]
    [InlineData("Ask before touching acme\u2011care")]
    [InlineData("Ask before touching acmecare")]
    [InlineData("Ask before touching ACME\u00A0CARE")]
    [InlineData("Ask before touching ac\u200Bme care")]
    [InlineData("Ask before touching \uFF21cme Care")]
    public void A_two_part_term_holds_in_every_spelling(string title) =>
        Assert.Equal(LeakLayer.Terms, Check(Proposals.Saying(title), Rules(terms: ["AcmeCare"])).Layer);

    [Theory]
    [InlineData("Wrap the AcmeClient")]
    [InlineData("Wrap the ACMEClient")]
    [InlineData("Call useAcme first")]
    [InlineData("Bump acme2")]
    public void A_one_part_term_holds_inside_a_compound_name(string title) =>
        Assert.Equal(LeakLayer.Terms, Check(Proposals.Saying(title), Rules(terms: ["Acme"])).Layer);

    [Theory]
    [InlineData("Visit Acmeville")]
    [InlineData("visit acmeville")]
    public void A_one_part_term_does_not_hold_a_longer_word(string title) =>
        Assert.False(Check(Proposals.Saying(title), Rules(terms: ["Acme"])).Held);

    [Fact]
    public void A_term_in_the_rationale_or_the_description_holds_too()
    {
        Assert.Equal(LeakLayer.Terms, Check(Proposals.Saying("Run tests", rationale: "Zorblax wanted it."), Rules(terms: ["Zorblax"])).Layer);
        Assert.Equal(LeakLayer.Terms, Check(Proposals.Saying("Run tests", description: "As in Zorblax."), Rules(terms: ["Zorblax"])).Layer);
    }

    [Fact]
    public void A_pattern_match_holds_case_insensitively() =>
        Assert.Equal(LeakLayer.Patterns, Check(Proposals.Saying("Fix it the way abc-1234 did"), Rules(patterns: [@"ABC-\d+"])).Layer);

    [Fact]
    public void A_pattern_near_miss_passes() =>
        Assert.False(Check(Proposals.Saying("Fix it the way abc-x did"), Rules(patterns: [@"ABC-\d+"])).Held);

    [Fact]
    public void A_pattern_that_does_not_compile_holds_every_proposal() =>
        Assert.Equal(LeakLayer.Patterns, Check(Proposals.Saying("Run the tests"), Rules(patterns: ["(unclosed"])).Layer);

    [Fact]
    public void A_pattern_matches_the_visible_form() =>
        Assert.Equal(LeakLayer.Patterns, Check(Proposals.Saying("Fix it like ABC\u200B-12"), Rules(patterns: [@"ABC-\d+"])).Layer);

    [Theory]
    [InlineData("Read novaledger before you start")]
    [InlineData("Wrap the NovaLedgerClient")]
    [InlineData("Call createNovaLedger() first")]
    [InlineData("Import @acme/nova-ledger-client")]
    [InlineData("Edit nova_ledger.ts")]
    [InlineData("Ask NOVALedger")]
    [InlineData("Bump nova-ledger-v2")]
    public void A_private_name_holds_in_every_spelling(string title) =>
        Assert.Equal(LeakLayer.Identifiers, Check(Proposals.Saying(title), Rules(names: ["nova-ledger"])).Layer);

    [Fact]
    public void A_longer_name_that_contains_the_private_one_passes() =>
        Assert.False(Check(Proposals.Saying("Wrap the SupernovaLedger"), Rules(names: ["nova-ledger"])).Held);

    [Fact]
    public void A_private_file_path_in_the_change_holds() =>
        Assert.Equal(
            LeakLayer.Identifiers,
            Check(Proposals.Saying("Run tests") with { Change = new ProposalChange(["skills/review/SKILL.md"], "Mention the quarkpipe folder.") }, Rules(names: ["quarkpipe"])).Layer);

    [Fact]
    public void Private_names_under_five_characters_are_not_checked() =>
        Assert.False(Check(Proposals.Saying("Wrap the zork client"), Rules(names: ["zork"])).Held);

    [Fact]
    public void A_common_word_among_the_private_names_is_not_checked() =>
        Assert.False(Check(Proposals.Saying("Update the ledger"), Rules(names: ["ledger"])).Held);

    [Fact]
    public void With_no_word_list_a_common_word_among_the_private_names_holds() =>
        Assert.Equal(LeakLayer.Identifiers, Check(Proposals.Saying("Update the ledger"), Rules(names: ["ledger"], words: new HashSet<string>(StringComparer.Ordinal))).Layer);

    [Fact]
    public void Eight_words_copied_from_a_turn_hold()
    {
        var source = new LeakSource("transcript:1a2b3c4d", ["No. You always skip the integration lane when the build is green, stop that."]);
        Assert.Equal(LeakLayer.Verbatim, Check(Proposals.Saying("Never skip the integration lane when the build is green, stop"), sources: [source]).Layer);
    }

    [Fact]
    public void Seven_shared_words_pass()
    {
        var source = new LeakSource("transcript:1a2b3c4d", ["No. You always skip the integration lane when the build is green, stop that."]);
        Assert.False(Check(Proposals.Saying("Never skip the integration lane when the build passes"), sources: [source]).Held);
    }

    [Fact]
    public void A_copy_that_crosses_two_turns_of_one_session_holds()
    {
        var source = new LeakSource("transcript:1a2b3c4d", ["please run the whole suite", "before you open the pull request today"]);
        Assert.Equal(LeakLayer.Verbatim, Check(Proposals.Saying("Agents should run the whole suite before you open the pull request"), sources: [source]).Layer);
    }

    [Fact]
    public void A_copy_that_crosses_two_sessions_passes()
    {
        LeakSource[] sources = [new("transcript:aaaaaaaa", ["please run the whole suite"]), new("transcript:bbbbbbbb", ["before you open the pull request today"])];
        Assert.False(Check(Proposals.Saying("Agents should run the whole suite before you open the pull request"), sources: sources).Held);
    }

    [Fact]
    public void A_whole_short_turn_copied_holds()
    {
        var source = new LeakSource("transcript:1a2b3c4d", ["use the staging quarkpipe bucket"]);
        Assert.Equal(LeakLayer.Verbatim, Check(Proposals.Saying("Always use the staging quarkpipe bucket"), sources: [source]).Layer);
    }

    [Fact]
    public void A_three_word_turn_copied_passes()
    {
        var source = new LeakSource("transcript:1a2b3c4d", ["run the tests"]);
        Assert.False(Check(Proposals.Saying("Always run the tests first"), sources: [source]).Held);
    }

    [Fact]
    public void A_copy_with_other_punctuation_and_case_still_holds()
    {
        var source = new LeakSource("transcript:1a2b3c4d", ["Don't merge until the canary has been green for an hour"]);
        Assert.Equal(LeakLayer.Verbatim, Check(Proposals.Saying("DON'T MERGE until the canary has\u00A0been green, for an hour"), sources: [source]).Layer);
    }

    [Theory]
    [InlineData("Mail someone@example.com first")]
    [InlineData("Read /home/someone/notes first")]
    [InlineData("Read /Users/someone/notes first")]
    [InlineData("Read C:\\Users\\someone\\notes first")]
    public void An_email_or_a_home_path_holds(string title) => Assert.Equal(LeakLayer.Personal, Check(Proposals.Saying(title)).Layer);

    [Fact]
    public void A_text_the_scrubber_would_change_holds() =>
        Assert.Equal(LeakLayer.Scrubber, Check(Proposals.Saying("Never paste keys like sk-live-123456")).Layer);

    [Fact]
    public void Layers_run_in_order_and_the_first_hit_wins()
    {
        var rewrite = Proposals.Saying("Zorblax abc-12 sk-live-123456 someone@example.com");
        Assert.Equal(LeakLayer.Terms, Check(rewrite, Rules(terms: ["Zorblax"], patterns: [@"abc-\d+"])).Layer);
        Assert.Equal(LeakLayer.Patterns, Check(rewrite, Rules(patterns: [@"abc-\d+"])).Layer);
        Assert.Equal(LeakLayer.Personal, Check(rewrite).Layer);
    }
}
