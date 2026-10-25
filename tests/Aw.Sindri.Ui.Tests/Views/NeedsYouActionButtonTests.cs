using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>A row's own buttons act (real-app re-proof, NB1): clicking or pressing Space on a row's own button sends the action.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouActionButtonTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly OffThreadTimers _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NeedsYouActionButtonTests).Assembly)
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

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private Task<(ShellWindow Window, ShellViewModel Shell)> WithRows(int n) =>
        WithFeed(new NeedsYouFeed([.. Enumerable.Range(0, n).Select(i => NeedsYouRows.Decision("decision:DEC-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)))], ""));

    private async Task<(ShellWindow Window, ShellViewModel Shell)> WithFeed(NeedsYouFeed feed)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 300 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.Equal(ShellState.Normal, shell.State);
        shell.ShowNeedsYou(feed);
        Settle(window);
        return (window, shell);
    }

    private static List<NeedsYouRow> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible)];


    // Decline, not the first option: a button that sent the wrong option would pass with Accept.
    private static Button Decline(NeedsYouRow row) =>
        row.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && string.Equals(b.Content as string, "Decline", StringComparison.OrdinalIgnoreCase));

    private async Task SentOnce(Window window)
    {
        for (var i = 0; i < 300 && _server.DecideCalls.Count == 0; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        var (decision, request) = Assert.Single(_server.DecideCalls);
        Assert.Equal("DEC-1", decision.Replace("decision:", "", StringComparison.Ordinal));
        Assert.Equal("decline", request.Choice);
    }

    [Fact]
    public Task A_click_on_a_rows_own_option_button_sends_decline_once() => OnUi(async () =>
    {
        var (window, _) = await WithRows(1);
        var button = Decline(Rows(window)[0]);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await SentOnce(window);
    });

    [Fact]
    public Task Space_on_the_focused_option_button_sends_decline_once() => OnUi(async () =>
    {
        var (window, _) = await WithRows(1);
        var button = Decline(Rows(window)[0]);
        Assert.True(button.Focus(NavigationMethod.Tab));
        Settle(window);

        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        await SentOnce(window);
    });


    private const string Unfillable = "sindri services pause <itemId>";

    private static T Golden<T>(string slug, string scenario)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(GoldenFiles.Api(slug, scenario)), SindriApiClient.Json).Value;

    // The golden's failure row alone, against the golden catalog with its PauseService row changed by `change`.
    private static (NeedsYouFeed Feed, string Pause) FailureFeed(Func<CommandRowView, CommandRowView> change)
    {
        var list = Golden<NeedsYouList>("needs-you", "fixture");
        var catalog = Golden<CommandList>("commands", "catalog");
        var failure = list.Rows.Single(r => r.Kind == NeedsYouKinds.Failure);
        var pause = catalog.Rows.Single(r => r.Action == "PauseService").Cli.Replace("<name>", failure.Subject!.Id, StringComparison.Ordinal);
        var changed = catalog with { Rows = [.. catalog.Rows.Select(r => r.Action == "PauseService" ? change(r) : r)] };
        return (NeedsYouMapper.Map(list with { Rows = [failure] }, changed, decisions: null, new HomeCounts(0, 0, 0)), pause);
    }

    private static Button PauseButton(NeedsYouRow row) =>
        row.GetVisualDescendants().OfType<Button>().First(b => string.Equals(b.Content as string, Labels.Action("PauseService"), StringComparison.Ordinal));

    private static SheetDetail PauseLine(NeedsYouRow row) =>
        ((NeedsYouRowViewModel)row.DataContext!).Details.Single(d => d.Label == Labels.Action("PauseService"));

    [Fact]
    public Task A_ready_Pause_shows_the_pause_command_on_the_sheet_without_a_hint() => OnUi(async () =>
    {
        var (feed, pause) = FailureFeed(r => r);
        var (window, _) = await WithFeed(feed);

        var line = PauseLine(Rows(window).Single());

        Assert.Equal(pause, line.Value);
        Assert.Empty(line.Hint);
    });

    // q93: the catalog's ui word gates nothing; a Pause the catalog does not list is pending and carries the row's served command.
    [Fact]
    public Task A_pending_Pauses_tooltip_help_text_and_sheet_line_carry_the_row_command() => OnUi(async () =>
    {
        var list = Golden<NeedsYouList>("needs-you", "fixture");
        var catalog = Golden<CommandList>("commands", "catalog");
        var failure = list.Rows.Single(r => r.Kind == NeedsYouKinds.Failure);
        var unlisted = catalog with { Rows = [.. catalog.Rows.Where(r => r.Action != "PauseService")] };
        var feed = NeedsYouMapper.Map(list with { Rows = [failure] }, unlisted, decisions: null, new HomeCounts(0, 0, 0));
        var (window, _) = await WithFeed(feed);

        var row = Rows(window).Single();
        var pause = failure.Cli;

        Assert.Equal(pause, ToolTip.GetTip(PauseButton(row)));
        Assert.Equal(pause, AutomationProperties.GetHelpText(PauseButton(row)));
        Assert.Equal(Labels.NotInTheAppYet(pause), PauseLine(row).Value);
        Assert.Equal(pause, PauseLine(row).Command);
    });

    private static List<TextBlock> SheetHintCaptions(ShellWindow window, string hint) =>
        [.. window.GetVisualDescendants().OfType<NeedsYouSheet>().Single().GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && string.Equals(t.Text, hint, StringComparison.Ordinal))];

    [Fact]
    public Task The_failure_sheet_draws_the_hint_as_a_caption_a_screen_reader_names() => OnUi(async () =>
    {
        var (feed, _) = FailureFeed(r => r with { Cli = Unfillable });
        var (window, shell) = await WithFeed(feed);
        var hint = Labels.FillInHint(["itemId"]);

        shell.NavigateToRowSheet(new DeepLink(DeepLinkKind.NeedsYou, feed.Rows[0].Id));
        Settle(window);

        var caption = Assert.Single(SheetHintCaptions(window, hint));
        Assert.Equal(hint, Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(caption).GetName());
    });

    [Fact]
    public Task The_failure_sheet_draws_no_hint_caption_when_the_command_is_complete() => OnUi(async () =>
    {
        var (feed, _) = FailureFeed(r => r);
        var (window, shell) = await WithFeed(feed);

        shell.NavigateToRowSheet(new DeepLink(DeepLinkKind.NeedsYou, feed.Rows[0].Id));
        Settle(window);

        var sheet = window.GetVisualDescendants().OfType<NeedsYouSheet>().Single();
        Assert.True(sheet.IsEffectivelyVisible);
        Assert.DoesNotContain(sheet.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text?.StartsWith("fill in", StringComparison.Ordinal) == true);
    });
}
