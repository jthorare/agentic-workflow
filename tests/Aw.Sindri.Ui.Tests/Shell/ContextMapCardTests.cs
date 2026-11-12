using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The Health page's Context map card (code index spec 6.4, F1P-c): the six states in words with a glyph, Sindri's sentences under them, and nothing while there is no map.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextMapCardTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer Served(string state, string word, string line, string? reason = null)
    {
        var reasonJson = reason is null ? "null" : "\"" + reason + "\"";
        var stale = state == "published-stale" ? "true" : "false";
        return new RouteAnswer(HttpStatusCode.OK,
            "{\"state\":\"" + state + "\",\"word\":\"" + word + "\",\"reason\":" + reasonJson + ",\"stale\":" + stale + ",\"staleRepos\":[],\"version\":14,\"until\":null,\"lines\":[\"" + line + "\"]}");
    }

    private static async Task<ContextMapCardViewModel> Shown(RouteAnswer answer)
    {
        var server = new FakeSindriApi { MapPublishAnswer = answer };
        var card = new ContextMapCardViewModel(server);
        await card.Refresh(_api, Token);
        return card;
    }

    [Fact]
    public async Task The_served_golden_shows_a_published_map_with_its_sentence()
    {
        var card = await Shown(RouteAnswer.Api("context-map-publish", "published"));

        Assert.True(card.IsVisible);
        Assert.Equal(("Published", StateGlyph.Ok, "Map v1 is current."), (card.State, card.Glyph, Assert.Single(card.Lines)));
        Assert.Equal("Context map: Published. Map v1 is current.", card.Name);
    }

    [Theory]
    [InlineData("held", "Held for the code index", "Stale")]
    [InlineData("overdue", "Overdue", "Profile")]
    [InlineData("published", "Published", "Ok")]
    [InlineData("published-without-waiting", "Published without waiting", "Ok")]
    [InlineData("published-stale", "Published, code answers stale", "Profile")]
    [InlineData("published-by-hand", "Published by hand", "Ok")]
    public async Task Each_of_the_six_states_has_its_own_words_and_a_glyph(string state, string words, string glyph)
    {
        var card = await Shown(Served(state, "served word", "A sentence from Sindri."));

        Assert.Equal((words, glyph, true), (card.State, card.Glyph.ToString(), card.IsVisible));
        Assert.Equal(["A sentence from Sindri."], card.Lines);
    }

    [Fact]
    public async Task A_state_this_build_does_not_know_shows_the_word_Sindri_sent()
    {
        var card = await Shown(Served("published-later", "published, later", "x"));

        Assert.Equal(("published, later", StateGlyph.Ok), (card.State, card.Glyph));
    }

    [Fact]
    public async Task No_map_yet_hides_the_card_and_a_later_map_shows_it_again()
    {
        var server = new FakeSindriApi { MapPublishAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-030", "no map yet") };
        var card = new ContextMapCardViewModel(server);

        await card.Refresh(_api, Token);
        var hidden = (card.IsVisible, card.State, card.Lines.Count, card.Name);
        server.MapPublishAnswer = Served("held", "held", "Map v14 is held; none of your repos are waiting.");
        await card.Refresh(_api, Token);

        Assert.Equal((false, string.Empty, 0, "Context map"), hidden);
        Assert.True(card.IsVisible);
        Assert.Equal("Context map: Held for the code index. Map v14 is held; none of your repos are waiting.", card.Name);
    }

    [Fact]
    public async Task The_health_page_reads_the_card_with_the_other_followed_views()
    {
        var server = new FakeSindriApi { MapPublishAnswer = Served("held", "held", "Map v14 is held for the code index until 12:15Z.") };
        var health = new HealthViewModel(new InMemoryEndpointRecords(), server, TimeProvider.System, new FakeClipboard());

        await health.RefreshViews(_api, Token);

        Assert.Equal((1, "Held for the code index"), (server.MapPublishCalls, health.ContextMap.State));
    }
}
