using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Home's actions never fail silently (#1058): a Complete or Pause service that sends nothing or is refused says why on the row.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouHomeActionNoteTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly OffThreadTimers _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NeedsYouHomeActionNoteTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> WithFeed(NeedsYouFeed feed)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 3000 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(1, Token);
            Settle(window);
        }

        Assert.Equal(ShellState.Normal, shell.State);
        shell.ShowNeedsYou(feed);
        Settle(window);
        return (window, shell);
    }

    private static List<NeedsYouRow> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible)];

    private static T Golden<T>(string slug, string scenario)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(GoldenFiles.Api(slug, scenario)), SindriApiClient.Json).Value;

    private static (NeedsYouFeed Feed, string Service) FailureFeed()
    {
        var list = Golden<NeedsYouList>("needs-you", "fixture");
        var catalog = Golden<CommandList>("commands", "catalog");
        var failure = list.Rows.Single(r => r.Kind == NeedsYouKinds.Failure);
        return (NeedsYouMapper.Map(list with { Rows = [failure] }, catalog, decisions: null, new HomeCounts(0, 0, 0)), failure.Subject!.Id);
    }

    private static Button ActionButton(NeedsYouRow row, string catalogId) =>
        row.GetVisualDescendants().OfType<Button>().First(b => string.Equals(b.Content as string, Labels.Action(catalogId), StringComparison.Ordinal));

    private static async Task WaitFor(Window window, Func<bool> done)
    {
        for (var i = 0; i < 3000 && !done(); i++)
        {
            await Task.Delay(1, Token);
            Settle(window);
        }
    }

    [Fact]
    public Task A_refused_Pause_service_shows_the_refusal_on_the_row() => OnUi(async () =>
    {
        _server.PauseServiceAnswer = RouteAnswer.Refused("This caller may not pause.");
        var (feed, _) = FailureFeed();
        var (window, shell) = await WithFeed(feed);

        ActionButton(Rows(window).Single(), "PauseService").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitFor(window, () => shell.NeedsYou.Rows.Any(r => r.HasNotice));

        Assert.Equal(Labels.ActionRefused("SND-AUTH-002", "This caller may not pause."), shell.NeedsYou.Rows.Single().Notice);
    });
}
