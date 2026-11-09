using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Features.SyncTodoNote;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>The note sync over the todo harness's in-memory ledger and views, a fake note folder and a fake disk.</summary>
internal sealed class SyncRig
{
    internal const string Vault = "notes";
    internal const string NoteName = "Todo.md";
    internal const string Path = "/vault/Todo.md";

    private SyncRig(TodoHarness harness)
    {
        Harness = harness;
        Sync = new TodoNoteSync(new SyncDeps(
            Files, Locator, Disk, harness.Slice.Store, harness.Slice.Held, harness.Slice.Events, harness.Views.Views,
            new TodoHolds(harness.Slice.Store, harness.Slice.Events), harness.Picks, new PassThroughScrubber(), harness.Slice.Clock, harness.Slice.Ids, NullLogger.Instance));
    }

    internal TodoHarness Harness { get; }

    internal FakeTodoNoteFiles Files { get; } = new();

    internal FakeNoteLocator Locator { get; } = new(new NotePlace(Vault, NoteName, "/vault"));

    internal FakeNoteDisk Disk { get; } = new();

    internal TodoNoteSync Sync { get; }

    /// <summary>A rig with the lease held.</summary>
    internal static async Task<SyncRig> Start()
    {
        var harness = new TodoHarness();
        await harness.Lease();
        return new SyncRig(harness);
    }

    /// <summary>The note's text now, or null when it is missing.</summary>
    internal string? Note => Files.Files.TryGetValue(Path, out var text) ? text : null;

    internal Task<TodoNoteStatus> Pass() => Sync.Pass(TestContext.Current.CancellationToken).AsTask();

    /// <summary>The recorded events of a type, oldest first.</summary>
    internal async Task<IReadOnlyList<(long Sequence, string Type, string Key, object Payload)>> Events(params string[] types)
    {
        var all = (await Harness.Slice.Store.ReadAfter(0, 10_000, TestContext.Current.CancellationToken)).Value;
        return [.. all.Where(e => types.Contains(e.Envelope.Type, StringComparer.Ordinal)).Select(e => (e.Sequence, e.Envelope.Type, e.IdempotencyKey, (object)e.Envelope.Payload))];
    }

    internal async Task<IReadOnlyList<T>> Payloads<T>(string type)
    {
        var all = (await Harness.Slice.Store.ReadAfter(0, 10_000, TestContext.Current.CancellationToken)).Value;
        return [.. all.Where(e => e.Envelope.Type == type).Select(Harness.Payload<T>)];
    }

    /// <summary>A rig with <paramref name="count"/> seeded open todos that one pass has already written to the note ("todo 000", "todo 001", ...).</summary>
    internal static async Task<(SyncRig Rig, IReadOnlyList<TodoId> Ids)> WithTodos(int count)
    {
        var rig = await Start();
        var ids = Enumerable.Range(0, count).Select(i => TodoId.From("td_01jc5a8xq0n4w7ms2t9r3vb" + "kmnpqrstvwxyz"[i % 13] + "0" + "0123456789abcdefghjk"[i])).ToList();
        foreach (var id in ids)
        {
            await rig.Harness.Seed(id, "todo " + id.Value[^3..]);
        }

        _ = await rig.Pass();
        return (rig, ids);
    }

    /// <summary>The note's lines with the task lines numbered <paramref name="from"/> up to (not including) <paramref name="to"/> removed (line 0 is the heading, 1 the blank, tasks start at 2).</summary>
    internal static string WithoutLines(string note, int from, int to) =>
        string.Join('\n', note.Split('\n').Where((_, i) => i < from || i >= to));

    private readonly HashSet<TodoId> _folded = [];

    /// <summary>Shows the todos the sync added to the ledger in the todo view, as the projector would.</summary>
    internal async Task FoldAdded()
    {
        foreach (var added in await Payloads<TodoAdded>(EventTypes.TodoAdded))
        {
            if (_folded.Add(added.Id))
            {
                await Harness.Fold(EventTypes.TodoAdded, added);
            }
        }
    }

    /// <summary>The id of a note line, by its text.</summary>
    internal static TodoId? IdOnLine(string note, string text) =>
        NoteParser.Parse(note).Todos.FirstOrDefault(t => t.Text == text)?.Id;

    internal sealed class FakeNoteLocator(NotePlace? place) : INoteLocator
    {
        internal NotePlace? Place { get; set; } = place;

        /// <summary>Gets or sets an error every Locate answers instead of the place.</summary>
        internal Error? Error { get; set; }

        public ValueTask<Result<NotePlace?>> Locate(CancellationToken cancellationToken) =>
            ValueTask.FromResult(Error is { } error ? (Result<NotePlace?>)error : Results.Success(Place));
    }

    internal sealed class FakeNoteDisk : ITodoNoteDisk
    {
        internal bool CanSwap { get; set; } = true;

        public bool VaultExists(string vaultRoot) => true;

        public NoteFileFacts Inspect(string vaultRoot, string note) => new(Exists: false, IsSymlink: false, LinkCount: 1, Text: null, Unreadable: null);

        public NoteFolderFacts Probe(string vaultRoot, string note) => new(Writable: true, CanSwap);
    }
}
