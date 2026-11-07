using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The page's header and summary strip (review page spec §6.2, §6.3, §16; mockup review-a-1440).</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewPageHeaderTests
{
    private readonly ReviewPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_header_has_title_ref_author_head_branch_settings_and_the_issue_chips()
    {
        var page = await _f.Opened(Token);

        Assert.Equal(
            ("Add retry budget to the sync worker", "acme/api#2071", "sam-dev", "head a1b2c3d", "feat/retry-budget", "Settings: sindri", false),
            (page.Title, page.Ref, page.Author, page.HeadText, page.Branch, page.SettingsText, page.IsDraft));
        Assert.Equal(["ENG-12"], page.IssueKeys, StringComparer.Ordinal);
        Assert.Equal("https://github.com/acme/api/pull/2071", page.PrUrl);
    }

    [Fact]
    public async Task A_draft_PR_says_so_and_a_page_with_no_settings_has_no_settings_chip()
    {
        var page = await _f.Opened("full", json =>
        {
            json["pr"]!["draft"] = true;
            json["settings"] = null;
        }, Token);

        Assert.Equal((true, string.Empty), (page.IsDraft, page.SettingsText));
    }

    [Fact]
    public async Task The_strip_shows_the_served_parts_in_order_each_naming_its_panel()
    {
        var page = await _f.Opened(Token);

        Assert.Equal(
            [("requiredChecks", "4 open"), ("docMap", "Covers Ph5, REQ-E3"), ("risk", "1 risky file"), ("ci", "1 failing")],
            page.Strip.Select(s => (s.Panel, s.Text)));
        Assert.All(page.Strip, s => Assert.False(s.IsLoading));
    }

    [Fact]
    public async Task A_strip_part_reads_its_panels_title_and_its_words_and_never_a_bare_glyph()
    {
        var page = await _f.Opened(Token);

        Assert.Equal("Required checks: 4 open", page.Strip[0].AutomationName);
        Assert.All(page.Strip, s => Assert.NotEqual(StateGlyph.None, s.Glyph));
    }

    [Fact]
    public async Task With_no_doc_named_and_no_checks_the_strip_says_so()
    {
        _f.Serve("empty-panels");

        var page = await _f.Opened(Token);

        Assert.Equal([("requiredChecks", "none open"), ("docMap", "No doc named")], page.Strip.Select(s => (s.Panel, s.Text)));
    }

    [Fact]
    public async Task When_checks_cant_be_decided_the_strip_says_so_and_has_no_other_part()
    {
        _f.Serve("panel-errors");

        var page = await _f.Opened(Token);

        Assert.Equal([("requiredChecks", "Couldn't decide checks")], page.Strip.Select(s => (s.Panel, s.Text)));
    }

    [Fact]
    public async Task Pressing_a_strip_part_opens_its_panel_and_sends_focus_there()
    {
        var page = await _f.Opened(Token, wide: false);
        Assert.True(page.Panels.Single(p => string.Equals(p.Id, "risk", StringComparison.Ordinal)).IsCollapsed);

        page.Jump("risk");

        Assert.False(page.Panels.Single(p => string.Equals(p.Id, "risk", StringComparison.Ordinal)).IsCollapsed);
        Assert.Equal(new ReviewFocus(ReviewFocusRegion.Panel, "risk"), page.Focus);
    }
}
