using Aw.Context.Core.Code;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Facade;
using Aw.Context.Core.History;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Locks;
using Aw.Context.Core.Map;
using Aw.Context.Core.Models;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Context.Features;
using Aw.Context.Testing.Code;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.History;
using Aw.Context.Testing.Linking;
using Aw.Context.Testing.Locks;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Review;
using Aw.Context.Tests.EvalProbes;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

/// <summary>
/// The map facade over the fakes, composed the way Sindri composes it (AddContextFeatures), with the fixture map placed at
/// <see cref="MapPath"/>. ValidateOnBuild is off: AddContextFeatures also registers the query slices, whose ports
/// (doc index, sources) these tests don't need; resolving IContextMap still checks what the map needs.
/// </summary>
internal sealed class MapWorld : IDisposable
{
    /// <summary>The tabs map (map-tabs.json): a conflict, a suggested file link and an orphaned override.</summary>
    public static string TabsMap => MapTabsRepo.MapJson;

    public const string MapPath = "/maps/product-map.yaml";

    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    /// <summary>The commit the fake repo is at.</summary>
    public const string Head = "0a1b2c3d4e5f60718293a4b5c6d7e8f901234567";

    /// <summary>The files of the fake repo at <see cref="Head"/>: the extractor finds two commands, a keybinding, a component and a setting in them; the rest is noise or private.</summary>
    public static IReadOnlyDictionary<string, string> BuildFiles { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["src/commands/ids.ts"] = "export type CommandId = \"split-vertical\" | \"close-pane\";",
        ["src/commands/registry.ts"] = "register({ id: \"split-vertical\", title: \"Split vertically\" });",
        ["src/keys.ts"] = "export const keys: Record<string, CommandId> = { \"ctrl+shift+d\": \"split-vertical\" };",
        ["src/ui/Split.tsx"] = "export function SplitPane() {}",
        ["config/settings.json"] = "{\"paneResizeStep\": 8}",
        ["config/secrets/settings.json"] = "{\"apiToken\": \"hunter2\"}",
        [".env"] = "TOKEN=hunter2",
        ["README.md"] = "# Split panes",
    };

