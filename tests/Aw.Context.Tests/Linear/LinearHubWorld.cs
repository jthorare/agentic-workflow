using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Models;
using Aw.Context.Core.Results;
using Aw.Context.Features;
using Aw.Context.Infrastructure;
using Aw.Context.Infrastructure.Linear;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Linear;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Vectors;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// The Linear hub end to end for the tests of its stored side (spec 2026-11-04 §4): the real feed catalog, sync, SQLite record store and side-row writer over the stateful fake Linear, on a throwaway Sindri home.
/// The clock stands at 2026-01-02, a day after the fake's own, so its issues fall in the feed's Recent window. Nothing but the HTTP wire and the clock is faked.
/// </summary>
internal sealed class LinearHubWorld : IAsyncDisposable
{
    public const string KeyName = "LINEAR_HUB_KEY";
    public static readonly DateTimeOffset Now = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Directory.CreateTempSubdirectory("linear-hub-").FullName;
    private readonly HttpClient _fakeHttp = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });
    private readonly string _databasePath;

    private LinearHubWorld(FakeLinear linear, string profile)
    {
        Linear = linear;
        Clock = new FakeTimeProvider(Now);
        _databasePath = SettingsFromEnvironment.Read(name => name == "AW_SINDRI_HOME" ? _home : null).DatabasePath;
        var parts = new LinearSourceParts(new HttpClient(new Spy(this, new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })), name => name == "env:" + KeyName ? linear.Token : null, Clock, NullLogger<LinearClient>.Instance);
        Sources = new LinearSources(parts);
        Provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>))
            .AddSingleton(FixtureMap.Json)
            .AddSingleton<IIdGenerator>(new SequentialIdGenerator())
            .AddSingleton<IModelPort>(new FakeModelPort())
            .AddSingleton<Context.Core.Vectors.IChunkEmbedder>(new FakeChunkEmbedder())
            .AddSingleton<Context.Core.Vectors.IQueryEmbedder>(new FakeQueryEmbedder())
            .AddSingleton<Context.Core.Vectors.IVectorScan>(new FakeVectorScan())
            .AddContextCore()
            .AddContextFeatures()
            .AddContextInfrastructure(name => name == "AW_SINDRI_HOME" ? _home : null)
            .AddSingleton(Sources)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true });
        Scope = new ContextScope(
            JsonDocument.Parse(profile).RootElement.Clone(),
            [new ContextRepo(ContextRepoName.From("split-panes"), _home)],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));
    }

    public FakeLinear Linear { get; }

    /// <summary>Called with the body of every request the index sends to the fake Linear, before it is sent (a test uses it to move the clock mid-tick).</summary>
    public Action<string>? OnRequest { get; set; }

    public FakeTimeProvider Clock { get; }

    public LinearSources Sources { get; }

    public ServiceProvider Provider { get; }

    public ContextScope Scope { get; }

    /// <summary>Starts a fake Linear with the team 'ENG' and whatever the seed adds, and a world whose profile reads that team.</summary>
    public static async Task<LinearHubWorld> Start(Action<FakeLinearSeed> seed)
    {
        var linear = await FakeLinear.Start(seed);
        var profile = new JsonObject
        {
            ["sources"] = new JsonObject { ["linear"] = new JsonObject { ["url"] = linear.Url.ToString(), ["teams"] = new JsonArray("ENG"), ["token"] = "env:" + KeyName } },
        }.ToJsonString();
        return new LinearHubWorld(linear, profile);
    }

    /// <summary>One sync tick of every feed; the Linear issue feed's row.</summary>
    public async Task<SourceSync> Tick()
    {
        await using var scope = Provider.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<IContextEngine>();
        var ticked = await engine.Tick(Scope, TestContext.Current.CancellationToken);
        Assert.True(ticked.IsSuccess, ticked.IsError ? ticked.Error.Message : "");
        return ticked.Value.Sources.Single(static s => s.Source == LinearSourceIds.Issues);
    }

    /// <summary>Sends a GraphQL mutation to the fake with the write key, the way Sindri's writer will.</summary>
    public async Task Mutate(string query, JsonObject variables)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Linear.Url) { Content = new StringContent(new JsonObject { ["query"] = query, ["variables"] = variables }.ToJsonString(), Encoding.UTF8, "application/json") };
        _ = request.Headers.TryAddWithoutValidation("Authorization", Linear.Token);
        using var response = await _fakeHttp.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode && !body.Contains("\"errors\"", StringComparison.Ordinal), body);
    }

    /// <summary>Updates an issue in the fake as the owner would from Sindri.</summary>
    public Task Update(string issueId, JsonObject input) =>
        Mutate("mutation M($id: String!, $input: IssueUpdateInput!) { issueUpdate(id: $id, input: $input) { success } }", new JsonObject { ["id"] = issueId, ["input"] = input });

    /// <summary>Runs one statement on the context database (a row planted or removed to set a test up).</summary>
    public void Sql(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    /// <summary>The first column of the first row of a query on the context database.</summary>
    public long Count(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The first column of the first row of a query, as text (null for NULL or no row).</summary>
    public string? Text(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar() is { } value and not DBNull ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    public async ValueTask DisposeAsync()
    {
        await Provider.DisposeAsync();
        await Linear.DisposeAsync();
        _fakeHttp.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_home, recursive: true);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private sealed class Spy(LinearHubWorld world, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (world.OnRequest is { } hook && request.Content is { } content)
            {
                hook(await content.ReadAsStringAsync(cancellationToken));
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
