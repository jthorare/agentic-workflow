using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;
using static Aw.Sindri.Ui.Tests.Agents.Page.AgentsPageFixture;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>The Agents table's cells (#2553): empty columns drop, a session doing nothing says "—" never "None", the state is one dot with the word beside it, and "estimated" says what is estimated and why.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentsColumnsTests
{
    private const string Claude = "claude-code/";

    private readonly AgentsPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SessionRowViewModel RowOf(AgentsPageViewModel page, string prefix) =>
        page.Groups.SelectMany(g => g.Rows).Single(r => r.Name.Contains(prefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) || r.Provider.StartsWith(prefix[..3], StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task Every_column_shows_while_some_row_has_a_value_for_it()
    {
        var page = await _f.Shown(Token);
        page.Resize(1280);

        Assert.Equal((true, true, true), (page.ShowsRepo, page.ShowsWhere, page.ShowsDoing));
    }

    [Fact]
    public async Task A_column_whose_every_value_is_empty_is_dropped()
    {
        _f.Serve(Edited(l =>
        {
            Remove(l, Claude);
            Remove(l, "codex/");
        }));   // what is left is the cursor session: no repository, no worktree, nothing it asks or runs
        var page = await _f.Shown(Token);
        page.Resize(1280);

        Assert.Equal((false, true, false), (page.ShowsRepo, page.ShowsWhere, page.ShowsDoing));
    }

    [Fact]
    public async Task Under_the_wide_width_every_optional_column_is_dropped()
    {
        var page = await _f.Shown(Token);

        page.Resize(500);

        Assert.Equal((false, false, false), (page.ShowsRepo, page.ShowsWhere, page.ShowsDoing));
    }

    [Fact]
    public async Task A_session_doing_nothing_says_a_dash_never_None()
    {
        var page = await _f.Shown(Token);

        var row = RowOf(page, Claude);

        Assert.Equal((true, "—"), (row.HasNoStep, row.NoStepText));
    }

    [Theory]
    [InlineData("running", "● Working")]
    [InlineData("asking", "● Waiting on you")]
    [InlineData("idle", "● Idle")]
    public async Task Every_state_is_the_same_dot_with_its_own_word(string state, string chip)
    {
        _f.Serve(Edited(l => Row(l, Claude)["state"] = state));
        var page = await _f.Shown(Token);

        Assert.Equal(chip, RowOf(page, Claude).ChipText);
    }

    [Fact]
    public async Task Estimated_has_a_tooltip_that_says_what_is_estimated_and_why()
    {
        _f.Serve(Edited(l =>
        {
            Row(l, Claude)["stateSource"] = "transcript";
            Row(l, Claude)["estimated"] = true;
        }));
        var page = await _f.Shown(Token);

        var row = RowOf(page, Claude);

        Assert.Equal("The agent reported no state, so Sindri guessed it from when the session's transcript was last written.", row.EstimatedTip);
    }

    [Fact]
    public async Task A_row_that_is_not_estimated_has_no_tooltip()
    {
        var page = await _f.Shown(Token);

        Assert.Equal(string.Empty, RowOf(page, Claude).EstimatedTip);
    }
}
