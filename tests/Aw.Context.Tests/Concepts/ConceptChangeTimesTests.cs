using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.History;
using Aw.Context.Core.Map;
using Aw.Context.Features.GetConcepts;
using Aw.Context.Testing.Extract;
using Aw.Context.Tests.GetHistory;
using Aw.Context.Tests.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.Concepts;

/// <summary>The concepts read never waits on git for lastChanged (#3760): a cold cache answers without it and fills in the background, a warm one fills it with no git read, a new HEAD refills.</summary>
public sealed class ConceptChangeTimesTests : IDisposable
{
    private const string NewHead = "1b2c3d4e5f60718293a4b5c6d7e8f90123456789";
    private static readonly DateTimeOffset _first = new(2026, 5, 4, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _second = new(2026, 11, 2, 16, 45, 0, TimeSpan.Zero);

    private readonly MovableHead _reader = new();
    private readonly ScriptedHistory _history;
    private readonly MapWorld _world;
    private int _answers;
    private bool _shallow;

    public ConceptChangeTimesTests()
    {
        // The first git read says src/shifts.ts changed at _first, the next ones at _second.
        _history = new ScriptedHistory(inner: null!, (_, query, _) => ValueTask.FromResult(Answer(query)));
        _world = new MapWorld(repoReader: _reader, history: _history);
    }

    public void Dispose() => _world.Dispose();

    private Result<FileHistory> Answer(FileHistoryQuery query)
    {
        var at = Interlocked.Increment(ref _answers) == 1 ? _first : _second;
        string[] files = [.. query.Paths.Where(p => p == "src/shifts.ts")];
        return new FileHistory(query.UpTo ?? MapWorld.Head, Shallow: _shallow, [new FileChange(query.UpTo ?? MapWorld.Head, at, "Touch shifts", 1, files)], Truncated: false);
    }

    private static string Yaml() => MapWorld.Yaml(
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        "  - {key: \"concept:scheduling\", label: \"Self scheduling\", attributes: {summary: \"People pick shifts.\", source: derived}}",
        "  - {key: \"concept:billing\", label: \"Billing\", attributes: {summary: \"Invoices.\", source: derived}}",
        "  - {key: \"feature:shifts\", label: \"Shifts\"}",
        "  - {key: \"feature:invoices\", label: \"Invoices\"}",
        "  - {key: \"code:src/shifts.ts\", label: \"shifts.ts\", provenance: deterministic}",
        "  - {key: \"code:src/invoices.ts\", label: \"invoices.ts\", provenance: deterministic}",
        "edges:",
        "  - {from: \"feature:shifts\", to: \"concept:scheduling\", type: belongs-to}",
        "  - {from: \"feature:invoices\", to: \"concept:billing\", type: belongs-to}",
        "  - {from: \"code:src/shifts.ts\", to: \"feature:shifts\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/shifts.ts\"}]}",
        "  - {from: \"code:src/invoices.ts\", to: \"feature:invoices\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/invoices.ts\"}]}");

    private async Task<DateTimeOffset?> Scheduling()
    {
        var view = (await _world.Engine.Concepts(new ConceptsQuery(MapRead.Current, new ConceptPage()), MapWorld.Human, MapWorld.None)).Value;
        return view.Concepts.Single(c => c.Id.Value == "concept:scheduling").LastChanged;
    }

    private async Task Filled()
    {
        _ = await _world.ImportYaml(Yaml());
        _ = await Scheduling();
        await _world.Service<ConceptChangeTimes>().WhenIdle();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cold_cache_returns_concepts_without_lastChanged_and_fills_in_the_background()
    {
        _ = await _world.ImportYaml(Yaml());

        var cold = await Scheduling();
        await _world.Service<ConceptChangeTimes>().WhenIdle();
        var filled = await Scheduling();

        Assert.Null(cold);
        Assert.Equal(_first, filled);
        _ = Assert.Single(_history.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_warm_cache_fills_lastChanged_with_no_git_read()
    {
        await Filled();
        var reads = _history.Calls.Count;

        var warm = await Scheduling();

        Assert.Equal(_first, warm);
        Assert.Equal(reads, _history.Calls.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_head_refills()
    {
        await Filled();
        _reader.Current = NewHead;
        _world.Clock.Advance(ConceptChangeTimes.HeadFor + TimeSpan.FromSeconds(1));   // #3837: HEAD is read again only once the cached one is this old

        var stale = await Scheduling();
        await _world.Service<ConceptChangeTimes>().WhenIdle();
        var refilled = await Scheduling();

        Assert.Null(stale);
        Assert.Equal(_second, refilled);
        Assert.Equal(2, _history.Calls.Count);
        Assert.Equal(NewHead, _history.Calls[1].Query.UpTo);
    }

    // #3837: in a shallow clone the oldest fetched commit stands in for every older change, so its times would make files look newer than they are.
    [Fact, Trait("Lane", "unit")]
    public async Task A_shallow_clone_leaves_lastChanged_out()
    {
        _shallow = true;
        await Filled();

        Assert.Null(await Scheduling());
    }

    // #3837: HEAD costs a git process start; a read within HeadFor of the last one uses the cached HEAD.
    [Fact, Trait("Lane", "unit")]
    public async Task Reads_close_together_read_head_once()
    {
        await Filled();
        var heads = _reader.HeadReads;

        _ = await Scheduling();
        _ = await Scheduling();

        Assert.Equal(heads, _reader.HeadReads);
    }

    /// <summary>The fixture repo reader with a HEAD a test moves.</summary>
    private sealed class MovableHead : IRepoReader
    {
        private readonly FakeRepoReader _inner = new(MapWorld.Repos[0], MapWorld.Head, MapWorld.BuildFiles);

        public string Current { get; set; } = MapWorld.Head;

        /// <summary>HEAD reads of the fixture repo. The scope's second repo has no checkout: its failed read is never cached, so it is left out of the count.</summary>
        public int HeadReads { get; private set; }

        public async ValueTask<Result<string>> Head(ContextRepo repo, CancellationToken cancellationToken)
        {
            var known = await _inner.Head(repo, cancellationToken);
            if (known.IsError)
            {
                return known;
            }

            HeadReads++;
            return Current;
        }

        public ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepo repo, string commit, CancellationToken cancellationToken) => _inner.ListFiles(repo, MapWorld.Head, cancellationToken);

        public ValueTask<Result<IReadOnlyDictionary<string, string>>> BlobIds(ContextRepo repo, string commit, IReadOnlyList<string> paths, CancellationToken cancellationToken) => _inner.BlobIds(repo, MapWorld.Head, paths, cancellationToken);

        public ValueTask<Result<string>> Read(ContextRepo repo, string commit, string path, CancellationToken cancellationToken) => _inner.Read(repo, MapWorld.Head, path, cancellationToken);

        public ValueTask<Result<RepoChanges>> Changes(ContextRepo repo, DiffRange range, CancellationToken cancellationToken) => _inner.Changes(repo, range, cancellationToken);
    }
}
