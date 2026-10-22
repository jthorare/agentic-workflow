using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>The Agents page's keys (collector spec CU-1, Q6): J / K, Enter, O, Y and E.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentsPageKeysTests
{
    private readonly AgentsPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task J_and_K_walk_the_rows_across_groups_and_stop_at_the_ends()
    {
        var page = await _f.Shown(Token);
        var refs = page.Groups.SelectMany(g => g.Rows).Select(r => r.Ref).ToList();

        Assert.True(page.Press("J"));
        Assert.Equal(refs[0], page.SelectedKey);
        _ = page.Press("J");
        _ = page.Press("J");
        _ = page.Press("J");
        Assert.Equal(refs[^1], page.SelectedKey);
        _ = page.Press("K");
        Assert.Equal(refs[^2], page.SelectedKey);
    }

    [Fact]
    public async Task K_from_no_row_goes_to_the_last()
    {
        var page = await _f.Shown(Token);

        _ = page.Press("K");

        Assert.Equal(page.Groups[^1].Rows[^1].Ref, page.SelectedKey);
    }

    [Fact]
    public async Task Enter_asks_to_open_the_selected_sessions_sheet()
    {
        var page = await _f.Shown(Token);
        SessionOpenRequestedEventArgs? opened = null;
        page.OpenRequested += (_, e) => opened = e;
        _ = page.Press("J");

        Assert.True(page.Press("Enter"));

        Assert.Equal(new DeepLink(DeepLinkKind.Session, page.SelectedKey), opened!.Link);
    }

    [Fact]
    public async Task Enter_with_no_selection_does_nothing()
    {
        var page = await _f.Shown(Token);

        Assert.False(page.Press("Enter"));
        Assert.False(page.Press("X"));
    }

    [Fact]
    public async Task O_moves_to_focus_and_Y_copies_its_cli()
    {
        var page = await _f.Shown(Token);
        AgentFocusEventArgs? focus = null;
        page.FocusRequested += (_, e) => focus = e;
        _ = page.Press("J");   // the launched row comes first and O opens its run
        _ = page.Press("J");

        Assert.True(page.Press("O"));
        Assert.True(focus!.ToFocusLine);
        Assert.Equal(page.SelectedKey, focus.Key);
        Assert.True(page.Press("Y"));

        Assert.Equal("sindri agents focus " + page.SelectedKey, _f.Clipboard.Text);
    }

    [Fact]
    public async Task E_opens_the_ended_fold_and_Y_copies_the_selected_ended_row()
    {
        _f.Serve("recently-ended");
        var page = await _f.Shown(Token);

        Assert.True(page.Press("E"));
        Assert.True(page.IsEndedExpanded);
        _ = page.Press("K");
        Assert.Equal(page.Ended[0].Key, page.SelectedKey);
        Assert.True(page.Press("Y"));
        Assert.Equal(page.Ended[0].ResumeCommand, _f.Clipboard.Text);
        Assert.False(page.Press("O"));
        Assert.True(page.Press("E"));
        Assert.False(page.IsEndedExpanded);
    }

    [Fact]
    public async Task E_does_nothing_when_nothing_ended()
    {
        var page = await _f.Shown(Token);

        Assert.False(page.Press("E"));
    }
}
