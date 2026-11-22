using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Agents page polish (#3710): Copy attach command in the sheet, no leftover Session tab, no dead band under the page head, one way back at 1000 wide.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class AgentsPolishFramesTests : FrameHost
{
    private const string Claude = "claude-code/aaaaaaaa-0000-4000-8000-000000000001";
    private const string Codex = "codex/bbbbbbbb-0000-4000-8000-000000000002";
    private static readonly string[] _waysBack = ["SheetBack", "SheetClose"];

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Opened(string reference, double width = 1600)
    {
        var (window, shell) = await Connected(width: width, height: 1000);
        shell.GoTo(PageId.Agents);
        Settle(window);
        shell.Agents.RequestOpen(new SessionOpenRequestedEventArgs(new DeepLink(DeepLinkKind.Session, reference)));
        await Until(window, () => shell.IsSheetShown && shell.AgentSheet.Phase == FramePhase.Shown, "the sheet never showed the session");
        return (window, shell);
    }

    private static bool Visible(Window window, string name) =>
        window.GetVisualDescendants().OfType<Control>().Any(c => string.Equals(c.Name, name, StringComparison.Ordinal) && c.IsEffectivelyVisible);

    [Fact]
    public Task A_tmux_session_shows_Copy_attach_command_and_a_session_outside_tmux_does_not_3710() => OnUi(async () =>
    {
        var (tmux, _) = await Opened(Claude);
        var (other, _) = await Opened(Codex);

        Assert.Equal((true, false), (Visible(tmux, "SessionAttachCopy"), Visible(other, "SessionAttachCopy")));
    });

    [Fact]
    public Task No_Session_tab_stays_at_the_edge_after_the_sheet_closes_3710() => OnUi(async () =>
    {
        var (window, shell) = await Opened(Claude);

        shell.CloseSheet();
        Settle(window);

        Assert.False(Visible(window, "EdgeTab"));
    });

    [Fact]
    public Task With_no_problems_or_stale_line_the_table_sits_one_page_gap_under_the_head_3710() => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "fixture-three-providers");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Groups.Count > 0);
        Settle(window);

        var head = Named<Control>(window, "AgentsHeadRegion").Parent as Control ?? throw new InvalidOperationException("no head row");
        var headBottom = head.TranslatePoint(new Point(0, head.Bounds.Height), window)!.Value.Y;   // why: in the window
        var panelTop = Named<Control>(window, "AgentsPanel").TranslatePoint(default, window)!.Value.Y;
        var gap = Assert.IsType<double>(Application.Current!.FindResource("Size.PanelGap"));
        Assert.InRange(panelTop - headBottom, 0, gap + 1);
    });

    [Fact]
    public Task At_1000_wide_the_sheet_has_one_way_back_and_focus_is_not_on_the_title_3710() => OnUi(async () =>
    {
        var (window, _) = await Opened(Claude, width: 1000);

        Assert.Equal(1, _waysBack.Count(n => Visible(window, n)));
        var focused = window.FocusManager?.GetFocusedElement() as Control;
        var chain = new List<Control>();
        for (var at = focused; at is not null; at = at.GetVisualParent() as Control)
        {
            chain.Add(at);
        }

        Assert.DoesNotContain(chain, c => string.Equals(c.Name, "SheetTitle", StringComparison.Ordinal));
        Assert.Contains(chain, c => string.Equals(c.Name, "Sheet", StringComparison.Ordinal));
    });

    // #3783: narrowing below 1100 px with focus on the page makes the sheet cover the page; focus leaves the disabled page for the
    // session sheet's body (Neighbour's covering fallback), never its title, which draws a focus underline.
    [Fact]
    public Task Narrowing_to_cover_moves_focus_from_the_page_to_the_session_sheets_body_3783() => OnUi(async () =>
    {
        var (window, _) = await Opened(Claude, width: 1400);
        Assert.True(Named<Border>(window, "Page").Focus(NavigationMethod.Tab));
        Settle(window);

        window.Width = 1000;
        Settle(window);

        Assert.Contains("covers", Named<Border>(window, "Sheet").Classes, StringComparer.Ordinal);
        Assert.Equal("Sheet", (window.FocusManager?.GetFocusedElement() as Control)?.Name);
    });

    // #3783: beside the page, with the selected row's item still the sheet's last item, Enter on the row moves into the sheet. A session
    // sheet has no first action, so focus falls back (EnterIntoSheet): to the session sheet's body, never its title.
    [Fact]
    public Task Enter_into_a_session_sheet_with_no_first_action_lands_on_its_body_3783() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Queue.Total > 0 && s.Queue.Groups.Count > 0, width: 1400, height: 1000);
        shell.GoTo(PageId.Work);
        Settle(window);
        for (var i = 0; i < 12 && shell.Queue.SelectedId != A.NewItem; i++)
        {
            window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        }

        Assert.Equal(A.NewItem, shell.Queue.SelectedId);   // J never reached the fixture's new item: its order changed
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Until(window, () => shell.IsSheetShown && shell.Item.HasItem, "the item sheet never showed");
        shell.OpenSheet(new DeepLink(DeepLinkKind.Session, Claude));
        await Until(window, () => shell.IsSessionSheet && shell.AgentSheet.Phase == FramePhase.Shown, "the sheet never showed the session");
        var row = window.GetVisualDescendants().OfType<Border>()
            .First(b => string.Equals(b.Name, "RowFrame", StringComparison.Ordinal) && b is { IsEffectivelyVisible: true, DataContext: ItemRowViewModel r } && string.Equals(r.Id, shell.Queue.SelectedId, StringComparison.Ordinal));
        Assert.True(row.Focus(NavigationMethod.Tab));
        Settle(window);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(window);

        Assert.Equal("Sheet", (window.FocusManager?.GetFocusedElement() as Control)?.Name);
    });

    // #3839: the sheet stays open across pages, so Work -> Enter on a row (its item sheet) -> G A -> Enter on a session row (the session
    // sheet) -> G W leaves the session sheet beside Work with the row still selected. Enter on that row reopens its item, never moves
    // into the session sheet: the row's sheet is open only while the sheet shows that row's item.
    [Fact]
    public Task Enter_on_the_Work_row_after_a_session_sheet_opened_on_Agents_reopens_its_item_3839() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Queue.Total > 0 && s.Queue.Groups.Count > 0, width: 1400, height: 1000);
        shell.GoTo(PageId.Work);
        Settle(window);
        for (var i = 0; i < 12 && shell.Queue.SelectedId != A.NewItem; i++)
        {
            window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        }

        Assert.Equal(A.NewItem, shell.Queue.SelectedId);   // J never reached the fixture's new item: its order changed
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Until(window, () => shell.IsSheetShown && shell.Item.HasItem, "the item sheet never showed");
        shell.GoTo(PageId.Agents);
        Settle(window);
        shell.Agents.RequestOpen(new SessionOpenRequestedEventArgs(new DeepLink(DeepLinkKind.Session, Claude)));   // what Enter on a session row raises
        await Until(window, () => shell.IsSessionSheet && shell.AgentSheet.Phase == FramePhase.Shown, "the sheet never showed the session");
        shell.GoTo(PageId.Work);
        Settle(window);
        var row = window.GetVisualDescendants().OfType<Border>()
            .First(b => string.Equals(b.Name, "RowFrame", StringComparison.Ordinal) && b is { IsEffectivelyVisible: true, DataContext: ItemRowViewModel r } && string.Equals(r.Id, A.NewItem, StringComparison.Ordinal));
        Assert.True(row.Focus(NavigationMethod.Tab));
        Settle(window);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(window);

        Assert.Equal((true, A.NewItem), (shell.IsItemSheet, shell.Item.ItemId));
    });
}
