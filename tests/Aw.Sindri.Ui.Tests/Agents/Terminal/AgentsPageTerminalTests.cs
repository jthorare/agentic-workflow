using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Agents.Page;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>The Agents page hosts the terminal view (agents spec §15.3, U7a): Enter opens it with focus in the list, Esc or Close closes it, j/k and a click switch it.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentsPageTerminalTests
{
    private readonly AgentsPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Enter_opens_the_terminal_view_with_focus_in_the_list_and_not_the_sheet()
    {
        var page = await _f.Shown(Token);
        var sheets = new List<SessionOpenRequestedEventArgs>();
        page.OpenRequested += (_, e) => sheets.Add(e);
        _ = page.Press("J");

        var used = page.Press("Enter");

        Assert.True(used);
        Assert.True(page.IsTerminalOpen);
        Assert.Equal(page.SelectedKey, page.TerminalKey);
        Assert.Equal((page.SelectedKey, TerminalSide.List), (page.Terminal.Target?.Key, page.Terminal.Side));
        Assert.Empty(sheets);
    }

    [Fact]
    public async Task Esc_with_focus_in_the_list_closes_the_view_and_keeps_the_cursor_on_the_row()
    {
        var page = await _f.Shown(Token);
        _ = page.Press("J");
        var key = page.SelectedKey;
        _ = page.Press("Enter");

        var used = page.Press("Esc");

        Assert.True(used);
        Assert.False(page.IsTerminalOpen);
        Assert.Null(page.TerminalKey);
        Assert.Equal(key, page.SelectedKey);
    }

    [Fact]
    public async Task The_header_Close_button_closes_the_view()
    {
        var page = await _f.Shown(Token);
        _ = page.Press("J");
        _ = page.Press("Enter");

        page.Terminal.Close();

        Assert.False(page.IsTerminalOpen);
    }

    [Fact]
    public async Task Esc_does_nothing_when_the_view_is_closed()
    {
        var page = await _f.Shown(Token);
        _ = page.Press("J");

        Assert.False(page.Press("Esc"));
    }

    [Fact]
    public async Task J_while_open_switches_the_pane_and_focus_stays_in_the_list()
    {
        var page = await _f.Shown(Token);
        _ = page.Press("J");
        _ = page.Press("Enter");

        _ = page.Press("J");

        Assert.Equal(page.SelectedKey, page.TerminalKey);
        Assert.Equal((page.SelectedKey, TerminalSide.List), (page.Terminal.Target?.Key, page.Terminal.Side));
    }

    [Fact]
    public async Task A_click_on_another_row_while_open_switches_the_pane()
    {
        var page = await _f.Shown(Token);
        var refs = page.Groups.SelectMany(g => g.Rows).Select(r => r.Ref).ToList();
        _ = page.Press("J");
        _ = page.Press("Enter");

        _ = page.Select(refs[^1]);

        Assert.Equal(refs[^1], page.Terminal.Target?.Key);
    }

    [Fact]
    public async Task Selecting_a_row_while_closed_opens_nothing()
    {
        var page = await _f.Shown(Token);
        var refs = page.Groups.SelectMany(g => g.Rows).Select(r => r.Ref).ToList();

        _ = page.Select(refs[0]);

        Assert.False(page.IsTerminalOpen);
    }

    [Fact]
    public async Task Transcript_opens_the_sheet_on_the_row_the_pane_shows()
    {
        var page = await _f.Shown(Token);
        SessionOpenRequestedEventArgs? opened = null;
        page.OpenRequested += (_, e) => opened = e;
        _ = page.Press("J");
        _ = page.Press("Enter");

        page.OpenTranscript();

        Assert.Equal(new DeepLink(DeepLinkKind.Session, page.TerminalKey), opened?.Link);
    }

    [Fact]
    public async Task Opening_by_key_puts_focus_in_the_pane_for_a_row_that_is_drawn_and_refuses_one_that_is_not()
    {
        var page = await _f.Shown(Token);
        var key = page.Groups.SelectMany(g => g.Rows).Select(r => r.Ref).First();

        var opened = page.OpenTerminalByKey(key);
        var ghost = page.OpenTerminalByKey("sindri-nothere");

        Assert.True(opened);
        Assert.False(ghost);
        Assert.Equal((key, TerminalSide.Pane, key), (page.TerminalKey, page.Terminal.Side, page.SelectedKey));
    }
}
