using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Eval;
using Aw.Context.Core.ScopeGraph;
using Aw.Context.Testing.Engine;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>'input.context.graph' remembers the graph it hands a scoping Step under the instance, so the context tools can scope that Step's reads to it (issue 1005).</summary>
[Lane(TestLane.Unit)]
public sealed class ContextGraphInputTests
{
    private const string Instance = "ITM-01JN3/scoping/1";

    private static readonly string[] _anchors = ["feature:sharing"];

    private static ScopeGraphView Graph() => new(1, "v3", ["feature:sharing"], 2, [], [], [], []);

    private static ContextGraphInput Input(FakeContextEngine engine) =>
        new(engine, new ContextInputScopes(new ContextInputFixtures.Profiles(ContextInputFixtures.Profile())), ContextInputFixtures.NoFacts(), ShippedComposition.Json);

    private static StepInputContext Context(string? instance) => new(
        BlockId.From(ContextGraphInput.Id), BlockId.From("step.scope.gather"),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["anchors"] = JsonSerializer.SerializeToElement(_anchors) },
        new Dictionary<FactKey, JsonElement>(), ContextInputFixtures.Web, Instance: instance);

    [Fact]
    public async Task A_good_read_is_remembered_under_the_instance_address_and_handed_over()
    {
        var engine = FakeContextEngine.WithProbes(new ProbeSet([]));
        var graph = Graph();
        engine.ScopeGraphAnswer = Results.Success(graph);

        var filled = await Input(engine).Fill(Context(Instance), TestContext.Current.CancellationToken);

        var (address, remembered) = Assert.Single(engine.Remembered);
        Assert.Equal(Instance, address);
        Assert.Same(graph, remembered);
        Assert.True(filled.Value.Result is not null);
    }

    [Fact]
    public async Task A_remember_error_still_hands_the_graph_over_and_adds_a_note()
    {
        var engine = FakeContextEngine.WithProbes(new ProbeSet([]));
        engine.ScopeGraphAnswer = Results.Success(Graph());
        engine.RememberAnswer = ContextErrors.ScopeGraphUnavailable.With("the store is down");

        var filled = await Input(engine).Fill(Context(Instance), TestContext.Current.CancellationToken);

        Assert.True(filled.Value.Result is not null);
        var text = filled.Value.Sections[ContextGraphInput.Section];
        Assert.Contains("note: ", text, StringComparison.Ordinal);
        Assert.Contains("the store is down", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_fill_with_no_instance_remembers_nothing()
    {
        var engine = FakeContextEngine.WithProbes(new ProbeSet([]));
        engine.ScopeGraphAnswer = Results.Success(Graph());

        _ = await Input(engine).Fill(Context(instance: null), TestContext.Current.CancellationToken);

        Assert.Empty(engine.Remembered);
    }
}
