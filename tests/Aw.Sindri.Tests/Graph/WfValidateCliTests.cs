using System.Text.Json;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Graph;

/// <summary>'sindri wf validate' in process against graph files on disk: exit 0 pass, 1 findings, 2 usage; it writes nothing under the home.</summary>
public sealed class WfValidateCliTests : IDisposable
{
    private const string TwoTriggers = """{ "version": 1, "name": "Two triggers", "nodes": [ { "id": "daily", "type": "schedule", "config": { "cron": "0 7 * * 1-5" } }, { "id": "hourly", "type": "schedule", "config": { "cron": "0 * * * *" } }, { "id": "tell", "type": "notify", "config": { "title": "Hi" } } ], "edges": [ { "from": "daily", "to": "tell" }, { "from": "hourly", "to": "tell" } ] }""";

    private const string Fine = """{ "version": 1, "name": "Fine", "nodes": [ { "id": "daily", "type": "schedule", "config": { "cron": "0 7 * * 1-5" } }, { "id": "tell", "type": "notify", "config": { "title": "Hi" } } ], "edges": [ { "from": "daily", "to": "tell" } ] }""";

    private readonly TempHome _work = new();
    private readonly TempHome _home = new();

    public void Dispose()
    {
        _work.Dispose();
        _home.Dispose();
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_graph_with_two_triggers_prints_the_finding_and_exits_1()
    {
        var run = await Run(Put("two.json", TwoTriggers));

        Assert.Equal(1, run.Exit);
        Assert.Contains("GRAPH-TRIGGER-001 node 'daily', 'hourly'", run.Stdout, StringComparison.Ordinal);
        Assert.Equal(string.Empty, run.Stderr);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_graph_that_passes_exits_0_and_json_carries_the_hash_and_no_findings()
    {
        var file = Put("fine.json", Fine);

        var text = await Run(file);
        var json = await Run(file, "--json");

        Assert.Equal(0, text.Exit);
        using var document = JsonDocument.Parse(json.Stdout);
        Assert.Equal(0, json.Exit);
        Assert.True(document.RootElement.GetProperty("passed").GetBoolean());
        Assert.StartsWith("sha256:", document.RootElement.GetProperty("hash").GetString(), StringComparison.Ordinal);
        Assert.Equal(0, document.RootElement.GetProperty("findings").GetArrayLength());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_file_or_no_file_is_a_usage_error_with_exit_2()
    {
        var none = await Run();
        var missing = await Run(Path.Combine(_work.Path, "missing.json"));

        Assert.Equal(2, none.Exit);
        Assert.Equal(2, missing.Exit);
        Assert.Contains("SND-CLI-003", none.Stderr + missing.Stderr, StringComparison.Ordinal);
    }

    private string Put(string name, string text)
    {
        var path = Path.Combine(_work.Path, name);
        File.WriteAllText(path, text);
        return path;
    }

    private async Task<CliRun> Run(params string[] args)
    {
        var collection = Composition.Compose(new ServiceCollection(), name => name == "AW_SINDRI_HOME" ? _home.Path : null, new FakeTimeProvider());
        _ = collection.AddSingleton<IGraphBlockCatalog>(new TestCatalog());
        _ = collection.AddSingleton<IGraphWorld>(new AnyWorld());
        var provider = collection.BuildServiceProvider();
        await using var disposeProvider = provider;
        await using var scope = provider.CreateAsyncScope();
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var output = new Output(stdout, stderr, scope.ServiceProvider.GetRequiredService<JsonSerializerOptions>());

        var exit = await WfValidateCommand.Run(args, scope.ServiceProvider, output, TestContext.Current.CancellationToken);

        Assert.Empty(Directory.EnumerateFileSystemEntries(_home.Path));
        return new CliRun(exit, stdout.ToString(), stderr.ToString());
    }

    private sealed class TestCatalog : IGraphBlockCatalog
    {
        public IReadOnlyDictionary<string, IGraphBlockType> Types => GraphFixtures.Blocks;
    }

    private sealed class AnyWorld : IGraphWorld
    {
        public GraphLookup Repo(string name) => GraphLookup.Known;

        public GraphLookup Connection(string name) => GraphLookup.Known;

        public GraphLookup Memory(string name) => GraphLookup.Known;
    }

    private sealed record CliRun(int Exit, string Stdout, string Stderr);
}
