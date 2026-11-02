using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Models;
using Aw.Context.Features;
using Aw.Context.Infrastructure;
using Aw.Context.Infrastructure.Linear;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Vectors;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// Linear through the owned index end to end (owned-index spec §5.5, §5.8, §4.4): the real feed catalog, sync, SQLite record store and lexical leg over a fake Linear,
/// the profile read by SettingsReader, then a context query. Nothing but the HTTP wire and the clock is faked.
/// </summary>
[Trait("Lane", "integration")]
public sealed class LinearIndexEndToEndTests : IDisposable
{
    private const string Key = "LINEAR_E2E_KEY";
    private const string Profile = """{"sources":{"linear":{"teams":["SPL"],"token":"env:LINEAR_E2E_KEY"}}}""";
    private static readonly DateTimeOffset _now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Directory.CreateTempSubdirectory("linear-e2e-").FullName;
    private readonly ServiceProvider _provider;
    private readonly FakeLinearIssues _linear;

    public LinearIndexEndToEndTests()
    {
        _linear = new FakeLinearIssues([Issue("a1", "SPL", "Rotate the webhook signing secret"), Issue("a2", "SPL", "Pane resize jitters"), Issue("z9", "OTHR", "Rotate the webhook signing secret elsewhere")]);
        var parts = new LinearSourceParts(new HttpClient(_linear), name => name == "env:" + Key ? "lin_key" : null, new FakeTimeProvider(_now), NullLogger<LinearClient>.Instance);
        _provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(_now))
            .AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>))
            .AddSingleton(FixtureMap.Json)
            .AddSingleton<IIdGenerator>(new SequentialIdGenerator())
            .AddSingleton<IModelPort>(new FakeModelPort())
            .AddSingleton<Context.Core.Vectors.IChunkEmbedder>(new FakeChunkEmbedder())   // the host (Sindri) supplies the embedders and the vector scan
            .AddSingleton<Context.Core.Vectors.IQueryEmbedder>(new FakeQueryEmbedder())
            .AddSingleton<Context.Core.Vectors.IVectorScan>(new FakeVectorScan())
            .AddContextCore()
            .AddContextFeatures()
            .AddContextInfrastructure(name => name == "AW_SINDRI_HOME" ? _home : null)
            .AddSingleton(new LinearSources(parts))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true });
    }

    private static JsonObject Issue(string id, string team, string title)
    {
        var node = FakeLinearIssues.Node(id, _now.AddDays(-3));
        node["title"] = title;
        node["description"] = "Details of " + title;
        node["identifier"] = team + "-" + id;
        node["url"] = $"https://linear.app/split/issue/{team}-{id}/slug";
        node["team"] = new JsonObject { ["id"] = "team-" + team, ["key"] = team, ["name"] = team };
        return node;
    }

    private static ContextScope Scope(string repoPath) => new(
        JsonDocument.Parse(Profile).RootElement.Clone(),
        [new ContextRepo(ContextRepoName.From("split-panes"), repoPath)],
        new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    public void Dispose()
    {
        _provider.Dispose();
        Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public async Task A_tick_stores_the_teams_issues_and_a_query_finds_one_by_title_with_its_linear_permalink_and_never_another_teams()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var services = _provider.CreateAsyncScope();
        var engine = services.ServiceProvider.GetRequiredService<IContextEngine>();
        var scope = Scope(_home);

        var ticked = await engine.Tick(scope, ct);
        var progress = await engine.Progress(scope, ct);
        var answer = await engine.Query(new ContextQuery("webhook signing secret"), scope, ct);

        Assert.True(ticked.IsSuccess, ticked.IsError ? ticked.Error.Message : "");
        var linear = Assert.Single(ticked.Value.Sources, s => s.Source.Value == "linear");
        Assert.Null(linear.Code);
        Assert.True(Assert.Single(progress.Value, p => p.Source.Value == "linear").Usable);
        Assert.True(answer.IsSuccess, answer.IsError ? answer.Error.Message : "");
        var cites = answer.Value.Cites.Values.Where(c => c.Key.Value.StartsWith("linear:", StringComparison.Ordinal)).ToList();
        var hit = Assert.Single(cites);
        Assert.Equal("linear:issue/a1", hit.Key.Value);
        Assert.Equal("https://linear.app/split/issue/SPL-a1/slug", hit.Permalink?.ToString());
    }
}
