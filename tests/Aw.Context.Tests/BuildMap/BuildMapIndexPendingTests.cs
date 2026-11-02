using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Symbols;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Records;
using Aw.Context.Testing.Symbols;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>Issue 1704: a draft built before the code index held its commit has unresolved symbol citations; a build that waited must not reuse it once the index has landed, and the draft says which index it read.</summary>
public sealed class BuildMapIndexPendingTests : IDisposable
{
    private const string DocText = "The pane is `SplitPane`.";
    private const string DocPath = "docs/panes.md";
    private const string ResolvedNote = "citations: 1 resolved, 0 unresolved (0 missing, 0 ambiguous)";
    private static readonly ContextRepoName _repo = MapWorld.Repos[0].Name;
    private readonly List<MapWorld> _worlds = [];

    public void Dispose() => _worlds.ForEach(w => w.Dispose());

    private static ContextScope Scope => MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);

    // A code index that knows the repo's SplitPane class but holds no index yet: Land(repo, commit) builds it.
    private static FakeCodeSymbolSource IndexNotBuilt() =>
        new([new FakeIndexedRepo(_repo, CodeIndexState.Missing, Commit: null, [new SymbolSite("src/ui/Split.tsx#SplitPane", "SplitPane", "function", "src/ui/Split.tsx", 1, 1, Signature: null, Exported: true)], [])]);

    private async Task<MapWorld> World(FakeCodeSymbolSource symbols)
    {
        var files = new Dictionary<string, string>(MapWorld.BuildFiles, StringComparer.Ordinal) { [DocPath] = DocText };
        var records = new FakeRecordStore();
        var row = RecordSamples.Write(RecordSamples.Record(RepoDocs.Key(_repo, DocPath).Value, FakeRepoReader.BlobId(DocText), RepoDocs.Id) with { Kind = RecordKind.Doc, Repo = _repo, Title = DocPath }, DocText);
        _ = await records.Commit(RecordSamples.Batch(row.Record.Source, 1, [row]), MapWorld.None);
        var world = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, files), records: records, symbols: symbols);
        _worlds.Add(world);
        return world;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_that_waited_does_not_reuse_a_draft_built_before_the_code_index_of_its_heads()
    {
        var ct = TestContext.Current.CancellationToken;
        var index = IndexNotBuilt();
        var world = await World(index);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        world.Store.SaveDraftHold = release.Task;

        var first = world.Map.BuildMap(progress: null, Scope, MapWorld.None).AsTask();
        await WaitUntil(() => world.Store.SaveDraftsWaiting == 1, ct);   // the first build has read the repo without an index and is not yet saved
        var parked = new Signal();
        var second = world.Map.BuildMap(parked, Scope, MapWorld.None).AsTask();
        await parked.Told.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, ct);   // the second build reports it is waiting at the gate, just before it waits
        index.Land(_repo, MapWorld.Head);
        release.SetResult();

        var firstReport = (await first.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, ct)).Value;
        var secondReport = (await second.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, ct)).Value;

        Assert.Equal(firstReport.Draft.Number + 1, secondReport.Draft.Number);   // a second draft was saved
        Assert.Contains(secondReport.Notes, n => string.Equals(n, ResolvedNote, StringComparison.Ordinal));
        Assert.DoesNotContain(secondReport.Notes, n => n.Contains("while this build waited", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_draft_built_before_the_code_index_records_that_its_citations_lacked_the_index()
    {
        var without = await (await World(IndexNotBuilt())).Map.BuildMap(progress: null, Scope, MapWorld.None);
        var index = IndexNotBuilt();
        index.Land(_repo, MapWorld.Head);
        var with = await (await World(index)).Map.BuildMap(progress: null, Scope, MapWorld.None);

        Assert.Null(Assert.Single(without.Value.Heads).IndexCommit);   // no index of that commit was used
        Assert.Equal(MapWorld.Head, Assert.Single(with.Value.Heads).IndexCommit);   // the commit of the index the citations were resolved against
    }

    private sealed class Signal : IProgress<string>
    {
        private readonly TaskCompletionSource _told = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Told => _told.Task;

        public void Report(string value) => _ = _told.TrySetResult();
    }

    private static async Task WaitUntil(Func<bool> condition, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), TimeProvider.System, limit.Token);
        }
    }
}
