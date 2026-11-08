using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Tests.Graph;

/// <summary>Block types, names and specs the graph tests share: the shapes of the issue's example workflow, without the engine.</summary>
internal static class GraphFixtures
{
    public static readonly IReadOnlyDictionary<string, IGraphBlockType> Blocks = new Dictionary<string, IGraphBlockType>(StringComparer.Ordinal)
    {
        ["schedule"] = new TestBlock("schedule", trigger: true, [new("cron", BlockFieldType.Text), new("timezone", BlockFieldType.Text, Required: false)], new Dictionary<string, string>(StringComparer.Ordinal)),
        ["memory_search"] = new TestBlock("memory_search", trigger: false, [new("memory", BlockFieldType.Text), new("query", BlockFieldType.Text)], new Dictionary<string, string>(StringComparer.Ordinal) { ["facts"] = "string" }),
        ["notify"] = new TestBlock("notify", trigger: false, [new("title", BlockFieldType.Text), new("body", BlockFieldType.Text, Required: false)], new Dictionary<string, string>(StringComparer.Ordinal)),
        ["condition"] = new TestBlock("condition", trigger: false, [new("when", BlockFieldType.Text)], new Dictionary<string, string>(StringComparer.Ordinal), ["true", "false"]),
    };

    /// <summary>A context where every name is known and cron and conditions parse (unless a test says otherwise).</summary>
    public static GraphValidator Validator(IGraphWorld? world = null, Func<string, string?, string?>? cron = null, Func<string, ConditionParse>? condition = null) =>
        new(new GraphValidationContext(Blocks, world ?? new KnownWorld(), cron ?? ((_, _) => null), condition ?? (text => new ConditionParse(Error: null, References: Template(text)))));

    public static GraphNode Schedule(string id) => new(NodeId.From(id), "schedule", Title: null, new JsonObject { ["cron"] = "0 7 * * 1-5", ["timezone"] = "America/Los_Angeles" });

    public static GraphNode Search(string id, string query = "last checked") => new(NodeId.From(id), "memory_search", Title: null, new JsonObject { ["memory"] = "drift", ["query"] = query });

    public static GraphNode Notify(string id, string body) => new(NodeId.From(id), "notify", Title: null, new JsonObject { ["title"] = "Drifted", ["body"] = body });

    public static GraphEdge Edge(string from, string to, string? output = null) => new(From: NodeId.From(from), Output: output, To: NodeId.From(to));

    public static GraphSpec Spec(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges) => new(Version: 1, Name: "Test", Description: null, Concurrency: null, Resources: null, Nodes: nodes, Edges: edges);

    private static IReadOnlyList<string> Template(string text) => Aw.Sindri.Core.Graph.Interpolation.Template.References(text);

    private sealed record TestBlock(string Type, bool IsTrigger, BlockSchema Config, string Example, BlockOutputs Declared) : IGraphBlockType
    {
        public TestBlock(string type, bool trigger, IReadOnlyList<BlockField> fields, IReadOnlyDictionary<string, string> outputs, IReadOnlyList<string>? branches = null)
            : this(type, trigger, new BlockSchema(fields), "{}", new BlockOutputs(outputs, branches))
        {
        }

        public BlockOutputs OutputsFor(JsonObject config) => Declared;
    }

    private sealed class KnownWorld : IGraphWorld
    {
        public GraphLookup Repo(string name) => GraphLookup.Known;

        public GraphLookup Connection(string name) => GraphLookup.Known;

        public GraphLookup Memory(string name) => GraphLookup.Known;
    }
}
