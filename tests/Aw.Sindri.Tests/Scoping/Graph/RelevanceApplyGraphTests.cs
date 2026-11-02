using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Scoping.Graph;

/// <summary>Issue 1005: 'step.scope.relevance-apply' also rewrites fact:graph. A lens entry the graph's cap left out has no verdict, so it stays in and the graph records that it was not reviewed.</summary>
[Lane(TestLane.Unit)]
public sealed class RelevanceApplyGraphTests
{
    [Fact]
    public async Task Lens_entries_outside_the_graph_stay_in_and_the_graph_says_how_many_were_not_reviewed()
    {
        var facts = ScopingFacts.Expiry();
        facts["graph"] = """{"version":1,"mapVersion":"v1","anchors":[],"depth":2,"hops":[],"nodes":[{"id":"feature:sharing","kind":"feature","summary":"Public link sharing","rank":1,"lens":"code","lensItem":"L-code-1","citations":[]}],"edges":[],"limits":[]}""";
        facts["relevanceVerdicts"] = """{"model":"m","verdicts":[]}""";
        facts["relevanceRulings"] = """{"rulings":[]}""";

        var result = await new RelevanceApplyStep().Run(ScopingStepContexts.Of("step.scope.relevance-apply", facts), TestContext.Current.CancellationToken);

        Assert.Equal("applied", result.Result!.Outcome.Value);
        var graph = JsonNode.Parse(result.Result.Outputs[FactKey.From("fact:graph")].GetRawText())!;
        var limit = Assert.Single(ScopeJson.Items(graph, "limits"), l => ScopeJson.Str(l, "why") == "unreviewed");
        Assert.Contains("outside the graph cap were not reviewed", ScopeJson.Str(limit, "not"), StringComparison.Ordinal);
        _ = Assert.Single(ScopeJson.Items(graph, "nodes"));
    }
}
