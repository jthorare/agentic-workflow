using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>The Agents filter box's parser and matcher (issue 3024): words and the tokens p: r: s: n: o:, with no Avalonia in sight.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentFilterQueryTests
{
    private static readonly AgentFilterTarget _sample = new(
        Name: "Fix the refund bug",
        Provider: "Claude Code claude-code",
        Repo: "api",
        State: "asking Waiting on you",
        Doing: "approval · Bash",
        Owner: "Lane sindri/ideas");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_typed_is_an_empty_query_that_keeps_every_row(string? text)
    {
        var query = AgentFilterQuery.Parse(text);

        Assert.True(query.IsEmpty);
        Assert.True(query.Matches(_sample));
    }

    [Theory]
    [InlineData("p:codex", "Providers")]
    [InlineData("r:web", "Repos")]
    [InlineData("s:asking", "States")]
    [InlineData("n:refund", "Names")]
    [InlineData("o:ideas", "Owners")]
    public void Each_token_fills_its_own_list(string text, string list)
    {
        var query = AgentFilterQuery.Parse(text);

        var value = text[2..];
        var lists = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Providers"] = query.Providers,
            ["Repos"] = query.Repos,
            ["States"] = query.States,
            ["Names"] = query.Names,
            ["Owners"] = query.Owners,
        };
        Assert.Equal([value], lists[list]);
        Assert.Empty(query.Words);
        foreach (var other in lists.Where(l => !string.Equals(l.Key, list, StringComparison.Ordinal)))
        {
            Assert.Empty(other.Value);
        }
    }

    [Fact]
    public void Words_and_tokens_mix_in_any_order()
    {
        var query = AgentFilterQuery.Parse("refund r:web   bash s:asking");

        Assert.Equal(["refund", "bash"], query.Words);
        Assert.Equal(["web"], query.Repos);
        Assert.Equal(["asking"], query.States);
    }

    [Fact]
    public void Case_is_ignored_in_the_token_and_in_its_value()
    {
        var query = AgentFilterQuery.Parse("P:CodeX REFUND");

        Assert.Equal(["codex"], query.Providers);
        Assert.Equal(["refund"], query.Words);
    }

    [Fact]
    public void A_token_the_page_does_not_know_stays_a_plain_word()
    {
        var query = AgentFilterQuery.Parse("x:foo bar");

        Assert.Equal(["x:foo", "bar"], query.Words);
        Assert.Empty(query.Providers);
    }

    [Fact]
    public void A_token_with_no_value_yet_narrows_nothing_while_it_is_being_typed()
    {
        var query = AgentFilterQuery.Parse("p:");

        Assert.True(query.IsEmpty);
        Assert.True(query.Matches(_sample));
    }

    [Fact]
    public void A_value_may_hold_a_colon() =>
        Assert.Equal(["a:b"], AgentFilterQuery.Parse("r:a:b").Repos);

    [Fact]
    public void State_blocked_means_asking_as_in_claudes_agent_view() =>
        Assert.Equal(["asking"], AgentFilterQuery.Parse("s:blocked").States);

    [Theory]
    [InlineData("refund", true)]
    [InlineData("REFUND", true)]
    [InlineData("api", true)]
    [InlineData("bash", true)]
    [InlineData("ideas", false)]    // the owner is for o: only, not for plain words
    [InlineData("codex", false)]    // the provider is for p: only
    [InlineData("refund api", true)]
    [InlineData("refund web", false)]
    public void Plain_words_match_as_substrings_of_the_name_the_repo_and_what_it_is_doing(string text, bool expected) =>
        Assert.Equal(expected, AgentFilterQuery.Parse(text).Matches(_sample));

    [Theory]
    [InlineData("p:claude", true)]
    [InlineData("p:codex", false)]
    [InlineData("r:api", true)]
    [InlineData("r:web", false)]
    [InlineData("s:asking", true)]
    [InlineData("s:waiting", true)]
    [InlineData("s:running", false)]
    [InlineData("s:blocked", true)]
    [InlineData("n:refund", true)]
    [InlineData("n:api", false)]    // n: looks at the name only
    [InlineData("o:ideas", true)]
    [InlineData("o:web", false)]
    public void Each_token_narrows_on_its_own_field(string text, bool expected) =>
        Assert.Equal(expected, AgentFilterQuery.Parse(text).Matches(_sample));

    [Fact]
    public void Tokens_and_words_all_have_to_hold()
    {
        Assert.True(AgentFilterQuery.Parse("p:claude r:api s:asking refund").Matches(_sample));
        Assert.False(AgentFilterQuery.Parse("p:claude r:api s:running refund").Matches(_sample));
        Assert.False(AgentFilterQuery.Parse("p:claude r:api s:asking nothing-like-it").Matches(_sample));
    }

    [Fact]
    public void Two_tokens_of_one_kind_are_both_needed()
    {
        Assert.True(AgentFilterQuery.Parse("r:ap r:pi").Matches(_sample));
        Assert.False(AgentFilterQuery.Parse("r:api r:web").Matches(_sample));
    }

    [Fact]
    public void An_unknown_token_has_to_appear_in_the_text_as_a_word() =>
        Assert.False(AgentFilterQuery.Parse("x:foo").Matches(_sample));
}
