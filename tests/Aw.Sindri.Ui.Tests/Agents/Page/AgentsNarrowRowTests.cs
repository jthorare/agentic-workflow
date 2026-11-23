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
}
