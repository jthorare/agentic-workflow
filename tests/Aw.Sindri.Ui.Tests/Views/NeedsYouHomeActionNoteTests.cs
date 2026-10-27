using System.Net;
using System.Text;
using System.Text.Json.Nodes;
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

    private static Button Complete(Window window) => ActionButton(Rows(window).Single(), "CompleteStep");

    private async Task<(Window Window, ShellViewModel Shell)> DeskWindow()
    {
        var (window, shell) = await WithFeed(new NeedsYouFeed([NeedsYouRows.Desk()], ""));
        return (window, shell);
    }

    [Fact]
    public Task Complete_on_a_step_whose_complete_is_protected_sends_nothing_and_says_so_on_the_row() => OnUi(async () =>
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        foreach (var command in instance["current"]!["status"]!["commands"]!.AsArray().Where(c => c!["verb"]!.GetValue<string>() == "complete"))
        {
            command!["class"] = "protected";
        }

        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, instance.ToJsonString());
        var (window, shell) = await DeskWindow();

        Complete(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitFor(window, () => shell.NeedsYou.Row("desk:" + NeedsYouRows.DeskInstance)!.HasNotice);

        Assert.Empty(_server.CompleteCalls);
        Assert.StartsWith("Can't complete this step from the app. Run: sindri workflow", shell.NeedsYou.Row("desk:" + NeedsYouRows.DeskInstance)!.Notice, StringComparison.Ordinal);
    });

    [Fact]
    public Task Complete_on_an_instance_that_cannot_be_read_sends_nothing_and_says_so_on_the_row() => OnUi(async () =>
    {
        _server.InstanceBody = _ => RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-WORKFLOW-001", "no such instance");
        var (window, shell) = await DeskWindow();

        Complete(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitFor(window, () => shell.NeedsYou.Row("desk:" + NeedsYouRows.DeskInstance)!.HasNotice);

        Assert.Empty(_server.CompleteCalls);
        Assert.Equal(Labels.CompleteInTerminal(""), shell.NeedsYou.Row("desk:" + NeedsYouRows.DeskInstance)!.Notice);
    });

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
