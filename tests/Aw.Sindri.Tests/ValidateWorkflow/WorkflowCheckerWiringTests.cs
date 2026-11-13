using System.Text.Json.Nodes;
using Aw.Kernel.Telemetry;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.ValidateWorkflow;

/// <summary>The host's own container gives the workflow checker the connection and memory stores (#3211): a name the stores lack is refused, not waved through as 'unavailable'.</summary>
[Lane(TestLane.Integration)]
public sealed class WorkflowCheckerWiringTests : IDisposable
{
    private static readonly CancellationToken _token = TestContext.Current.CancellationToken;

    private readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-checker-wiring-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_stateRoot))
        {
            Directory.Delete(_stateRoot, recursive: true);
        }
    }

    private async Task<GraphValidation> Activate(GraphSpec spec)
    {
        var services = Composition.Compose(
            new ServiceCollection(),
            name => name switch { "AW_STATE_DIR" => _stateRoot, _ => null },
            new FakeTimeProvider());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _ = provider.GetRequiredService<TelemetryRuntime>();   // so the container disposes it, as Program.cs does
        await using var scope = provider.CreateAsyncScope();

        var validator = await scope.ServiceProvider.GetRequiredService<IWorkflowChecker>().Validator(_token);
        return validator.Validate(spec, GraphMode.Activate);
    }

    [Fact]
    public async Task The_host_checker_refuses_a_trigger_naming_a_connection_that_does_not_exist_with_GRAPH_NAME_001()
    {
        var trigger = new GraphNode(NodeId.From("hook"), "connection_event", Title: null, new JsonObject { ["connection"] = "github:nowhere", ["event"] = "github.pr.merged" });

        var result = await Activate(Spec([trigger, Notify("tell", "hi")], [Edge("hook", "tell")]));

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-NAME-001");
        Assert.Contains("hook", finding.Nodes, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_host_checker_refuses_a_memory_block_naming_a_memory_that_does_not_exist_with_GRAPH_NAME_001()
    {
        var recall = new GraphNode(NodeId.From("recall"), "memory_search", Title: null, new JsonObject { ["memory"] = "nowhere", ["query"] = "last checked" });

        var result = await Activate(Spec([Trigger("daily"), recall], [Edge("daily", "recall")]));

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-NAME-001");
        Assert.Contains("recall", finding.Nodes, StringComparer.Ordinal);
    }
}
