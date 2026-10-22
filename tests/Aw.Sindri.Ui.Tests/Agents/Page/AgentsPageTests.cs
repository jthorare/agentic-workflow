using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>The Agents page in the window (collector spec CU-1, U11a T2): rows named for assistive tech, lines as named text, a refresh that moves nothing, keys, and the key frames.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class AgentsPageTests : FrameHost
{
    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private static readonly HashSet<string> _focusNames = new(["SessionFocusLine", "SessionFocusButton", "SessionFocusCopy"], StringComparer.Ordinal);

    private static List<Border> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "SessionRowFrame" && b.IsEffectivelyVisible)];

    private async Task<(Window Window, ShellViewModel Shell)> OnAgents(Action? serve = null, double? height = null)
    {
        serve?.Invoke();
        var (window, shell) = await Connected(width: 1280, height: height);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Drawn > 0, "the Agents page never read the list");
        return (window, shell);
    }

    [Fact]
    public Task G_A_shows_the_Agents_page_with_a_row_per_session() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        Press(window, Key.G);
        Press(window, Key.A);
        await Until(window, () => shell.Agents.Drawn > 0, "the Agents page never read the list");

        Assert.Equal(PageId.Agents, shell.CurrentPage);
        Assert.Equal(("AgentsPage", true), (Named<Control>(window, "AgentsPage").GetType().Name, Visible(window, "AgentsPage")));
        Expect.Sequence(shell.Agents.Groups.SelectMany(g => g.Rows).Select(r => r.Ref), Rows(window).Select(b => ((SessionRowViewModel)b.DataContext!).Ref));
        Frame(window, "agents-three", "u11a-");
    });

    [Fact]
    public Task Each_row_is_named_provider_title_state_host() => OnUi(async () =>
    {
        var (window, shell) = await OnAgents();

        foreach (var row in Rows(window))
        {
            var vm = (SessionRowViewModel)row.DataContext!;
            Assert.Equal(vm.Name, Heard.Name(row));
            Assert.Contains(vm.Provider, vm.Name, StringComparison.Ordinal);
            Assert.Contains(vm.HostText, vm.Name, StringComparison.Ordinal);
        }

        Assert.Equal(shell.Agents.Head, Named<TextBlock>(window, "AgentsHead").Text);
        Assert.Equal(shell.Agents.HeadName, Heard.Wrapper(Named<Border>(window, "AgentsHeadName")));
        Assert.Contains(Rows(window), b => ((SessionRowViewModel)b.DataContext!).IsAsking);
    });

    [Fact]
    public Task Problem_and_unavailable_lines_are_named_text() => OnUi(async () =>
    {
        var (window, shell) = await OnAgents(() => Server.SessionAnswer = RouteAnswer.Api("sessions", "problems-source-failed"));
        var line = window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("problem-line") && b.IsEffectivelyVisible);

        Assert.Equal(shell.Agents.ProblemLines[0], Heard.Wrapper(line));
        Frame(window, "agents-problems", "u11a-");

        Server.SessionAnswer = RouteAnswer.Problem("sessions", "disabled");
        _ = shell.Agents.Refresh(new Uri("http://127.0.0.1:7410/"), Token);
        await Until(window, () => shell.Agents.IsUnavailable);
        Assert.Equal(shell.Agents.UnavailableText, Heard.Wrapper(Named<Border>(window, "AgentsUnavailableName")));
        Assert.False(Visible(window, "AgentGroups") && Rows(window).Count > 0);
        Frame(window, "agents-unavailable", "u11a-");
    });

    [Fact]
    public Task The_empty_and_ended_states_are_named_and_framed() => OnUi(async () =>
    {
        var (window, shell) = await OnAgents(() => Server.SessionAnswer = RouteAnswer.Api("sessions", "empty"));

        Assert.Equal(shell.Agents.EmptyText, Heard.Wrapper(Named<Border>(window, "AgentsEmptyName")));
        Assert.Equal(Labels.CursorChatsNotShown, Heard.Wrapper(Named<Border>(window, "AgentsFooterName")));
        Frame(window, "agents-empty", "u11a-");

        Server.SessionAnswer = RouteAnswer.Api("sessions", "recently-ended");
        _ = shell.Agents.Refresh(new Uri("http://127.0.0.1:7410/"), Token);
        await Until(window, () => shell.Agents.HasEnded);
        Assert.False(Visible(window, "AgentsEnded"));
        Press(window, Key.E);
        Settle(window);
        Assert.True(Visible(window, "AgentsEnded"));
        Assert.Equal(shell.Agents.Ended[0].Name, Heard.Name(Named<Border>(window, "EndedRowFrame")));
        Frame(window, "agents-ended-open", "u11a-");
    });

    [Fact]
    public Task Refresh_does_not_move_focus_or_scroll() => OnUi(async () =>
    {
        var (window, shell) = await OnAgents(height: 360);
        var open = Named<Button>(window, "SessionRowOpen");
        Assert.True(open.Focus(NavigationMethod.Tab));
        var scroll = Named<ScrollViewer>(window, "Pages");
        scroll.Offset = new Avalonia.Vector(0, 30);
        Settle(window);
        var (focused, offset, reads) = (window.FocusManager?.GetFocusedElement(), scroll.Offset, shell.Agents.Drawn);

        _ = shell.Agents.Refresh(new Uri("http://127.0.0.1:7410/"), Token);
        await Until(window, () => shell.Agents.Drawn > reads, "the list was not read again");

        Assert.Same(focused, window.FocusManager?.GetFocusedElement());
        Assert.Equal(offset, scroll.Offset);
    });

    [Fact]
    public Task J_K_Enter_open_the_sheet_on_the_selected_row() => OnUi(async () =>
    {
        var (window, shell) = await OnAgents();
        SessionOpenRequestedEventArgs? opened = null;
        shell.Agents.OpenRequested += (_, e) => opened = e;

        Press(window, Key.J);
        Press(window, Key.J);
        Settle(window);
        var selected = shell.Agents.SelectedKey!;
        Assert.True(Rows(window).Single(b => ((SessionRowViewModel)b.DataContext!).Ref == selected).IsKeyboardFocusWithin);
        Press(window, Key.Enter);

        Assert.Equal(new DeepLink(DeepLinkKind.Session, selected), opened!.Link);
    });

    // F3: a row draws no Focus button or CLI line of its own (Watch the agent lives on the session's sheet, under Details); O on a row with no Step puts keyboard focus on the row.
    [Fact]
    public Task O_moves_focus_to_the_row_which_draws_no_focus_line() => OnUi(async () =>
    {
        var (window, shell) = await OnAgents();
        Press(window, Key.J);
        Press(window, Key.J);   // the launched row comes first; O on it opens its run
        Settle(window);

        Press(window, Key.O);
        Settle(window);

        var rows = Rows(window);
        Assert.Contains(rows, b => b.IsKeyboardFocusWithin);
        Assert.All(rows, r => Assert.DoesNotContain(r.GetVisualDescendants().OfType<Control>(), c => c.IsEffectivelyVisible && _focusNames.Contains(c.Name ?? string.Empty)));
        Assert.NotNull(shell.Agents.SelectedKey);
    });

    [Fact]
    public Task Minimising_the_window_stops_the_page_polling_and_restoring_resumes_it() => OnUi(async () =>
    {
        var (window, shell) = await OnAgents();
        Assert.True(shell.Agents.IsActive);

        window.WindowState = WindowState.Minimized;
        Dispatcher.UIThread.RunJobs();
        Assert.False(shell.Agents.IsActive);

        window.WindowState = WindowState.Normal;
        Dispatcher.UIThread.RunJobs();
        Assert.True(shell.Agents.IsActive);
    });

    [Fact]
    public Task The_provider_and_state_filters_keep_showing_their_choice_after_the_list_fills_them() => OnUi(async () =>
    {
        var (window, shell) = await OnAgents();

        Assert.Equal(Labels.AllProviders, Named<ComboBox>(window, "ProviderFilter").SelectedItem);
        Assert.Equal(Labels.AllStates, Named<ComboBox>(window, "StateFilter").SelectedItem);
        Assert.Equal(Labels.AllRepos, Named<ComboBox>(window, "RepoFilter").SelectedItem);
        Assert.Null(shell.Agents.ProviderFilter);
    });

    // WCAG 2.5.3: a combo shows its choice, so its name starts with it.
    [Fact]
    public Task The_filter_names_start_with_the_choice_they_show() => OnUi(async () =>
    {
        var (window, shell) = await OnAgents();

        Assert.StartsWith(Labels.AllProviders, Heard.Name(Named<ComboBox>(window, "ProviderFilter")), StringComparison.Ordinal);
        Assert.StartsWith(Labels.AllStates, Heard.Name(Named<ComboBox>(window, "StateFilter")), StringComparison.Ordinal);
        Assert.StartsWith(Labels.AllRepos, Heard.Name(Named<ComboBox>(window, "RepoFilter")), StringComparison.Ordinal);
        shell.Agents.FilterState("running");
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("running", Heard.Name(Named<ComboBox>(window, "StateFilter")), StringComparison.Ordinal);
    });
}
