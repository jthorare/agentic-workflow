using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;
using static Aw.Sindri.Ui.Tests.Agents.Page.AgentsPageFixture;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>The narrow list column (#4162): with the live pane open a row drops the provider word and "estimated" to give the width to the title; the tooltip and the accessible name still carry both.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentsNarrowRowTests
{
    private const string Claude = "claude-code/";

    private readonly AgentsPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SessionRowViewModel ClaudeRow(AgentsPageViewModel page) =>
        page.Groups.SelectMany(g => g.Rows).Single(r => r.Ref.StartsWith(Claude, StringComparison.Ordinal));

    private void ServeEstimated() => _f.Serve(Edited(l =>
    {
        Row(l, Claude)["stateSource"] = "transcript";
        Row(l, Claude)["estimated"] = true;
    }));

    [Fact]
    public async Task In_the_narrow_column_the_row_hides_the_provider_and_estimated_words()
    {
        ServeEstimated();
        var page = await _f.Shown(Token);
        var row = ClaudeRow(page);

        page.Resize(360);

        Assert.True(row.IsEstimated);
        Assert.Equal((false, false), (row.ShowsProvider, row.ShowsEstimated));
    }

    [Fact]
    public async Task In_the_narrow_column_the_tooltip_still_holds_the_provider_and_estimated_and_the_name_is_unchanged()
    {
        ServeEstimated();
        var page = await _f.Shown(Token);
        page.Resize(1280);
        var row = ClaudeRow(page);
        var name = row.Name;

        page.Resize(360);

        Assert.Equal(name, row.Name);
        Assert.Contains(row.Provider, row.Tip, StringComparison.Ordinal);
        Assert.Contains("estimated", row.Tip, StringComparison.Ordinal);
        Assert.Contains(row.TitleText, row.Tip, StringComparison.Ordinal);
    }

    [Fact]
    public async Task In_the_wide_layout_the_row_shows_the_provider_and_estimated_words()
    {
        ServeEstimated();
        var page = await _f.Shown(Token);

        page.Resize(1280);

        Assert.Equal((true, true), (ClaudeRow(page).ShowsProvider, ClaudeRow(page).ShowsEstimated));
    }

    [Fact]
    public async Task A_row_that_is_not_estimated_never_shows_estimated()
    {
        var page = await _f.Shown(Token);

        page.Resize(1280);

        Assert.False(ClaudeRow(page).ShowsEstimated);
    }

    [Fact]
    public async Task A_row_that_arrives_after_the_page_narrowed_hides_the_words_too()
    {
        var page = await _f.Shown(Token);
        page.Resize(360);
        ServeEstimated();

        await page.Refresh(Url, Token);

        Assert.Equal((false, false), (ClaudeRow(page).ShowsProvider, ClaudeRow(page).ShowsEstimated));
    }

    [Fact]
    public async Task An_estimated_row_in_the_narrow_column_draws_a_hollow_dot_and_a_wide_one_the_full_dot()
    {
        ServeEstimated();
        var page = await _f.Shown(Token);
        var row = ClaudeRow(page);

        page.Resize(1280);
        var wide = row.DotGlyph;
        page.Resize(360);

        Assert.Equal(("●", "○"), (wide, row.DotGlyph));
        Assert.Equal("●", row.Glyph);   // the chip's own glyph (and its accessible name) stay
    }

    [Fact]
    public async Task A_row_that_is_not_estimated_keeps_the_full_dot_in_the_narrow_column()
    {
        var page = await _f.Shown(Token);

        page.Resize(360);

        Assert.Equal("●", ClaudeRow(page).DotGlyph);
    }

    [Fact]
    public async Task In_the_narrow_column_a_lane_row_is_named_by_its_lane_and_the_title_stays_in_the_tooltip()
    {
        _f.Serve(Edited(l => Row(l, Claude)["lane"] = new System.Text.Json.Nodes.JsonObject { ["team"] = "sindri", ["lane"] = "ideas" }));
        var page = await _f.Shown(Token);
        var row = ClaudeRow(page);

        page.Resize(1280);
        var wide = row.ListText;
        page.Resize(360);

        Assert.Equal((row.TitleText, "ideas"), (wide, row.ListText));
        Assert.Contains(row.TitleText, row.Tip, StringComparison.Ordinal);
    }

    [Fact]
    public async Task In_the_narrow_column_a_row_without_a_lane_is_named_by_its_tmux_session()
    {
        _f.Serve(Edited(l =>
        {
            Row(l, Claude)["lane"] = null;
            Row(l, Claude)["host"]!["session"] = "ideas-tmux";
        }));
        var page = await _f.Shown(Token);

        page.Resize(360);

        Assert.Equal("ideas-tmux", ClaudeRow(page).ListText);
    }

    [Fact]
    public async Task In_the_narrow_column_a_row_with_neither_lane_nor_tmux_name_shows_its_title()
    {
        _f.Serve(Edited(l =>
        {
            Row(l, Claude)["lane"] = null;
            Row(l, Claude)["host"]!["session"] = null;
        }));
        var page = await _f.Shown(Token);

        page.Resize(360);

        Assert.Equal(ClaudeRow(page).TitleText, ClaudeRow(page).ListText);
    }
}