    public MapWorld(IRepoReader? repoReader = null, IContextSource? intent = null, ICodeSearch? codeSearch = null, IFileHistory? history = null)
    {
        Sources = new FixedSourceCatalog(intent is null ? [Docs] : [Docs, intent]);
        Exports = new FakeExportStore(Clock);
        Reader.Put(MapPath, Maps.Fixture(), FixtureMap.Yaml);
        _provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton<IGraphStore>(Store)
            .AddSingleton<IProposalRuns>(Store)
            .AddSingleton<IExportStore>(Exports)
            .AddSingleton<IMapFileReader>(Reader)
            .AddSingleton<IScrubberFactory>(new FakeScrubberFactory())
            .AddSingleton<ISourceCatalog>(Sources)
            .AddSingleton<IDocIndex>(new FakeDocIndex(Clock))
            .AddSingleton<IIdGenerator>(new SequentialIdGenerator())
            .AddSingleton<IProbeFileReader>(new NoProbes())
            .AddSingleton<IProbeReportStore>(new NoProbes())
            .AddSingleton(repoReader ?? new FakeRepoReader(Repos[0], Head, BuildFiles))
            .AddSingleton<IDefaultBranch>(DefaultBranch)
            .AddSingleton(codeSearch ?? new FakeCodeSearch(Repos[0], Head, BuildFiles))
            .AddSingleton<IIntentStore>(Intents)
            .AddSingleton(history ?? new FakeFileHistory(Repos[0], new HistoryWorld(Head, [])))
            .AddSingleton<IModelPort>(Model)
            .AddSingleton<IClusterSource>(Clusters)
            .AddSingleton<ILabeler>(Labeler)
            .AddSingleton<IHeavyJobLock>(HeavyLock)
            .AddSingleton<IReviewFileStore>(ReviewFiles)
            .AddSingleton<ICurrentUser>(new FakeCurrentUser())
            .AddContextFeatures()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true });
        _scope = _provider.CreateScope();
    }

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    public FakeGraphStore Store { get; } = new();

    public FakeDefaultBranch DefaultBranch { get; } = new();

    public FakeIntentStore Intents { get; } = new();

    public FakeModelPort Model { get; } = new();

    public FakeExportStore Exports { get; }

    public FakeClusterSource Clusters { get; } = new();

    public FakeLabeler Labeler { get; } = new();

    public FakeHeavyJobLock HeavyLock { get; } = new();

    public FakeReviewFileStore ReviewFiles { get; } = new();

    public FakeMapFileReader Reader { get; } = new();

    public IContextMap Map => _scope.ServiceProvider.GetRequiredService<IContextMap>();

    /// <summary>The whole facade (S-3b: graph reads and queries over the imported map).</summary>
    public IContextEngine Engine => _scope.ServiceProvider.GetRequiredService<IContextEngine>();

    /// <summary>The sources the queries search: repo-docs over the fixture's docs by default.</summary>
    public FakeContextSource Docs { get; } = FakeContextSource.FixtureDocs();

    public FixedSourceCatalog Sources { get; }

    public static IReadOnlyList<ContextRepo> Repos { get; } = [new(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes"), new(ContextRepoName.From("other"), "/work/other")];

    public static ContextScope Human { get; } = new(Settings: null, Repos, new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    /// <summary>A human whose call arrives through <paramref name="client"/> (the app is plain HTTP; the CLI, MCP and services are the others).</summary>
    public static ContextScope HumanVia(ContextClient client) => new(Settings: null, Repos, new ContextCaller(ContextCallerKind.Human, Repo: null, client));

    /// <summary>A human's scope with the given 'context:' section (JSON) over the repos, or over <paramref name="repos"/> when given.</summary>
    public static ContextScope HumanWith(string sectionJson, IReadOnlyList<ContextRepo>? repos = null)
    {
        using var section = System.Text.Json.JsonDocument.Parse(sectionJson);
        return new(section.RootElement.Clone(), repos ?? Repos, new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));
    }

    /// <summary>The section that maps only the fixture repo (the fake repo reader knows no other).</summary>
    public const string OnlyFixtureRepo = """{"repos":["aw-split-panes"]}""";

    public static ContextScope Session(string? repo, bool enabled = true)
    {
        using var section = System.Text.Json.JsonDocument.Parse(enabled ? """{"sessions":{"enabled":true}}""" : """{"sessions":{"enabled":false}}""");
        return new(section.RootElement.Clone(), Repos, new ContextCaller(ContextCallerKind.Session, repo is null ? null : ContextRepoName.From(repo), ContextClient.Http));
    }

    public static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>YAML from lines (one string per line).</summary>
    public static string Yaml(params string[] lines) => string.Join('\n', lines) + "\n";

    /// <summary>Imports a map written as YAML (placed at '/maps/other.yaml').</summary>
    public async Task<MapImported> ImportYaml(string yaml)
    {
        const string path = "/maps/other.yaml";
        Reader.Put(path, Maps.Parse(yaml), yaml);
        var preview = await Map.PreviewMap(path, Human, None);
        Assert.True(preview.IsSuccess, preview.IsError ? preview.Error.Message : null);
        var imported = await Map.ImportMap(path, preview.Value.Hash, Human, None);
        Assert.True(imported.IsSuccess, imported.IsError ? imported.Error.Message : null);
        return imported.Value;
    }

    /// <summary>Imports the fixture map with its own hash.</summary>
    public async Task<MapImported> ImportFixture()
    {
        var preview = await Map.PreviewMap(MapPath, Human, None);
        var imported = await Map.ImportMap(MapPath, preview.Value.Hash, Human, None);
        Assert.True(imported.IsSuccess, imported.IsError ? imported.Error.Message : null);
        return imported.Value;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
