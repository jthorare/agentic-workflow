using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.relevance-apply' on the block runtime (spec 4.3): <see cref="RelevanceApply"/> over the lenses, the coverage, the brief and the two label sets. It rewrites 'fact:lenses', 'fact:graph' (the same entries leave it) and 'fact:coverage' without the
/// entries both reviewers called off topic, and writes 'fact:relevance' and 'fact:mapFixes'. A verdict or ruling for an id the lenses do not hold, or an id twice, fails the Step with the fix line.
/// </summary>
internal sealed class RelevanceApplyStep : IBlockStep, IBlockStepId
{
    private const string Name = "step.scope.relevance-apply";
    private const string Unreviewed = "unreviewed";

    private static readonly string[] _reads = ["fact:lenses", "fact:coverage", "fact:brief", "fact:relevanceVerdicts", "fact:relevanceRulings"];

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From(Name);

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Missing(context, _reads) is { Count: > 0 } missing)
        {
            return ValueTask.FromResult(BlockStepResult.Failed($"{Name} needs {string.Join(", ", missing)}, which the instance does not hold"));
        }

        var applied = RelevanceApply.Apply(Fact(context, "fact:lenses"), Fact(context, "fact:coverage"), Fact(context, "fact:brief"), Fact(context, "fact:relevanceVerdicts"), Fact(context, "fact:relevanceRulings"));
        return ValueTask.FromResult(applied.IsError
            ? BlockStepResult.Failed(applied.Error.Message)
            : StepFacts.Done("applied", ("fact:lenses", applied.Value.Lenses), ("fact:coverage", applied.Value.Coverage), ("fact:relevance", applied.Value.Relevance), ("fact:mapFixes", applied.Value.MapFixes), ("fact:graph", Graph(context, Fact(context, "fact:lenses"), applied.Value.Lenses))));
    }

    // The graph without the nodes whose lens items left the lenses, and the edges that lost an end; a legacy instance with no graph gets the projection of the kept lenses.
    private static JsonObject Graph(StepContext context, JsonNode before, JsonObject kept)
    {
        if (StepFacts.Read(context, "fact:graph") is not JsonObject graph)
        {
            return GraphGather.Assemble(graphView: null, kept, anchors: []);
        }

        var left = RelevanceEntries.Of(before).Select(static e => e.Id).Except(RelevanceEntries.Of(kept).Select(static e => e.Id), StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var nodes = ScopeJson.Items(graph, "nodes").Where(n => !left.Contains(ScopeJson.Str(n, "lensItem") ?? string.Empty)).ToList();
        var ids = nodes.Select(static n => ScopeJson.Str(n, "id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var edges = ScopeJson.Items(graph, "edges").Where(e => ids.Contains(ScopeJson.Str(e, "from") ?? string.Empty) && ids.Contains(ScopeJson.Str(e, "to") ?? string.Empty)).ToList();
        return GraphGather.Assemble(new JsonObject
        {
            ["mapVersion"] = ScopeJson.Str(graph, "mapVersion"),
            ["anchors"] = graph["anchors"]?.DeepClone(),
            ["depth"] = ScopeJson.Int(graph, "depth"),
            ["hops"] = graph["hops"]?.DeepClone(),
            ["nodes"] = new JsonArray([.. nodes.Select(static n => n.DeepClone())]),
            ["edges"] = new JsonArray([.. edges.Select(static e => e.DeepClone())]),
            ["limits"] = Limits(graph, nodes, kept),
        }, kept, anchors: []);
    }

    // The graph's limits, plus one for the lens entries the graph's cap left out: the review saw only the graph, so those have no verdict and stay in, marked unreviewed.
    private static JsonArray Limits(JsonObject graph, List<JsonNode> nodes, JsonObject kept)
    {
        var limits = new JsonArray([.. ScopeJson.Items(graph, "limits").Where(static l => !string.Equals(ScopeJson.Str(l, "why"), Unreviewed, StringComparison.Ordinal)).Select(static l => l.DeepClone())]);
        var shown = nodes.Select(static n => ScopeJson.Str(n, "lensItem")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var outside = RelevanceEntries.Of(kept).Count(e => !shown.Contains(e.Id));
        if (outside > 0)
        {
            ScopeJson.Append(limits, new JsonObject { ["why"] = Unreviewed, ["not"] = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{outside} lens entries outside the graph cap were not reviewed") });
        }

        return limits;
    }

    private static JsonNode Fact(StepContext context, string key) => StepFacts.Read(context, key) ?? new JsonObject();
}
