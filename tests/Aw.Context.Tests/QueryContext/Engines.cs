using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.Sources;
using Aw.Context.Features;
using Aw.Context.Features.ApplyReviewFile;
using Aw.Context.Features.BuildMap;
using Aw.Context.Features.CheckMap;
using Aw.Context.Features.CheckRetention;
using Aw.Context.Features.CleanMap;
using Aw.Context.Features.EvalProbes;
using Aw.Context.Features.ExportMap;
using Aw.Context.Features.ExportReview;
using Aw.Context.Features.FindPath;
using Aw.Context.Features.GetCode;
using Aw.Context.Features.GetFileMap;
using Aw.Context.Features.GetHistory;
using Aw.Context.Features.GetLenses;
using Aw.Context.Features.GetProposals;
using Aw.Context.Features.GetSubgraph;
using Aw.Context.Features.ImportMap;
using Aw.Context.Features.ListNeighbors;
using Aw.Context.Features.PreviewMap;
using Aw.Context.Features.PreviewReviewFile;
using Aw.Context.Features.PromoteMap;
using Aw.Context.Features.ProposeFromScope;
using Aw.Context.Features.PurgeMap;
using Aw.Context.Features.QueryContext;
using Aw.Context.Features.RefreshDocs;
using Aw.Context.Features.ReviewHistory;
using Aw.Context.Features.ReviewMap;
using Aw.Context.Features.ReviewQueue;
using Aw.Context.Features.ShowMapStatus;
using Aw.Context.Features.ShowNode;
using Aw.Context.Features.SourcesStatus;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Code;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.History;
using Aw.Context.Testing.Linking;
using Aw.Context.Testing.Locks;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Review;
using Aw.Context.Tests.EvalProbes;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.QueryContext;

/// <summary>The facade over fakes, and the scopes the tests ask in.</summary>
internal static class Engines
{
    internal static readonly ContextRepo[] Repos = [new(ContextRepoName.From("split-panes"), "/repos/split-panes"), new(ContextRepoName.From("other"), "/repos/other")];

