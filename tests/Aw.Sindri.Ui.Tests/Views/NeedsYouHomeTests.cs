using Avalonia;
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
using Aw.Sindri.Ui.Views.Controls;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>Home's Needs you panel under the headless platform (U3' T9): rows with the one chip, the summary line, the overflow, the sheets.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouHomeTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NeedsYouHomeTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected(double? width = null)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        if (width is { } w)
        {
            window.Width = w;
        }

        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0);
        return (window, shell);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task Until(Window window, Func<bool> condition, string what = "the view never reached the expected state")
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Settle(window);
        Assert.True(condition(), what);
    }

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);


    private static List<NeedsYouRow> Rows(Window window) =>
        [.. Named<Control>(window, "NeedsYouPanel").GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible)];

    private async Task<(ShellWindow Window, ShellViewModel Shell)> WithRows(int count)
    {
        var (window, shell) = await Connected();
        shell.ShowNeedsYou(new ViewModels.NeedsYou.NeedsYouFeed([.. Enumerable.Range(0, count).Select(i => NeedsYouRows.Park("park:ITM-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)))], ""));
        Settle(window);
        return (window, shell);
    }

    [Fact]
    public Task Home_draws_the_served_rows_in_place_of_the_placeholder() => OnUi(async () =>
    {
        var (window, shell) = await WithRows(3);

        Assert.Equal(3, Rows(window).Count);
        Assert.DoesNotContain(Named<Control>(window, "NeedsYouPanel").GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == Labels.NotServedYet);
        Assert.Equal(shell.Attention.SummaryLine, Named<TextBlock>(window, "NeedsYouSummary").Text);
    });

    [Fact]
    public Task A_row_draws_its_chip_with_the_one_chip_control() => OnUi(async () =>
    {
        var (window, _) = await WithRows(1);

        var chip = Rows(window)[0].GetVisualDescendants().OfType<StateChipView>().Single();

        Assert.True(chip.IsEffectivelyVisible);
        Assert.Equal("PARKED", chip.GetVisualDescendants().OfType<TextBlock>().First().Text);
    });

    // Proof B5: the in-app Reduce motion switch, through the shell's appearance path, swaps the pulse for the outline and "new".
    [Fact]
    public Task Reduce_motion_draws_the_outline_and_the_word_new_and_no_pulse() => OnUi(async () =>
    {
        var (window, shell) = await WithRows(2);
        var frame = Rows(window)[0].GetVisualDescendants().OfType<Border>().First(b => string.Equals(b.Name, "RowFrame", StringComparison.Ordinal));
        Assert.Contains(frame.Classes, c => string.Equals(c, "pulsing", StringComparison.Ordinal));
        Assert.True(Named<TextBlock>(Rows(window)[0], "NewWord").IsEffectivelyVisible);   // motion only adds to the word (test-r3-5)

        shell.ApplyAppearance(new ViewModels.Settings.AppearanceSettings(1.0, ReduceMotion: true));
        Settle(window);

        Assert.Equal((false, true), (frame.Classes.Contains("pulsing"), frame.Classes.Contains("outlined")));
        Assert.True(Named<TextBlock>(Rows(window)[0], "NewWord").IsEffectivelyVisible);
    });

    [Fact]
    public Task Seven_rows_show_five_then_Show_all_opens_the_inbox_with_all_seven() => OnUi(async () =>
    {
        var (window, shell) = await WithRows(7);
        Assert.Equal(5, Rows(window).Count);

        var showAll = Named<Button>(window, "NeedsYouShowAll");
        Assert.True(showAll.IsEffectivelyVisible);
        showAll.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.True(shell.IsNeedsYouInbox);
        Assert.Equal(7, shell.Inbox.Groups.Sum(g => g.Rows.Count));
        Assert.False(showAll.IsEffectivelyVisible);   // Home's panel is behind the inbox
    });

    [Fact]
    public Task A_needs_you_link_shows_the_row_sheet_not_the_placeholder() => OnUi(async () =>
    {
        var (window, shell) = await WithRows(2);

        shell.NavigateToRowSheet(new DeepLink(DeepLinkKind.NeedsYou, "park:ITM-1"));
        Settle(window);

        Assert.True(Named<Control>(window, "NeedsYouSheet").IsEffectivelyVisible);
        Assert.False(Named<Control>(window, "SheetBody").IsEffectivelyVisible);
    });

    [Fact]
    public Task Clicking_a_rows_title_selects_it_and_opens_the_inbox_on_it() => OnUi(async () =>
    {
        var (window, shell) = await WithRows(2);

        Named<Button>(Rows(window)[1], "NeedsYouTitle").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.Equal(("park:ITM-1", true, false), (shell.NeedsYou.SelectedId, shell.IsNeedsYouInbox, shell.IsSheetOpen));
    });

    [Fact]
    public Task A_decision_link_shows_the_decision_sheet_with_one_button_per_option() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.ShowNeedsYou(new ViewModels.NeedsYou.NeedsYouFeed([NeedsYouRows.Decision()], ""));

        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        Settle(window);

        var sheet = Named<Control>(window, "DecisionSheet");
        Assert.True(sheet.IsEffectivelyVisible);
        Assert.Equal(["Accept", "Decline"], [.. sheet.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("decision-option")).Select(b => (string?)b.Content ?? "")]);
        Assert.Equal("Re-scope: drop the iOS head from this item", Named<TextBlock>(sheet, "DecisionQuestion").Text);
    });

    // Proof B4: the sheet asks the served question, not only the row's title.
    [Fact]
    public Task The_decision_sheet_shows_the_served_question() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.ShowNeedsYou(new ViewModels.NeedsYou.NeedsYouFeed([NeedsYouRows.Decision() with { Question = "Accept this re-scope request?\nDrop the iOS head" }], ""));

        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        Settle(window);

        Assert.Equal("Accept this re-scope request?\nDrop the iOS head", Named<TextBlock>(Named<Control>(window, "DecisionSheet"), "DecisionQuestion").Text);
    });

    [Fact]
    public Task Pressing_an_option_sends_Decide_and_the_outcome_shows() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.ShowNeedsYou(new ViewModels.NeedsYou.NeedsYouFeed([NeedsYouRows.Decision()], ""));
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        Settle(window);

        Named<Control>(window, "DecisionSheet").GetVisualDescendants().OfType<Button>().Single(b => string.Equals(b.Content as string, "Decline", StringComparison.Ordinal))
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await shell.Decision.Settled;
        Settle(window);

        Assert.Equal("Decided: Decline", Named<TextBlock>(window, "DecisionOutcome").Text);
    });

    // §11.2 as U2's F2, for a Needs-you row: with its sheet beside the page, Enter again moves into the sheet's first action.
    [Fact]
    public Task Enter_again_on_a_decision_row_with_its_sheet_open_focuses_the_first_option() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1400);
        shell.ShowNeedsYou(new ViewModels.NeedsYou.NeedsYouFeed([NeedsYouRows.Decision()], ""));
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        Settle(window);
        var first = Named<Control>(window, "DecisionSheet").GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("decision-option"));
        Assert.False(ReferenceEquals(window.FocusManager?.GetFocusedElement(), first));

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(window);

        Assert.True(ReferenceEquals(window.FocusManager?.GetFocusedElement(), first));
    });

    [Fact]
    public Task Enter_again_on_a_row_whose_sheet_has_no_action_focuses_the_sheets_heading() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1400);
        shell.ShowNeedsYou(new ViewModels.NeedsYou.NeedsYouFeed([NeedsYouRows.Failure()], ""));
        shell.NavigateToRowSheet(new DeepLink(DeepLinkKind.NeedsYou, "failure:service:index"));
        Settle(window);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(window);

        Assert.True(ReferenceEquals(window.FocusManager?.GetFocusedElement(), Named<Control>(window, "SheetTitle")));
    });

    // U3' T8: the item's sheet lists its pending decisions with their option buttons and the requests filed against it.
    [Fact]
    public Task The_item_sheet_draws_the_items_decisions_and_requests() => OnUi(async () =>
    {
        const string item = "ITM-0000000000000000000000000000000e";
        _server.DecisionsByItem[item] = RouteAnswer.Api("decisions", "by-item");
        _server.RequestsByItem[item] = RouteAnswer.Api("requests", "list-by-item");
        _server.Items[item] = _server.Items[A.NewItem];
        var (window, shell) = await Connected(width: 1400);

        shell.Navigate(new DeepLink(DeepLinkKind.Item, item));
        await Until(window, () => shell.Item.HasItem && shell.Item.Needs.Decisions.Count == 1);

        var served = GoldenFiles.Node("decisions", "by-item")["decisions"]![0]!;
        var sheet = Named<Control>(window, "ItemDecisionList");
        Assert.Equal(served["options"]!.AsArray().Select(o => (string)o!["label"]!), sheet.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("decision-option")).Select(b => (string?)b.Content ?? ""), StringComparer.Ordinal);
        Assert.Equal((string)GoldenFiles.Node("requests", "list-by-item")["requests"]![0]!["text"]!, Named<Control>(window, "ItemRequestList").GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("body")).Text);
    });

    // Review run 3 (a11y-r3-0): a pending Decide leaves the options disabled with their help text and one focusable line naming the phase.
    [Fact]
    public Task A_pending_Decide_gives_the_item_sheet_options_help_text_and_a_focusable_line() => OnUi(async () =>
    {
        const string item = "ITM-0000000000000000000000000000000e";
        var catalog = GoldenFiles.Node("commands", "catalog");
        catalog["rows"]!.AsArray().First(r => (string?)r!["action"] == "Decide")!["ui"] = "pending:P4";
        _server.Catalog = new RouteAnswer(System.Net.HttpStatusCode.OK, catalog.ToJsonString());
        _server.DecisionsByItem[item] = RouteAnswer.Api("decisions", "by-item");
        _server.RequestsByItem[item] = RouteAnswer.Api("requests", "list-by-item");
        _server.Items[item] = _server.Items[A.NewItem];
        var (window, shell) = await Connected(width: 1400);

        shell.Navigate(new DeepLink(DeepLinkKind.Item, item));
        await Until(window, () => shell.Item.HasItem && shell.Item.Needs.Decisions.Count == 1);

        var options = Named<Control>(window, "ItemDecisionList").GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("decision-option")).ToList();
        Assert.All(options, b => Assert.False(b.IsEnabled));
        Assert.All(options, b => Assert.Contains("sindri decide", Avalonia.Automation.AutomationProperties.GetHelpText(b), StringComparison.Ordinal));
        var line = Named<Control>(window, "ItemDecisionList").GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("pending-line"));
        Assert.True(line.IsEffectivelyVisible && line.Focusable);
        Assert.Contains("P4", Avalonia.Automation.AutomationProperties.GetName(line), StringComparison.Ordinal);
    });
}
