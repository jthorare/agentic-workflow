using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Aw.Sindri.Ui.Views.Todo;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Panels.Tests.Views;

/// <summary>
/// #2908 and D13 (todo spec): the collapsed Todo card keeps one height whatever it holds, the From elsewhere line sits right under the last
/// todo, and a sync that brings in Show all leaves Needs you where it was.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TodoPanelHeightTests : IDisposable
{
    private const double MaxBand = 24;   // one row gap at most between the last todo and the From elsewhere line

    private static readonly Uri Api = new("http://127.0.0.1:7410/");

    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TodoPanelHeightTests).Assembly)
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

    // `count` open todos with no date and no links, as the server answers them.
    private void Serve(int count)
    {
        var list = JsonNode.Parse(TodoSample.List)!;   // why: the sample is a JSON document
        list["todos"] = new JsonArray([.. Enumerable.Range(1, count).Select(i =>
        {
            var todo = list["todos"]![3]!.DeepClone();   // "call the dentist": open, no date, no links
            todo["id"] = string.Create(CultureInfo.InvariantCulture, $"td_{i}");
            todo["text"] = string.Create(CultureInfo.InvariantCulture, $"todo number {i}");
            return todo;
        })]);
        _server.TodosAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
    }

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected(bool waitForTodos = true)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = 1440, Height = 900 };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.Normal && (!waitForTodos || (shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0 && !shell.Todo.IsLoading)));
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
        for (var i = 0; i < 3000 && !condition(); i++)
        {
            await Task.Delay(1, Token);
            Settle(window);
        }

        Settle(window);
        Assert.True(condition(), "the view never reached the expected state");
    }

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static Control Panel(Window window) => Named<Control>(window, "TodoPanelFrame");

    private static double Top(Window window, string name) => Named<Control>(window, name).TranslatePoint(default, window)!.Value.Y;   // why: both are in the window's visual tree

    // The collapsed card's height with `count` todos on a fresh window: held in its skeleton when `hold`, with the error line when `fail`.
    private async Task<double> CardHeight(int count, bool hold = false, bool fail = false)
    {
        Serve(count);
        _server.TodosGate = hold ? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        var (window, shell) = await Connected(waitForTodos: !hold);
        if (fail)
        {
            _server.TodosAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views are behind.");
            await shell.Todo.Refresh(Api, Token);
            Settle(window);
            Assert.NotEqual(string.Empty, shell.Todo.Error);
        }

        Assert.Equal(hold, shell.Todo.IsLoading);
        var height = Panel(window).Bounds.Height;
        _server.TodosGate?.SetResult();
        window.Close();
        return height;
    }

    [Fact]
    public Task The_collapsed_card_keeps_one_height_at_any_count_loading_and_with_the_error_line_and_an_empty_one_is_shorter() => OnUi(async () =>
    {
        var skeleton = await CardHeight(8, hold: true);
        int[] counts = [1, 3, 5, 6, 7];
        var heights = new List<double>();
        foreach (var count in counts)
        {
            heights.Add(await CardHeight(count));
        }

        var error = await CardHeight(3, fail: true);
        var empty = await CardHeight(0);

        Assert.True(skeleton > 0);
        double[] expected = [.. counts.Select(_ => skeleton), skeleton];
        double[] actual = [.. heights, error];
        Assert.Equal(expected, actual);   // D13: one height at 1, 3, 5, 6 and 7 todos and with the error line
        Assert.True(empty > 0 && empty < skeleton, string.Create(CultureInfo.InvariantCulture, $"empty {empty} vs {skeleton}"));   // #2548: an empty list collapses to one line
    });

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public Task The_From_elsewhere_line_sits_one_row_gap_under_the_last_todo(int count) => OnUi(async () =>
    {
        Serve(count);
        var (window, _) = await Connected();

        var rows = Panel(window).GetVisualDescendants().OfType<TodoRow>().Where(r => r.IsEffectivelyVisible).ToList();
        Assert.Equal(count, rows.Count);
        var last = rows[^1];
        var bottom = last.TranslatePoint(new Point(0, last.Bounds.Height), window)!.Value.Y;   // why: both are in the window's visual tree
        var band = Top(window, "TodoElsewhereSlot") - bottom;

        Assert.True(band >= 0 && band <= MaxBand, string.Create(CultureInfo.InvariantCulture, $"{band} px between todo {count} and the From elsewhere line"));
    });

    [Fact]
    public Task A_sync_from_five_to_six_todos_leaves_Needs_you_where_it_was() => OnUi(async () =>
    {
        Serve(5);
        var (window, shell) = await Connected();
        var before = Top(window, "NeedsYouPanel");
        Assert.False(shell.Todo.ShowsShowAll);

        Serve(6);
        await shell.Todo.Refresh(Api, Token);
        Settle(window);

        Assert.True(shell.Todo.ShowsShowAll);
        Assert.Equal(before, Top(window, "NeedsYouPanel"));
    });
}
