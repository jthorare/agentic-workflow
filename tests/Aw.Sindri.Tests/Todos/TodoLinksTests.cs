using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos;

/// <summary>A todo's links (todo spec §4.3): three kinds now (items, PRs, Linear issues), stored as deep links, shape checked only.</summary>
public sealed class TodoLinksTests
{
    private static readonly string[] _hosts = ["github.com"];

    [Theory, Trait("Lane", "unit")]
    [InlineData("ITM-0000000000000000000000000000000f", "sindri://item/ITM-0000000000000000000000000000000f")]
    [InlineData("acme/api#12", "sindri://reviews/acme/api/12")]
    [InlineData("sam-dev/web.app#7", "sindri://reviews/sam-dev/web.app/7")]
    [InlineData("https://github.com/acme/api/pull/12", "sindri://reviews/acme/api/12")]
    [InlineData("ENG-12", "sindri://linear/ENG-12")]
    public void Each_kind_becomes_its_deep_link(string typed, string expected)
    {
        var link = TodoLinks.Parse(typed, _hosts);

        Assert.True(link.IsSuccess, link.IsError ? link.Error.ToString() : null);
        Assert.Equal(expected, link.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("../api#12")]   // never ..
    [InlineData("acme/api#")]
    [InlineData("https://example.com/acme/api/pull/12")]   // not a configured forge host
    [InlineData("https://sam@github.com/acme/api/pull/12")]   // a user name is refused
    [InlineData("https://github.com.evil.test/acme/api/pull/12")]   // the host is compared exactly
    [InlineData("scoping/acme-api/fix-login")]   // the old workflow instance address isn't a link kind (workflow runs come later)
    public void A_link_of_no_known_shape_is_SND_TODO_004(string typed)
    {
        var link = TodoLinks.Parse(typed, _hosts);

        Assert.True(link.IsError);
        Assert.Equal("SND-TODO-004", link.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_list_drops_duplicates_keeps_order_and_takes_at_most_10()
    {
        var kept = TodoLinks.ParseAll(["ENG-12", "acme/api#12", "ENG-12"], _hosts);

        Assert.True(kept.IsSuccess, kept.IsError ? kept.Error.ToString() : null);
        Assert.Equal(["sindri://linear/ENG-12", "sindri://reviews/acme/api/12"], kept.Value);

        var ten = TodoLinks.ParseAll([.. Enumerable.Range(1, TodoLinks.Max).Select(n => $"ENG-{TodoHarness.Num(n)}")], _hosts);
        var eleven = TodoLinks.ParseAll([.. Enumerable.Range(1, TodoLinks.Max + 1).Select(n => $"ENG-{TodoHarness.Num(n)}")], _hosts);

        Assert.True(ten.IsSuccess);
        Assert.True(eleven.IsError);
        Assert.Equal("SND-TODO-004", eleven.Error.Code);
    }
}
