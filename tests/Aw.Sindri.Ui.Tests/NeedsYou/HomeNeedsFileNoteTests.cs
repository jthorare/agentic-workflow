using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>#1222: Home's "needs a file first" note is shown whole beside the row's buttons, not under them, and names no button the row lacks.</summary>
[Lane(TestLane.Integration)]
public sealed class HomeNeedsFileNoteTests : IDisposable
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly OffThreadTimers _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HomeNeedsFileNoteTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> DeskWindow()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = 900 };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 300 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.Equal(ShellState.Normal, shell.State);
        // A longer repo name: at the window's minimum width the label line has no room left for the note beside it.
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Desk() with { Repo = "customer-platform-billing-reconciliation-service" }], ""));
        Settle(window);
        return (window, shell);
    }

    [Fact]
    public Task Needs_file_note_never_sits_under_the_row_buttons() => OnUi(async () =>
    {
        var (window, shell) = await DeskWindow();
        var row = window.GetVisualDescendants().OfType<NeedsYouRow>().Single(r => r.IsEffectivelyVisible);
        var complete = row.GetVisualDescendants().OfType<Button>()
            .First(b => string.Equals(b.Content as string, Labels.Action("CompleteStep"), StringComparison.Ordinal));
        var id = "desk:" + NeedsYouRows.DeskInstance;

        complete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (var i = 0; i < 300 && !shell.NeedsYou.Row(id)!.HasNotice; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Settle(window);
        Assert.Equal(Labels.CompleteNeedsOutputInSheet, shell.NeedsYou.Row(id)!.Notice);

        var notice = row.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "NeedsYouNotice");
        var noticeBox = Box(notice, row);
        var buttons = row.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Bounds.Width > 0).Select(b => Box(b, row)).ToList();

        Assert.NotEmpty(buttons);
        Assert.All(buttons, b => Assert.False(noticeBox.Intersects(b), $"note {Show(noticeBox)} runs under a button at {Show(b)}"));
        Assert.True(noticeBox.Right <= row.Bounds.Width, $"note {Show(noticeBox)} is wider than the row {Show(row.Bounds)}");
        Assert.False(notice.TextLayout.TextLines.Any(l => l.HasCollapsed), $"note {Show(noticeBox)} is cut off with an ellipsis");
    });

    private static string Show(Rect r) => string.Create(CultureInfo.InvariantCulture, $"[x {r.X}, y {r.Y}, w {r.Width}, h {r.Height}]");

    private static Rect Box(Control control, Visual row) =>
        new(control.TranslatePoint(new Point(0, 0), row) ?? default, control.Bounds.Size);

    [Fact]
    public async Task Needs_file_note_names_no_button_the_row_lacks()
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "f5-desk-recommended"))!;
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, instance.ToJsonString());
        var dispatcher = new NeedsYouDispatcher(_server, () => _api, new InstanceCommander(_server, () => _api), new DeskReporter(_server, () => _api));

        var result = await dispatcher.Run(NeedsYouRows.Desk(), NeedsYouRows.Desk().Actions[0]);

        Assert.Empty(_server.CompleteCalls);
        Assert.DoesNotContain("Answer", result.Message ?? "", StringComparison.Ordinal);
    }
}
