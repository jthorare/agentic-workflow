using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Agents;
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
        var headBottom = head.TranslatePoint(new Avalonia.Point(0, head.Bounds.Height), window)!.Value.Y;   // why: in the window
        var panelTop = Named<Control>(window, "AgentsPanel").TranslatePoint(default, window)!.Value.Y;
        var gap = Assert.IsType<double>(Avalonia.Application.Current!.FindResource("Size.PanelGap"));
        Assert.InRange(panelTop - headBottom, 0, gap + 1);
    });

    [Fact]
    public Task At_1000_wide_the_sheet_has_one_way_back_and_focus_is_not_on_the_title_3710() => OnUi(async () =>
    {
        var (window, _) = await Opened(Claude, width: 1000);

        Assert.Equal(1, new[] { "SheetBack", "SheetClose" }.Count(n => Visible(window, n)));
        var focused = window.FocusManager?.GetFocusedElement() as Control;
        var chain = new List<Control>();
        for (Control? at = focused; at is not null; at = at.GetVisualParent() as Control)
        {
            chain.Add(at);
        }

        Assert.DoesNotContain(chain, c => string.Equals(c.Name, "SheetTitle", StringComparison.Ordinal));
    });
}
