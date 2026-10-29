using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>One project card (mockup B): the served fields as words, the footer, the note's face and the accessible name.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectCardTests
{
    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_led_card_reads_in_words()
    {
        var card = (await _f.Shown(Token)).Groups[0].Cards[0];

        Assert.Equal(("linear:project/00000000-0000-4000-8000-000000000001", 0L, "Checkout rewrite", "In progress"), (card.Id, card.Version, card.Name, card.StateWord));
        Assert.Equal(("at risk", ChipTone.Attn), (card.HealthWord, card.HealthTone));
        Assert.Equal(("12", "open issues", null, null), (card.IssuesValue, card.IssuesLabel, card.PrsValue, card.NotesFace));
        Assert.Equal(["Lead: you", "Target Jan 31", "1d ago"], card.Footer);
        Assert.Equal(("Checkout rewrite, In progress, at risk, 12 open issues", false), (card.AccessibleName, card.IsGone));
    }

    [Theory]
    [InlineData("onTrack", "on track", "Ok")]
    [InlineData("atRisk", "at risk", "Attn")]
    [InlineData("offTrack", "off track", "Fail")]
    [InlineData(null, null, "Neutral")]
    public async Task Health_is_a_word_and_a_tone(string? served, string? word, string tone)
    {
        var card = await _f.FirstCard(c => c["health"] = served, Token);

        Assert.Equal((word, Enum.Parse<ChipTone>(tone)), (card.HealthWord, card.HealthTone));
    }

    [Fact]
    public async Task A_hand_made_card_without_a_lead_or_counts_has_only_what_it_knows()
    {
        var kitchen = (await _f.Shown(Token)).Groups[1].Cards[0];

        Assert.Equal(("by hand", null, null, null, null), (kitchen.OriginChip, kitchen.HealthWord, kitchen.IssuesValue, kitchen.IssuesLabel, kitchen.PrsValue));
        Assert.Equal(["Target Mar 1", "just now"], kitchen.Footer);
        Assert.Equal("Kitchen, Planned", kitchen.AccessibleName);
    }

    [Fact]
    public async Task An_attached_card_names_its_lead_and_drops_the_target_it_does_not_have()
    {
        var mobile = (await _f.Shown(Token)).Groups[2].Cards[0];

        Assert.Equal(["Lead: Sam Lee", "5d ago"], mobile.Footer);
        Assert.Equal("Mobile app, Paused, 7 open issues", mobile.AccessibleName);
    }

    [Fact]
    public async Task A_finished_card_says_when_it_was_done_and_leaves_the_target_off()
    {
        var list = ProjectsFixture.Golden("mixed");
        list["finished"]![0]!["target"] = "2026-01-31";
        _f.Serve(list);

        var done = (await _f.Shown(Token)).Finished.Cards[0];

        Assert.Equal(["Done Dec 29", "Lead: you", "3d ago"], done.Footer);
        Assert.Equal("Done", done.StateWord);
    }

    [Fact]
    public async Task A_canceled_card_says_when_it_was_canceled()
    {
        var list = ProjectsFixture.Golden("mixed");
        list["finished"]![0]!["state"] = "canceled";
        list["finished"]![0]!["finishedAt"] = "2025-10-18T09:00:00Z";
        _f.Serve(list);

        var card = (await _f.Shown(Token)).Finished.Cards[0];

        Assert.Equal(("Canceled", "Canceled Oct 18"), (card.StateWord, card.Footer[0]));
    }

    [Fact]
    public async Task A_counting_card_shows_an_ellipsis_and_the_word_counting()
    {
        _f.Serve("counting");

        var card = (await _f.Shown(Token)).Groups[0].Cards[0];

        Assert.Equal(("…", "counting"), (card.IssuesValue, card.IssuesLabel));
    }

    [Fact]
    public async Task Open_PRs_show_when_served_and_join_the_accessible_name_and_the_repos_the_footer()
    {
        var card = await _f.FirstCard(c =>
        {
            c["openPrs"] = 2;
            c["repos"] = new System.Text.Json.Nodes.JsonArray("acme/web", "acme/api");
        }, Token);

        Assert.Equal("2", card.PrsValue);
        Assert.Equal("Checkout rewrite, In progress, at risk, 12 open issues, 2 open PRs", card.AccessibleName);
        Assert.Equal(["Lead: you", "Target Jan 31", "acme/web, acme/api", "1d ago"], card.Footer);
    }

    [Fact]
    public async Task A_card_the_store_no_longer_holds_says_so()
    {
        var card = await _f.FirstCard(c => c["synced"] = false, Token);

        Assert.Equal((true, "no longer synced from Linear"), (card.IsGone, card.GoneText));
    }

    [Theory]
    [InlineData("Waiting on the vendor", "Waiting on the vendor")]
    [InlineData("First line\nSecond line", "First line")]
    [InlineData("First line\r\nSecond", "First line")]
    public async Task The_note_shows_its_first_line_only(string note, string face)
        => Assert.Equal(face, (await _f.FirstCard(c => c["note"] = note, Token)).NotesFace);

    [Fact]
    public async Task A_note_of_eighty_characters_is_kept_and_a_longer_one_is_cut_with_an_ellipsis()
    {
        var eighty = new string('a', 80);
        var longer = new string('b', 79) + "  tail";

        Assert.Equal(eighty, (await _f.FirstCard(c => c["note"] = eighty, Token)).NotesFace);
        Assert.Equal(new string('b', 79) + "…", (await _f.FirstCard(c => c["note"] = longer, Token)).NotesFace);
    }

    [Fact]
    public async Task The_cut_drops_the_space_it_lands_after()
    {
        var note = new string('c', 77) + "  " + "dddd";

        var face = (await _f.FirstCard(c => c["note"] = note, Token)).NotesFace;

        Assert.Equal(new string('c', 77) + "…", face);
    }

    [Fact]
    public async Task A_missing_or_blank_note_has_no_face()
    {
        Assert.Null((await _f.FirstCard(c => c["note"] = null, Token)).NotesFace);
        Assert.Null((await _f.FirstCard(c => c["note"] = "  ", Token)).NotesFace);
    }

    [Fact]
    public async Task A_card_is_kept_by_its_id_across_a_read_and_shows_the_newer_serving()
    {
        var page = await _f.Shown(Token);
        var card = page.Groups[0].Cards[0];
        var list = ProjectsFixture.Golden("mixed");
        list["projects"]![0]!["openIssues"] = 9;
        _f.Serve(list);

        await page.Refresh(ProjectsFixture.Url, Token);

        Assert.Same(card, page.Groups[0].Cards[0]);
        Assert.Equal("9", card.IssuesValue);
    }

    [Fact]
    public async Task A_card_has_counts_when_it_has_an_issue_count_or_a_PR_count_and_none_otherwise()
    {
        var page = await _f.Shown(Token);

        Assert.Equal([true, false, true], page.Groups.Select(g => g.Cards[0].HasCounts));
        Assert.False((await _f.FirstCard(c => c["openIssues"] = null, Token)).HasCounts);
        Assert.True((await _f.FirstCard(c => { c["openIssues"] = null; c["openPrs"] = 2; }, Token)).HasCounts);
    }
}
