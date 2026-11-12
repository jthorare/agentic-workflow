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

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#2908: the collapsed Todo card has no empty band between its last row and the From elsewhere line.</summary>
[Lane(TestLane.Integration)]
public sealed class TodoFootBandTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TodoFootBandTests).Assembly)
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

    private async Task<ShellWindow> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = 1280, Height = 800 };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 300 && !(shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0 && !shell.Todo.IsLoading); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Settle(window);
        Assert.False(shell.Todo.IsLoading);
        return window;
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    // The gap, in px, between the bottom of the fifth todo row and the top of the From elsewhere slot.
    private static double BandBelowFifthRow(Window window)
    {
        var panel = Named<Control>(window, "TodoPanelFrame");
        var fifth = panel.GetVisualDescendants().OfType<TodoRow>().Where(r => r.IsEffectivelyVisible).ElementAt(4);
        var bottom = fifth.TranslatePoint(new Point(0, fifth.Bounds.Height), panel)!.Value.Y;   // why: both are in the same visual tree
        var slotTop = Named<Control>(window, "TodoElsewhereSlot").TranslatePoint(new Point(0, 0), panel)!.Value.Y;
        return slotTop - bottom;
    }

    [Fact]
    public Task The_From_elsewhere_line_sits_one_row_under_the_last_of_five_todos() => OnUi(async () =>
    {
        Serve(5);
        var window = await Connected();

        var band = BandBelowFifthRow(window);
        Assert.True(Math.Abs(band) <= 1, string.Create(CultureInfo.InvariantCulture, $"{band} px between the fifth row and the From elsewhere line"));
    });

    [Fact]
    public Task Show_all_shares_the_From_elsewhere_row_and_the_row_still_sits_under_the_fifth_todo() => OnUi(async () =>
    {
        Serve(7);
        var window = await Connected();

        var showAll = Named<Control>(window, "TodoShowAll");
        var slot = Named<Control>(window, "TodoElsewhereSlot");
        Assert.True(showAll.IsEffectivelyVisible);
        Assert.Contains(slot, showAll.GetVisualAncestors());
        var band = BandBelowFifthRow(window);
        Assert.True(Math.Abs(band) <= 1, string.Create(CultureInfo.InvariantCulture, $"{band} px between the fifth row and the From elsewhere line"));
    });
}
