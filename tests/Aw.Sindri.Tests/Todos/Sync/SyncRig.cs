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
    internal string? Note => Files.Files.GetValueOrDefault(Path);

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
        return [.. all.Where(e => e.Envelope.Type == type).Select(e => Harness.Payload<T>(e))];
    }

    /// <summary>The id of a note line, by its text.</summary>
    internal static TodoId? IdOnLine(string note, string text) =>
        NoteParser.Parse(note).Todos.FirstOrDefault(t => t.Text == text)?.Id;

    internal sealed class FakeNoteLocator(NotePlace? place) : INoteLocator
    {
        internal NotePlace? Place { get; set; } = place;

        public ValueTask<Result<NotePlace?>> Locate(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Place));
    }

    internal sealed class FakeNoteDisk : ITodoNoteDisk
    {
        internal bool CanSwap { get; set; } = true;

        public bool VaultExists(string vaultRoot) => true;

        public NoteFileFacts Inspect(string vaultRoot, string note) => new(Exists: false, IsSymlink: false, LinkCount: 1, Text: null, Unreadable: null);

        public NoteFolderFacts Probe(string vaultRoot, string note) => new(Writable: true, CanSwap);
    }
}