    internal static IContextEngine Over(FixedSourceCatalog catalog, FakeDocIndex? index = null, FakeTimeProvider? time = null, FakeIntentStore? intentStore = null)
    {
        var clock = time ?? new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));
        var scrubbers = new FakeScrubberFactory();
        var docs = index ?? new FakeDocIndex(clock);
        var store = new FakeGraphStore();
        var intents = intentStore ?? new FakeIntentStore();
        var graphs = new MapReader(store, intents);
        var repos = new FakeRepoReader(Repos[0], "0a1b2c3d4e5f60718293a4b5c6d7e8f901234567", new Dictionary<string, string>(StringComparer.Ordinal));
        return new ContextEngine(new QueryContextService(catalog, scrubbers, new SequentialIdGenerator(), clock, graphs, intents, repos),
            new ContextSources(new SourcesStatusService(docs, catalog, scrubbers, intents, clock), new RefreshDocsService(docs, scrubbers), new SyncSourcesService(catalog, scrubbers, graphs, intents, new FakeModelPort(), new SyncLocks(), clock), intents), scrubbers, Map(clock, store, scrubbers),
            new ContextGraph(new ShowNodeService(graphs, scrubbers), new ListNeighborsService(graphs, scrubbers), new FindPathService(graphs, scrubbers),
                new GetLensesService(graphs, new LensBasisReader(repos, intents, clock), scrubbers), new GetLensesSetService(graphs, new LensBasisReader(repos, intents, clock), scrubbers), new GetFileMapService(graphs, repos),
                new ContextCodeHistory(new GetCodeService(graphs, new LensBasisReader(repos, intents, clock), Lexical(repos, clock), scrubbers),
                    new GetHistoryService(graphs, new HistoryPipeline(new LensBasisReader(repos, intents, clock), intents, new FakeFileHistory(Repos[0], new HistoryWorld("0a1b2c3d4e5f60718293a4b5c6d7e8f901234567", [])), clock), scrubbers),
                    new GetSubgraphService(graphs, new LensBasisReader(repos, intents, clock), scrubbers))),
            new ContextEval(Probes(clock), new NoBacktest(), new NoMapQuality(), new NoPurge()),
            new Features.MapHeads.MapHeadsService(repos, new FakeDefaultBranch()));
    }

    /// <summary>The probe runner over no probe file (the query tests never run probes).</summary>
    // The code lens over an empty repo: nothing to find, but the engine facade is whole.
    private static CodeLexicalSearch Lexical(FakeRepoReader repos, TimeProvider clock)
    {
        var search = new FakeCodeSearch(Repos[0], "0a1b2c3d4e5f60718293a4b5c6d7e8f901234567", new Dictionary<string, string>(StringComparer.Ordinal));
        var candidates = new LexicalCandidates(search, repos);
        return new CodeLexicalSearch(repos, search, new LexicalTestsRule(candidates), new LexicalSchemaRule(candidates), clock);
    }

    private static EvalProbesService Probes(TimeProvider clock) => new(new NoProbes(), new NoProbes(), clock);

    /// <summary>The map part over its fakes (S-3a); the query tests never touch it.</summary>
    private static ContextMap Map(TimeProvider clock, FakeGraphStore store, FakeScrubberFactory scrubbers)
    {
        var exports = new FakeExportStore(clock);
        var reader = new FakeMapFileReader();
        var admin = new MapAdmin(new PreviewMapService(reader), new ImportMapService(reader, store, clock), new ExportMapService(store, exports, clock),
            new ShowMapStatusService(store), new CheckMapService(store), new CheckRetentionService(store, exports, clock, new Features.RunBacktest.UnconfiguredBacktest()), new PurgeMapService(store, exports, clock));
        var review = new MapReview(new ReviewQueueService(store, scrubbers), new ReviewMapService(store, new FakeCurrentUser(), clock), new ReviewHistoryService(store), new ExportReviewService(store, scrubbers, new FakeReviewFileStore(), clock),
            new PreviewReviewFileService(new FakeReviewFileStore()), new ApplyReviewFileService(store, new FakeReviewFileStore(), new FakeCurrentUser(), clock), new PromoteMapService(store, clock));
        return new ContextMap(admin, review, new MapProposals(Build(clock, store), new ProposeFromScopeService(store, store, clock), new GetProposalsService(store, store, scrubbers)), new CleanMapService(store, scrubbers, new FakeModelPort(), new FakeCurrentUser(), clock));
    }

    /// <summary>The build slice over empty fakes (the query tests never build).</summary>
    private static BuildMapService Build(TimeProvider clock, FakeGraphStore store) =>
        new(new FakeRepoReader(Repos[0], "0a1b2c3d4e5f60718293a4b5c6d7e8f901234567", new Dictionary<string, string>(StringComparer.Ordinal)), new FakeClusterSource(), new FakeLabeler(), new FakeHeavyJobLock(), store, clock);

    internal static IContextEngine Over(params IContextSource[] sources) => Over(new FixedSourceCatalog(sources));

    /// <summary>
    /// Runs <paramref name="query"/> while advancing <paramref name="clock"/> a second at a time, so a deadline on the
    /// query's clock passes without real waiting. A query still running after that fails the test within 10 s of real
    /// time instead of hanging the lane.
    /// </summary>
    internal static async Task<T> Advancing<T>(FakeTimeProvider clock, Func<Task<T>> query)
    {
        var running = query();
        for (var i = 0; i < 30 && !running.IsCompleted; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        return await running.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken);
    }

    internal static ContextScope Human(string? settingsJson = null) =>
        new(Settings(settingsJson), Repos, new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    internal static ContextScope Session(string? repo, string? settingsJson = null) =>
        new(Settings(settingsJson), Repos, new ContextCaller(ContextCallerKind.Session, repo is null ? null : ContextRepoName.From(repo), ContextClient.Http));

    private static JsonElement? Settings(string? json) => json is null ? null : JsonDocument.Parse(json).RootElement.Clone();
}
