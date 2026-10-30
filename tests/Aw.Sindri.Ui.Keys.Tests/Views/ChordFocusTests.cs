using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Keys.Tests.Views;

/// <summary>Review run 5 (a11y-0): a page change made by a G-chord or the palette's Go to lands focus on the new page's region, wherever focus was.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ChordFocusTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ChordFocusTests).Assembly)
            .Dispatch(async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    await _stop.CancelAsync();
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, Token);

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Show(ShellState state)
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 200 && shell.State != state; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(state, shell.State);
        Dispatcher.UIThread.RunJobs();
        return (window, shell);
    }

    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private static readonly RawInputModifiers _command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    private static Control Named(Window window, string name) =>
        window.GetVisualDescendants().OfType<Control>().First(c => c.Name == name);

    private static void AssertOnPageRegion(Window window, ShellViewModel shell)
    {
        var focused = window.FocusManager?.GetFocusedElement() as Control;
        Assert.Same(window.Get<Border>("Page"), focused);
        var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(focused!);
        Assert.Equal(shell.PageName, peer.GetName());
    }

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Opened(string start)
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);
        window.Width = 1400;
        if (start == "sheet")
        {
            Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
            shell.Navigate(link);
        }

        Dispatcher.UIThread.RunJobs();
        var from = start switch
        {
            "pill" => Named(window, "HealthPill"),
            "nav" => window.GetVisualDescendants().OfType<ListBoxItem>().First(i => i.IsEffectivelyVisible),
            "sheet" => Named(window, "SheetTitle"),
            _ => window.Get<Border>("Page"),
        };
        Assert.True(from.Focus(NavigationMethod.Tab), start);
        Dispatcher.UIThread.RunJobs();
        return (window, shell);
    }

    [Theory]
    [InlineData("pill")]
    [InlineData("nav")]
    [InlineData("sheet")]
    [InlineData("region")]
    public Task A_G_chord_lands_focus_on_the_new_page_region_from_anywhere(string start) => OnUi(async () =>
    {
        var (window, shell) = await Opened(start);
        if (start == "sheet")
        {
            Assert.DoesNotContain("covers", window.Get<Border>("Sheet").Classes, StringComparer.Ordinal);
        }

        Press(window, Key.G);
        Press(window, Key.W);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PageId.Workflows, shell.CurrentPage);
        AssertOnPageRegion(window, shell);
    });

    // The palette's Go to, opened from the page region: the region is focused again once the page changes. The count of
    // GotFocus events on the region is pinned at 2: the palette closing hands focus back, then the page change re-announces (corr-3).
    [Fact]
    public Task The_palette_Go_to_lands_focus_on_the_new_page_region_after_the_palette_hands_focus_back() => OnUi(async () =>
    {
        var (window, shell) = await Opened("region");
        var page = window.Get<Border>("Page");
        window.KeyPress(Key.K, _command, PhysicalKey.K, keySymbol: "k");
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput("workflows");
        Dispatcher.UIThread.RunJobs();
        for (var step = 0; shell.Palette.Selected?.Page != PageId.Workflows && step < 10; step++)
        {
            shell.Palette.Move(1);   // "Approve all workflows" matches the query too and now comes first
        }

        var raised = 0;
        var names = new List<string?>();
        window.AddHandler(InputElement.GotFocusEvent, (_, e) =>
        {
            if (ReferenceEquals(e.Source, page))
            {
                raised++;
                names.Add(Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(page).GetName());
            }
        }, Avalonia.Interactivity.RoutingStrategies.Bubble);

        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(PageId.Workflows, shell.CurrentPage);
        AssertOnPageRegion(window, shell);
        Assert.All(names, n => Assert.Equal(shell.PageName, n));   // nr-1: the page changes before the palette closes, so both events say the new page
        Assert.Equal(2, raised);   // the palette closing returns focus to the region, then the page change raises its fresh event
    });

    // Review re-check Y1: the top-bar nav is a tabs pattern with automatic activation, so Right and Left select the next tab and
    // focus stays on the tab (a chord, the palette and a pointer click still land on the region).
    [Fact]
    public Task Arrow_keys_in_the_nav_move_between_tabs_and_keep_focus_on_the_tab() => OnUi(async () =>
    {
        var (window, shell) = await Opened("nav");
        var items = window.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.IsEffectivelyVisible).ToList();
        Assert.True(items[0].Focus(NavigationMethod.Tab));
        Dispatcher.UIThread.RunJobs();

        var context = shell.Navigation.ToList().FindIndex(n => n.Page == PageId.Context);   // arrowing across Context must keep focus on the tab too, not move it into the anchor box
        for (var step = 1; step <= context; step++)
        {
            Press(window, Key.Right);
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(10, Token);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.Equal(shell.Navigation[step].Page, shell.CurrentPage);
            Assert.Same(items[step], window.FocusManager?.GetFocusedElement());
        }

        Press(window, Key.Left);
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(shell.Navigation[context - 1].Page, shell.CurrentPage);
        Assert.Same(items[context - 1], window.FocusManager?.GetFocusedElement());
    });

    // Review re-check R2: an arrowed tab keeps a visible keyboard focus ring (WCAG 2.4.7), not only the focus.
    [Theory]
    [InlineData(Key.Right)]
    [InlineData(Key.End)]
    public Task An_arrowed_nav_tab_keeps_the_keyboard_focus_ring(Key key) => OnUi(async () =>
    {
        var (window, shell) = await Opened("nav");
        var items = window.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.IsEffectivelyVisible).ToList();
        Assert.True(items[0].Focus(NavigationMethod.Tab));
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(items[0].Classes, c => string.Equals(c, ":focus-visible", StringComparison.Ordinal));

        Press(window, key);
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        var focused = Assert.IsType<ListBoxItem>(window.FocusManager?.GetFocusedElement());
        Assert.Equal(shell.CurrentPage, shell.Navigation[items.IndexOf(focused)].Page);
        Assert.Contains(focused.Classes, c => string.Equals(c, ":focus-visible", StringComparison.Ordinal));
    });
}
