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
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Todo panel's sync line on Home under the headless platform (todo spec §3.1, §3.4, §3.5): one frame test per state, quiet or warning, and which of the two blocks is the live region.</summary>
[Lane(TestLane.Integration)]
public sealed class TodoSyncLineFramesTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);   // Sunday 2026-10-11

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TodoSyncLineFramesTests).Assembly)
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

    private async Task<Window> Home(string note)
    {
        _server.TodoNoteAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, note);
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = 1000, Height = 800 };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.Normal && !shell.Todo.IsLoading && _server.TodoNoteCalls > 0);
        return window;
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

    private static TextBlock Block(Window window, string name) => window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == name);

    private static async Task Reads(Window window, string name, string text) => await Until(window, () => string.Equals(Block(window, name).Text, text, StringComparison.Ordinal));

    [Fact]
    public Task With_no_note_set_neither_block_says_anything_and_the_live_region_stays_in_the_tree() => OnUi(async () =>
    {
        var window = await Home(TodoNoteSetupSample.NoteIn("not set"));

        Assert.False(Block(window, "TodoSyncQuiet").IsEffectivelyVisible);
        Assert.Equal((string.Empty, string.Empty), (Block(window, "TodoSyncQuiet").Text, Block(window, "TodoSyncLive").Text));
        Assert.Equal(Avalonia.Automation.AutomationLiveSetting.Polite, Avalonia.Automation.AutomationProperties.GetLiveSetting(Block(window, "TodoSyncLive")));
    });

    [Fact]
    public Task A_syncing_note_shows_the_quiet_line_and_it_is_not_the_live_region() => OnUi(async () =>
    {
        var window = await Home(TodoNoteSetupSample.NoteIn("syncing"));
        await Reads(window, "TodoSyncQuiet", "Synced with Obsidian 09:58");

        Assert.True(Block(window, "TodoSyncQuiet").IsEffectivelyVisible);
        Assert.Equal(Avalonia.Automation.AutomationLiveSetting.Off, Avalonia.Automation.AutomationProperties.GetLiveSetting(Block(window, "TodoSyncQuiet")));
        Assert.Equal(Avalonia.Automation.AutomationLiveSetting.Polite, Avalonia.Automation.AutomationProperties.GetLiveSetting(Block(window, "TodoSyncLive")));
    });

    [Theory]
    [InlineData("waiting for import", "Waiting for import")]
    [InlineData("one way", "Obsidian → Sindri only: can't write to this disk")]
    [InlineData("note busy", "Obsidian note busy, trying again")]
    public Task A_warning_state_shows_its_words_in_the_warning_colour(string state, string words) => OnUi(async () =>
    {
        var window = await Home(TodoNoteSetupSample.NoteIn(state));
        await Reads(window, "TodoSyncLive", words);

        Assert.Equal(Application.Current!.FindResource("Brush.Attention"), Block(window, "TodoSyncLive").Foreground);   // why: the headless session starts the app
        Assert.Equal(string.Empty, Block(window, "TodoSyncQuiet").Text);
    });

    [Fact]
    public Task A_note_that_cannot_be_read_points_at_doctor() => OnUi(async () =>
    {
        var window = await Home(TodoNoteSetupSample.NoteIn("can't read", lastError: "Todo.md is missing"));

        await Reads(window, "TodoSyncLive", "Obsidian note: can't read. Run `sindri doctor` to see why.");
    });

    [Fact]
    public Task Lines_that_cannot_sync_and_a_hold_are_warnings() => OnUi(async () =>
    {
        var one = await Home(TodoNoteSetupSample.NoteIn("syncing", cantSync: [(14, "it has a markdown link")]));
        await Reads(one, "TodoSyncLive", "Line 14 can't sync: it has a markdown link");

        _server.TodoNoteAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, TodoNoteSetupSample.NoteIn("syncing", hold: 14));
        var held = await Home(TodoNoteSetupSample.NoteIn("syncing", hold: 14));
        await Reads(held, "TodoSyncLive", "Paused: the note changed 14 todos");
    });

    [Fact]
    public Task A_pick_in_its_window_counts_down_in_the_live_region_in_the_normal_colour() => OnUi(async () =>
    {
        var window = await Home(TodoNoteSetupSample.NoteIn("syncing", pickUndoUntil: "2026-10-11T12:00:06Z"));
        await Reads(window, "TodoSyncLive", "Saving your pick to Obsidian in 6 s");

        Assert.NotEqual(Application.Current!.FindResource("Brush.Attention"), Block(window, "TodoSyncLive").Foreground);   // why: the headless session starts the app
    });
}
