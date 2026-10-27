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

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>Home's ready actions act (#1058): Pause service pauses the service, Complete on a desk row sends one CompleteStep, Complete on a non-desk row is CLI-only.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouHomeActionsSendTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly OffThreadTimers _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NeedsYouHomeActionsSendTests).Assembly)
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
        for (var i = 0; i < 300 && !done(); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }
    }

    [Fact]
    public Task Pause_service_on_a_home_failure_row_pauses_that_service_once() => OnUi(async () =>
    {
        var (feed, service) = FailureFeed();
        var (window, _) = await WithFeed(feed);

        ActionButton(Rows(window).Single(), "PauseService").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitFor(window, () => _server.PauseServiceCalls.Count > 0);

        Assert.Equal([service], _server.PauseServiceCalls, StringComparer.Ordinal);
    });

    // A desk row's Complete reports through the engine's CompleteStep, which needs the desk run and an outcome. The test pins only
    // the observable: one click on Home's Complete (plus at most one confirm from the answer bar, clicked below when the bar offers it)
    // leads to exactly one CompleteStep for that instance.
    [Fact]
    public Task Complete_on_a_desk_row_sends_one_CompleteStep_for_its_instance() => OnUi(async () =>
    {
        var (window, _) = await WithFeed(new NeedsYouFeed([NeedsYouRows.Desk()], ""));

        ActionButton(Rows(window).Single(), "CompleteStep").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitFor(window, () => _server.CompleteCalls.Count > 0);
        if (_server.CompleteCalls.Count == 0)
        {
            var confirm = window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible && b.IsEnabled && string.Equals(b.Content as string, "Complete", StringComparison.Ordinal) && !Rows(window).Exists(r => r.GetVisualDescendants().Contains(b)));
            confirm?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitFor(window, () => _server.CompleteCalls.Count > 0);
        }

        var (instance, _) = Assert.Single(_server.CompleteCalls);
        Assert.Equal(NeedsYouRows.DeskInstance, instance);
    });

    [Fact]
    public Task Complete_on_a_row_that_is_not_a_desk_task_is_not_ready_and_carries_the_command() => OnUi(async () =>
    {
        var desk = NeedsYouRows.Desk();
        var plain = desk with
        {
            Id = "park:ITM-01JL1",
            Subject = new NeedsYouSubject("item", "ITM-01JL1"),
            Actions = [desk.Actions[0] with { Readiness = ActionReadiness.Ready }],
        };
        var (window, _) = await WithFeed(new NeedsYouFeed([plain], ""));

        var button = ActionButton(Rows(window).Single(), "CompleteStep");

        Assert.False(button.IsEnabled);
        Assert.Equal(plain.Cli, ToolTip.GetTip(button));
    });
}
