using Aw.Kernel.Testing;
using static Aw.Sindri.Ui.Tests.Projects.ProjectsFixture;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Projects page's cards (#2805): a finished project has no health word, and a read of the same data leaves the kept cards untouched.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectsPolishTests
{
    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Finished_card_has_no_health_word()
    {
        var list = Golden("mixed");
        list["finished"]![0]!["health"] = "onTrack";
        _f.Serve(list);

        var page = await _f.Shown(Token);

        var card = Assert.Single(page.Finished.Cards);
        Assert.Equal((null, Aw.Sindri.Ui.Api.ChipTone.Neutral), (card.HealthWord, card.HealthTone));
    }

    // Guard: passes today and must keep passing; a live card keeps the health word the server gave it.
    [Fact]
    public async Task A_live_card_keeps_its_health_word()
    {
        var page = await _f.Shown(Token);

        Assert.Equal("at risk", page.Groups[0].Cards[0].HealthWord);
    }

    [Fact]
    public async Task The_same_served_cards_twice_leave_the_kept_card_untouched()
    {
        var page = await _f.Shown(Token);
        var card = page.Groups[0].Cards[0];
        var served = card.Card;
        var changes = new List<string?>();
        card.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        await page.Refresh(Url, Token);

        Assert.Same(card, page.Groups[0].Cards[0]);
        Assert.Empty(changes);
        Assert.Same(served, card.Card);
    }
}
