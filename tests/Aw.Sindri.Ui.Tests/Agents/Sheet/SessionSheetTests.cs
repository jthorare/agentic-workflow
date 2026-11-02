using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Agents.Sheet;

/// <summary>The session sheet on screen (U11a T4): its name and fields as assistive tech hears them, Esc and the edge tab, the pending Focus and the collapsed preview host. Frames: u11a-sheet-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SessionSheetTests : FrameHost
{
    private const string Claude = "claude-code/aaaaaaaa-0000-4000-8000-000000000001";
    private const string Codex = "codex/bbbbbbbb-0000-4000-8000-000000000002";

    private static void Frame(Window window, string name) => Frame(window, name, "u11a-sheet-");

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private static DeepLink Link(string reference) => new(DeepLinkKind.Session, reference);

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Opened(string reference, double width = 1600, bool details = true)
    {
        var (window, shell) = await Connected(width: width, height: 1000);
        shell.GoTo(PageId.Agents);
        Settle(window);
        shell.Agents.RequestOpen(new SessionOpenRequestedEventArgs(Link(reference)));
        await Until(window, () => shell.IsSheetShown && shell.AgentSheet.Phase is FramePhase.Shown or FramePhase.NotFound, "the sheet never showed its answer");
        if (details)
        {
            OpenDetails(window);
        }

        return (window, shell);
    }

    // The technical fields, the history and Watch the agent sit under a closed Details disclosure (F3).
    private static void OpenDetails(Window window)
    {
        Named<Control>(window, "SessionSheet").GetVisualDescendants().OfType<Expander>().Single(e => e.Name == "SessionDetails").IsExpanded = true;
        Settle(window);
    }

    // The Agents rows have their own controls with some of the sheet's names: look inside the sheet only.
    private static T InSheet<T>(Window window, string name) where T : Control =>
        Named<Control>(window, "SessionSheet").GetVisualDescendants().OfType<T>().First(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    private static List<(string Name, AutomationControlType Type)> Tree(Window window) => Heard.Tree(Named<Control>(window, "SessionSheet"));

    [Fact]
    public Task A_session_link_opens_the_sheet_and_every_field_is_heard_through_the_peer_tree() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1600, height: 1000);

        shell.Navigate(Link(Claude));   // the deep link: the Agents page and the sheet
        await Until(window, () => shell.AgentSheet.Phase == FramePhase.Shown);
        OpenDetails(window);
        var sheet = shell.AgentSheet;
        Assert.Equal((PageId.Agents, "Session " + sheet.Heading), (shell.CurrentPage, shell.SheetName));
        Assert.Equal(shell.SheetName, Heard.Name(Named<Control>(window, "Sheet")));
        Assert.False(Visible(window, "SheetBody"), "the placeholder is gone");
        Assert.Equal(sheet.Heading, Heard.Wrapper(Named<Border>(window, "SessionHeading")));
        var heard = Tree(window);
        var expected = new[] { sheet.Heading, sheet.StateLine }
            .Concat(sheet.Fields.Select(f => f.Name)).Concat(sheet.HostFields.Select(f => f.Name)).Concat(sheet.History.Select(h => h.Name));
        Assert.All(expected, name => Assert.Contains((name, AutomationControlType.Text), heard));
        Assert.All(heard.Where(h => h.Type == AutomationControlType.Text), h => Assert.False(string.IsNullOrWhiteSpace(h.Name), "a nameless text node in the sheet"));
        Assert.DoesNotContain(heard, h => h.Name.StartsWith("Watch the agent", StringComparison.Ordinal));

        // the CO-7c host: in the tree, collapsed and empty
        var host = Named<ContentControl>(window, "LivePreviewHost");
        Assert.False(host.IsVisible);
        Assert.Null(host.Content);
        Frame(window, "claude-tmux");

        // Esc closes it to the edge tab, which reopens the same session
        Press(window, Key.Escape);
        await Until(window, () => !shell.IsSheetOpen);
        Assert.Equal("Reopen session " + shell.AgentSheet.Heading, Heard.Name(Named<Button>(window, "EdgeTab")));
        Named<Button>(window, "EdgeTab").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.IsSheetShown && Visible(window, "SessionSheet"));
        Assert.Equal(Claude, shell.AgentSheet.Ref);
    });

    [Fact]
    public Task Focus_is_not_shown_in_the_sheet_until_it_can_send() => OnUi(async () =>
    {
        var (window, _) = await Opened(Codex);

        Assert.False(Visible(window, "SessionFocusRow"));
        Frame(window, "codex");
    });

    // F3: the sheet leads with the session's heading and state; the fields, the history and Watch the agent wait behind Details.
    [Fact]
    public Task The_technical_fields_wait_behind_a_closed_Details_disclosure() => OnUi(async () =>
    {
        var (window, shell) = await Opened(Claude, details: false);

        var details = InSheet<Expander>(window, "SessionDetails");
        Assert.False(details.IsExpanded);
        Assert.Equal("Details", details.Header);
        Assert.True(InSheet<Border>(window, "SessionHeading").IsEffectivelyVisible);
        Assert.Equal(shell.AgentSheet.StateLine, Heard.Wrapper(InSheet<Border>(window, "SessionState")));
        Assert.DoesNotContain(Named<Control>(window, "SessionSheet").GetVisualDescendants().OfType<ItemsControl>(), c => string.Equals(c.Name, "SessionFields", StringComparison.Ordinal) && c.IsEffectivelyVisible);   // a closed Expander builds no content
        OpenDetails(window);
        Assert.True(details.IsExpanded);
        Assert.Contains(InSheet<ItemsControl>(window, "SessionFields").GetVisualDescendants().OfType<Border>(), b => b.IsEffectivelyVisible);
    });

    [Fact]
    public Task A_404_says_the_session_has_ended_and_the_sheet_stays_open() => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Problem("sessions-id", "not-found");
        var (window, shell) = await Opened(Claude);

        var ended = InSheet<Border>(window, "SessionEnded");
        Assert.Equal("This session has ended.", Heard.Wrapper(ended));
        Assert.Equal(shell.AgentSheet.EndedDetail, ToolTip.GetTip(ended));
        Assert.True(shell.IsSheetShown);
        Assert.False(InSheet<StackPanel>(window, "SessionBody").IsVisible);   // an ended session has no Details to open
        Assert.False(shell.AgentSheet.IsFocusEnabled);
        Frame(window, "ended");
    });

    [Fact]
    public Task Esc_returns_focus_to_the_opening_row() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1000, height: 1000);   // narrow: the sheet covers the page and takes focus
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Drawn > 0, "the Agents page never read the list");
        var row = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "SessionRowFrame" && b.IsEffectivelyVisible && b.DataContext is SessionRowViewModel r && string.Equals(r.Ref, Claude, StringComparison.Ordinal));
        Assert.True(row.Focus(NavigationMethod.Tab));

        shell.Agents.RequestOpen(new SessionOpenRequestedEventArgs(Link(Claude)));
        await Until(window, () => shell.IsSheetShown && Named<Border>(window, "SheetTitle").IsFocused, "focus never reached the sheet's heading");
        Press(window, Key.Escape);

        await Until(window, () => !shell.IsSheetOpen && ReferenceEquals(window.FocusManager?.GetFocusedElement(), row), $"focus is on {window.FocusManager?.GetFocusedElement()}");
    });

    [Fact]
    public Task The_sheet_is_named_session_provider_and_title() => OnUi(async () =>
    {
        var (_, shell) = await Opened(Claude);

        Assert.Equal("Session " + shell.AgentSheet.Heading, shell.SheetName);
        Assert.StartsWith("Session ", shell.SheetName, StringComparison.Ordinal);
        Assert.DoesNotContain(Claude, shell.SheetName, StringComparison.Ordinal);
        Assert.Equal("Reopen session " + shell.AgentSheet.Heading, shell.EdgeTabName);
    });
}
