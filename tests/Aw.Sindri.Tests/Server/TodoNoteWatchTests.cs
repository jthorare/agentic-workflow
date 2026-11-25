using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Tests.Todos;
using Aw.Sindri.Tests.Todos.Sync;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.Server;

/// <summary>The todo note watch on the fake clock (todo spec §4.5 "Reading"): a pass at start and every 60 s, one on a file change, one after another writer's todo event, one when a pick's window closes; nothing without the lease.</summary>
public sealed partial class TodoNoteWatchTests
{
    private readonly TodoHarness _todos = new();
    private readonly FakeSync _sync = new();
    private readonly SyncRig.FakeNoteLocator _locator = new(new NotePlace("notes", "Todo.md", "/vault"));
    private readonly FakeFolders _folders = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SliceHarness Slice => _todos.Slice;

    [Fact, Trait("Lane", "unit")]
    public async Task A_pass_runs_at_start_and_every_60_seconds()
    {
        _ = await Slice.HoldLease();

        await using var run = await Start();
        Assert.Equal(1, _sync.Passes);

        await run.Next(TodoNoteWatch.Every);
        Assert.Equal(2, _sync.Passes);
        await run.Next(TodoNoteWatch.Every);
        Assert.Equal(3, _sync.Passes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_note_s_folder_is_watched_for_the_note_s_file_name_and_a_change_runs_a_pass()
    {
        _ = await Slice.HoldLease();

        await using var run = await Start();
        Assert.Equal([("/vault", "Todo.md")], _folders.Started);

        await run.Next(TimeSpan.Zero, _folders.Fire);

        Assert.Equal(2, _sync.Passes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_todo_event_from_the_app_runs_a_pass_and_one_from_obsidian_or_the_sync_does_not()
    {
        var grant = await Slice.HoldLease();

        await using var run = await Start();
        await Append(grant, "obsidian", 1);
        await Append(grant, "service:todo-note", 2);
        await Task.Delay(50, Token);
        Assert.Equal(1, _sync.Passes);

        await run.Next(TimeSpan.Zero, async () => await Append(grant, "human:ui", 3));

        Assert.Equal(2, _sync.Passes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_todo_event_whose_pass_and_next_wait_finish_before_the_step_still_counts()
    {
        var grant = await Slice.HoldLease();

        await using var run = await Start();
        await run.Next(TimeSpan.Zero, async () =>
        {
            var arms = run.Clock.Arms;
            await Append(grant, "human:ui", 1);
            await LoopDone(run, arms);
        });

        Assert.Equal(2, _sync.Passes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_change_whose_pass_and_next_wait_finish_before_the_step_still_counts()
    {
        _ = await Slice.HoldLease();

        await using var run = await Start();
        await run.Next(TimeSpan.Zero, async () =>
        {
            var arms = run.Clock.Arms;
            _folders.Fire();
            await LoopDone(run, arms);
        });

        Assert.Equal(2, _sync.Passes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pass_runs_when_a_pick_s_window_closes()
    {
        _ = await Slice.HoldLease();
        _todos.Picks.Remember(TodoHarness.First, new TodoPick(Slice.Clock.GetUtcNow(), new TodoRow(TodoHarness.First, "reply to Sam", Due: null, Links: [], Slice.Clock.GetUtcNow(), "human:ui", AddedVia: null, DoneAt: null, Removed: false, RemovedAt: null, RemovedBy: null, LastLineHash: null, Version: 1, "human:ui", LastVia: null, Conflict: null, PickedConflict: null, Sequence: 1), VersionAfter: 2));

        await using var run = await Start();
        Assert.Equal(1, _sync.Passes);

        await run.Next(TodoPickWindow.Span);

        Assert.Equal(2, _sync.Passes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_runs_without_the_lease()
    {
        await using var run = await Start();

        await run.Next(TimeSpan.FromSeconds(1), expectPass: false);
        await run.Next(TimeSpan.FromSeconds(1), expectPass: false);

        Assert.Equal(0, _sync.Passes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Removing_the_key_drops_the_folder_watch()
    {
        _ = await Slice.HoldLease();

        await using var run = await Start();
        Assert.Equal(0, _folders.Stopped);

        _locator.Place = null;
        await run.Next(TodoNoteWatch.Every);

        Assert.Equal(1, _folders.Stopped);
        _ = Assert.Single(_folders.Started);
    }

    /// <summary>Waits until the loop ran its second pass and armed the wait after it: the order a busy box gives when the loop beats the test to its step (#2449).</summary>
    private async Task LoopDone(Run run, int armsBefore)
    {
        for (var waited = 0; _sync.Passes < 2 || run.Clock.Arms <= armsBefore; waited += 2)
        {
            Assert.True(waited < 5000, "the loop didn't run its pass and arm its next wait within 5 s");
            await Task.Delay(2, Token);
        }
    }

    private async Task Append(Core.Lease.LeaseGrant grant, string by, long version)
    {
        var entry = Slice.Events.Entry(EventTypes.TodoDone, new TodoDone(TodoHarness.First, version, by), string.Create(CultureInfo.InvariantCulture, $"todo.done:{by}:{version}"));
        _ = (await Slice.Store.Append(entry, grant, Token)).Value;
        _signal.Pulse((await Slice.Store.Head(Token)).Value);
    }

    private readonly RecordingSignal _signal = new(new Microsoft.Extensions.Time.Testing.FakeTimeProvider());

    private async Task<Run> Start()
    {
        var clock = new SteppedClock(Slice.Clock);
        var watch = new TodoNoteWatch(new NotePasses(_sync, Slice.Callers), new NoteFolder(_locator, _folders), new TodoEventTail(Slice.Store, _signal), _todos.Picks, Slice.Held, clock, NullLogger<TodoNoteWatch>.Instance);
        await watch.StartAsync(Token);
        var run = new Run(watch, clock, _sync);
        await clock.Armed();
        return run;
    }

    private sealed class FakeSync : ITodoNoteSync
    {
        private int _passes;

        internal int Passes => Volatile.Read(ref _passes);

        public TodoNoteStatus Status => TodoNoteStatus.NotSet;

        public ValueTask<TodoNoteStatus> Pass(CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _passes);
            return ValueTask.FromResult(TodoNoteStatus.NotSet);
        }

        public ValueTask<Result<TodoNoteImport>> Import(TodoNoteImportRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeFolders : IFolderWatch
    {
        private Action? _changed;

        internal List<(string Folder, string File)> Started { get; } = [];

        internal int Stopped { get; private set; }

        internal void Fire() => _changed?.Invoke();

        public IDisposable Watch(string folder, string fileName, Action changed)
        {
            Started.Add((folder, fileName));
            _changed = changed;
            return new Handle(this);
        }

        private sealed class Handle(FakeFolders owner) : IDisposable
        {
            public void Dispose() => owner.Stopped++;
        }
    }
}
