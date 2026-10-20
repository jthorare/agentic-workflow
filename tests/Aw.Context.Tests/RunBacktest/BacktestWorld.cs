using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Facade;
using Aw.Context.Core.History;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Locks;
using Aw.Context.Core.Map;
using Aw.Context.Core.Models;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Context.Core.Trust;
using Aw.Context.Features;
using Aw.Context.Infrastructure.Eval;
using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fakes;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Linking;
using Aw.Context.Testing.Locks;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Review;
using Aw.Context.Tests.EvalProbes;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.Onyx;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.RunBacktest;

/// <summary>
/// The backtest over the split-panes fixture repo (real git, real merge history), the fake Onyx server (the briefs) and fakes for the rest, composed the way
/// Sindri composes it (AddContextFeatures). The clock decides when the authored map is imported, which decides which cuts it reaches.
/// </summary>
internal sealed class BacktestWorld : IAsyncDisposable
{
    public const string MapPath = "/maps/product-map.yaml";
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private GrownCorpus? _grown;
    private readonly string _home;

    private BacktestWorld(FixtureRepo repo, (FakeOnyxServer Onyx, OnyxSourceHarness Harness) onyx, FakeTimeProvider clock, (Func<IRepoReader, IRepoReader>? Wrap, string? HomeUnder) options)
    {
        _home = Directory.CreateDirectory(Path.Combine(options.HomeUnder ?? Path.GetTempPath(), "aw-backtest-home-" + Path.GetRandomFileName())).FullName;
        Repo = repo;
        (Onyx, Harness) = onyx;
        Clock = clock;
        Settings = new ContextStoreSettings(Path.Combine(_home, "context"));
        var git = new GitCli();
        var catalog = new FixedSourceCatalog(Harness.Source);
        Briefs = new FlakyBriefSource(new OnyxBriefSource(catalog));
        Reader.Put(MapPath, Maps.Fixture(), FixtureMap.Yaml);
        Profile = [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), repo.Path)];
        _provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton<IGraphStore>(Store)
            .AddSingleton<IProposalRuns>(Store)
            .AddSingleton<IExportStore>(new FakeExportStore(Clock))
            .AddSingleton<IMapFileReader>(Reader)
            .AddSingleton<IScrubberFactory>(new RedactionScrubberFactory(Clock))
            .AddSingleton<ISourceCatalog>(catalog)
            .AddSingleton<IDocIndex>(new FakeDocIndex(Clock))
            .AddSingleton<IIdGenerator>(new SequentialIdGenerator())
            .AddSingleton<IProbeFileReader>(new NoProbes())
            .AddSingleton<IProbeReportStore>(new NoProbes())
            .AddSingleton(options.Wrap?.Invoke(new GitRepoReader(git)) ?? new GitRepoReader(git))
            .AddSingleton<Context.Core.Code.ICodeSearch>(new Testing.Code.FakeCodeSearch(new ContextRepo(ContextRepoName.From("none"), "/none"), "0000000", new Dictionary<string, string>(StringComparer.Ordinal)))   // the backtest never asks the code lens
            .AddSingleton<IIntentStore>(Intents)
            .AddSingleton<IModelPort>(Model)
            .AddSingleton<IClusterSource>(Clusters)
            .AddSingleton<ILabeler>(new FakeLabeler())
            .AddSingleton<IHeavyJobLock>(HeavyLock)
            .AddSingleton<IReviewFileStore>(new FakeReviewFileStore())
            .AddSingleton<ICurrentUser>(new FakeCurrentUser())
            .AddSingleton<IMergeHistory>(new GitMergeHistory(git))
            .AddSingleton<IFileHistory>(new GitFileHistory(git))
            .AddSingleton<IBriefSource>(Briefs).AddSingleton<IDefaultBranch>(new FakeDefaultBranch())
            .AddSingleton<IAsOfDocs>(new AsOfDocsFactory(git, Clock))
            .AddSingleton<IEvalStore>(new FileEvalStore(Settings))
            .AddContextFeatures()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true });
        _scope = _provider.CreateScope();
    }

    public FixtureRepo Repo { get; }

    public FlakyBriefSource Briefs { get; }

    public FakeOnyxServer Onyx { get; }

    public OnyxSourceHarness Harness { get; }

    public FakeTimeProvider Clock { get; }

    public ContextStoreSettings Settings { get; }

    public FakeGraphStore Store { get; } = new();

    public FakeMapFileReader Reader { get; } = new();

    public FakeIntentStore Intents { get; } = new();

    public FakeModelPort Model { get; } = new();

    public FakeClusterSource Clusters { get; } = new();

    public FakeHeavyJobLock HeavyLock { get; } = new();

    public IReadOnlyList<ContextRepo> Profile { get; }

    public IServiceProvider Services => _scope.ServiceProvider;

    public IContextEngine Engine => _scope.ServiceProvider.GetRequiredService<IContextEngine>();

    public IContextMap Map => _scope.ServiceProvider.GetRequiredService<IContextMap>();

    public static DateTimeOffset BeforeAllCuts { get; } = new(2026, 6, 30, 9, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset AfterAllCuts { get; } = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Starts the world with its clock at <paramref name="now"/>.</summary>
    /// <param name="now">The time the authored map is imported at when <see cref="ImportMap"/> runs.</param>
    /// <param name="onyx">Options for the fake Onyx server.</param>
    /// <param name="wrap">Wraps the git repo reader (to make a read fail).</param>
    /// <param name="homeUnder">A directory to make the state home in (inside a git worktree, to refuse writes with 005), or null for the temp directory.</param>
    /// <returns>The world.</returns>
    public static async Task<BacktestWorld> Start(DateTimeOffset now, FakeServerOptions? onyx = null, Func<IRepoReader, IRepoReader>? wrap = null, string? homeUnder = null)
    {
        var repo = FixtureRepo.Build();
        return await Start(repo, now, onyx, (wrap, homeUnder)).ConfigureAwait(false);
    }

    /// <summary>Starts the world over the grown synthetic corpus (32 pull requests, SPL-201 and up) and the fake Onyx serving its issues.</summary>
    /// <param name="now">The clock.</param>
    /// <param name="grown">The corpus; the world owns it and disposes it.</param>
    /// <returns>The world.</returns>
    public static async Task<BacktestWorld> Start(DateTimeOffset now, GrownCorpus grown)
    {
        ArgumentNullException.ThrowIfNull(grown);
        var world = await Start(grown.Repo, now, new FakeServerOptions { OnyxDocuments = grown.Issues }, (null, null)).ConfigureAwait(false);
        world._grown = grown;
        return world;
    }

    private static async Task<BacktestWorld> Start(FixtureRepo repo, DateTimeOffset now, FakeServerOptions? onyx, (Func<IRepoReader, IRepoReader>? Wrap, string? HomeUnder) options)
    {
        var server = await FakeOnyxServer.Start(onyx).ConfigureAwait(false);
        var harness = new OnyxSourceHarness(server.Url, server.Token);
        return new BacktestWorld(repo, (server, harness), new FakeTimeProvider(now), options);
    }

    /// <summary>The human's scope: the fixture repo, the fake Onyx and team SPL, plus any extra 'context:' keys.</summary>
    /// <param name="extra">Extra JSON members for the section (a leading comma is added), or null.</param>
    /// <param name="onyx">False to leave the onyx source out of the profile.</param>
    /// <returns>The scope.</returns>
    public ContextScope Human(string? extra = null, bool onyx = true)
    {
        var sources = onyx
            ? "{\"onyx\":{\"url\":\"" + Onyx.Url + "\",\"token\":\"" + OnyxSourceHarness.Pointer + "\",\"version\":\"v4.9.0\"},\"linear\":{\"teams\":[\"SPL\"]}}"
            : """{"linear":{"teams":["SPL"]}}""";
        return Scope(ContextCallerKind.Human, extra, sources);
    }

    public ContextScope Session() => Scope(ContextCallerKind.Session, extra: null, sources: """{"linear":{"teams":["SPL"]}}""");

    /// <summary>Imports the authored fixture map at the clock's time.</summary>
    /// <returns>The task.</returns>
    /// <param name="commit">The commit the map says it was authored at, or null.</param>
    public async Task ImportMap(string? commit = null)
    {
        const string path = "/maps/at-commit.yaml";
        var yaml = commit is null ? FixtureMap.Yaml : "commit: " + commit + "\n" + FixtureMap.Yaml;
        Reader.Put(path, Maps.Parse(yaml), yaml);
        var human = Human();
        var preview = await Map.PreviewMap(path, human, TestContext.Current.CancellationToken);
        var imported = await Map.ImportMap(path, preview.Value.Hash, human, TestContext.Current.CancellationToken);
        Assert.True(imported.IsSuccess, imported.IsError ? imported.Error.Message : null);
    }

    /// <summary>Runs the backtest.</summary>
    /// <param name="arms">The arms (default: all).</param>
    /// <param name="scope">The scope (default: the human's).</param>
    /// <param name="configure">Changes the request (window, limit).</param>
    /// <returns>The result.</returns>
    public Task<Kernel.Result<BacktestRun>> Run(string arms = "all", ContextScope? scope = null, Func<BacktestRequest, BacktestRequest>? configure = null) =>
        RunWith(TestContext.Current.CancellationToken, arms, scope, configure);

    /// <summary>Runs the backtest under <paramref name="cancellationToken"/>.</summary>
    /// <param name="cancellationToken">The run's token.</param>
    /// <param name="arms">The arms.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="configure">Changes the request.</param>
    /// <returns>The result.</returns>
    public async Task<Kernel.Result<BacktestRun>> RunWith(CancellationToken cancellationToken, string arms = "all", ContextScope? scope = null, Func<BacktestRequest, BacktestRequest>? configure = null)
    {
        var request = new BacktestRequest(scope ?? Human(), Arms.Parse(arms).Value);
        return await Engine.EvalBacktest(configure?.Invoke(request) ?? request, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _scope.Dispose();
        await _provider.DisposeAsync();
        Harness.Dispose();
        await Onyx.DisposeAsync();
        if (_grown is null)
        {
            Repo.Dispose();
        }
        else
        {
            _grown.Dispose();
        }

        Directory.Delete(_home, recursive: true);
    }

    private ContextScope Scope(ContextCallerKind kind, string? extra, string sources)
    {
        var section = "{\"repos\":[\"" + FixtureMap.Repo + "\"],\"sources\":" + sources + ",\"sessions\":{\"enabled\":true}" + (extra is null ? "" : "," + extra) + "}";
        using var json = System.Text.Json.JsonDocument.Parse(section);
        var caller = kind == ContextCallerKind.Session
            ? new ContextCaller(ContextCallerKind.Session, ContextRepoName.From(FixtureMap.Repo), ContextClient.Http)
            : new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli);
        return new ContextScope(json.RootElement.Clone(), Profile, caller);
    }
}
