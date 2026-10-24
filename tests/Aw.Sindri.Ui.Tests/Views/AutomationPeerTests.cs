using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// a11y-0: a Border has no automation peer of its own, so a focusable or named one is invisible to a screen reader (its
/// name and focus go nowhere) until its AccessibilityView is Control. The regions and rows the keyboard lands on are
/// control elements with the type a screen reader announces them as.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class AutomationPeerTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static readonly string[] _homePanels = ["NeedsYouPanel", "RunningPanel", "QueuedPanel"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AutomationPeerTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0 && shell.Health.Catalog.Controls.Count > 0);
        return (window, shell);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task Until(Window window, Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Settle(window);
        Assert.True(condition(), "the view never reached the expected state");
    }

    private static Border First(Window window, Func<Border, bool> match) => window.GetVisualDescendants().OfType<Border>().First(match);

    private static (bool IsControl, AutomationControlType Type) Peer(Control control)
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        return (peer.IsControlElement(), peer.GetAutomationControlType());
    }

    [Fact]
    public Task The_page_region_and_the_rows_the_keyboard_lands_on_are_control_elements() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Home);
        Settle(window);
        var compact = First(window, b => b.Name == "RowFrame" && b.IsEffectivelyVisible);
        shell.GoTo(PageId.Work);
        Settle(window);
        var row = First(window, b => b.Name == "RowFrame" && b.IsEffectivelyVisible);
        shell.OpenItem(A.NewItem);
        await Until(window, () => shell.Item.HasItem && shell.Item.Timeline.Count > 0);
        var timeline = First(window, b => b.Classes.Contains("timeline-row") && b.IsEffectivelyVisible);

        Assert.Equal((true, AutomationControlType.Group), Peer(First(window, b => b.Name == "Page")));
        Assert.Equal((true, AutomationControlType.ListItem), Peer(row));
        Assert.Equal((true, AutomationControlType.ListItem), Peer(timeline));
        Assert.Equal((true, AutomationControlType.ListItem), Peer(compact));
        Assert.Equal((true, AutomationControlType.Group), Peer(First(window, b => b.Name == "Sheet")));
        Assert.Equal((true, AutomationControlType.Button), Peer(First(window, b => b.Name == "SearchField")));
    });

    [Fact]
    public Task The_create_dialogs_card_is_a_control_element_and_a_window() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.Create.Open();
        Settle(window);

        Assert.Equal((true, AutomationControlType.Window), Peer(First(window, b => b.Name == "CreateCard")));
    });

    // testing-1: every other focusable or named Border is a control element of the type a screen reader announces.
    [Fact]
    public Task The_panels_the_top_bar_and_the_overlay_cards_are_control_elements() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Home);
        Settle(window);
        var home = _homePanels.Select(n => Peer(First(window, b => b.Name == n))).ToList();
        var topBar = Peer(First(window, b => b.Classes.Contains("topbar")));
        shell.GoTo(PageId.System);
        Settle(window);
        shell.Palette.Open();
        Settle(window);
        var palette = Peer(First(window, b => b.Name == "PaletteCard"));
        shell.Palette.Close();
        shell.Shortcuts.Open();
        Settle(window);
        var shortcuts = Peer(First(window, b => b.Name == "ShortcutsCard"));

        Assert.All(home, p => Assert.Equal((true, AutomationControlType.Group), p));
        Assert.Equal((true, AutomationControlType.Group), topBar);
        Assert.Equal((true, AutomationControlType.Window), palette);
        Assert.Equal((true, AutomationControlType.Window), shortcuts);
    });

    // a11y-2: the chord hint is a visual cue; it is no live region (ungated, its name going to "" when the chord ends) and not a control.
    [Fact]
    public Task The_chord_hint_is_not_a_live_region_and_not_a_control_element() => OnUi(async () =>
    {
        var (window, _) = await Connected();
        var hint = First(window, b => b.Name == "ChordHint");

        // Rev 8 (R1): the hint is a Text-typed element in the bar, shown only while a chord waits; never a live region.
        Assert.Equal(
            (Avalonia.Automation.AutomationLiveSetting.Off, Avalonia.Automation.AccessibilityView.Control, AutomationControlType.Text, false),
            (Avalonia.Automation.AutomationProperties.GetLiveSetting(hint), Avalonia.Automation.AutomationProperties.GetAccessibilityView(hint), Avalonia.Automation.AutomationProperties.GetControlTypeOverride(hint), hint.IsEffectivelyVisible));
    });
}
