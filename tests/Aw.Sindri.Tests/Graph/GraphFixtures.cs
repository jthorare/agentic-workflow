using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Tests.Graph;

/// <summary>Block types, names and specs the graph tests share: the shapes of the issue's example workflow, without the engine.</summary>
internal static class GraphFixtures
{
    public static readonly IReadOnlyDictionary<string, IGraphBlockType> Blocks = new Dictionary<string, IGraphBlockType>(StringComparer.Ordinal)
    {
        ["schedule"] = new TestBlock("schedule", trigger: true, [new("cron", BlockFieldType.Text), new("timezone", BlockFieldType.Text, Required: false)], new Dictionary<string, string>(StringComparer.Ordinal) { ["scheduledFor"] = "string" }),
        ["memory_search"] = new TestBlock("memory_search", trigger: false, [new("memory", BlockFieldType.Text), new("query", BlockFieldType.Text)], new Dictionary<string, string>(StringComparer.Ordinal) { ["facts"] = "string" }),
        ["notify"] = new TestBlock("notify", trigger: false, [new("title", BlockFieldType.Text), new("body", BlockFieldType.Text, Required: false)], new Dictionary<string, string>(StringComparer.Ordinal)),
        ["run_agent"] = new TestBlock("run_agent", trigger: false, [new("repo", BlockFieldType.Text), new("readOnlyRepos", BlockFieldType.TextList, Required: false), new("prompt", BlockFieldType.Text, Required: false)], new Dictionary<string, string>(StringComparer.Ordinal) { ["drifted"] = "boolean" }),
        ["webhook"] = new TestBlock("webhook", trigger: false, [new("url", BlockFieldType.Text), new("headers", BlockFieldType.Map, Required: false), new("args", BlockFieldType.TextList, Required: false)], new Dictionary<string, string>(StringComparer.Ordinal)),
        ["limit"] = new TestBlock("limit", trigger: false, [new("count", BlockFieldType.Number), new("enabled", BlockFieldType.Boolean, Required: false)], new Dictionary<string, string>(StringComparer.Ordinal)),
        ["prompt"] = new TestBlock("prompt", trigger: false, [new("prompt", BlockFieldType.Text), new("output", BlockFieldType.Map, Required: false)], new Dictionary<string, string>(StringComparer.Ordinal)),
        ["condition"] = new TestBlock("condition", trigger: false, [new("when", BlockFieldType.Text)], new Dictionary<string, string>(StringComparer.Ordinal), ["true", "false"]),
    };

    /// <summary>A context where every name is known and cron and conditions parse (unless a test says otherwise).</summary>
    public static GraphValidator Validator(IGraphWorld? world = null, Func<string, string?, string?>? cron = null, Func<string, ConditionParse>? condition = null, IReadOnlyDictionary<string, IGraphBlockType>? blocks = null, ResourceBoxCap? boxCap = null, Func<string, IReadOnlyList<CodeProblem>>? codeCheck = null) =>
        new(new GraphValidationContext(blocks ?? Blocks, world ?? new KnownWorld(), cron ?? ((_, _) => null), condition ?? (text => new ConditionParse(Error: null, References: Template(text)))) { BoxCap = boxCap, CodeCheck = codeCheck });

    public static GraphNode Trigger(string id) => new(NodeId.From(id), "schedule", Title: null, new JsonObject { ["cron"] = "0 7 * * 1-5", ["timezone"] = "America/Los_Angeles" });

    public static GraphNode Search(string id, string query = "last checked") => new(NodeId.From(id), "memory_search", Title: null, new JsonObject { ["memory"] = "drift", ["query"] = query });

    public static GraphNode Prompt(string id, string question) => new(NodeId.From(id), "prompt", Title: null, new JsonObject { ["prompt"] = question });

    public static GraphNode Notify(string id, string body) => new(NodeId.From(id), "notify", Title: null, new JsonObject { ["title"] = "Drifted", ["body"] = body });

    public static GraphNode Agent(string id, string repo, string prompt = "check") => new(NodeId.From(id), "run_agent", Title: null, new JsonObject { ["repo"] = repo, ["prompt"] = prompt });

    public static GraphNode Condition(string id, string when) => new(NodeId.From(id), "condition", Title: null, new JsonObject { ["when"] = when });

    public static GraphNode Limit(string id, JsonObject config) => new(NodeId.From(id), "limit", Title: null, config);

    public static GraphNode Hook(string id, JsonObject extra)
    {
        var config = new JsonObject { ["url"] = "https://example.test" };
        foreach (var (key, value) in extra.ToList())
        {
            config[key] = value?.DeepClone();
        }

        return new GraphNode(NodeId.From(id), "webhook", Title: null, config);
    }

    public static GraphEdge Edge(string from, string to, string? output = null) => new(From: NodeId.From(from), Output: output, To: NodeId.From(to));

    public static GraphSpec SpecWith(GraphResources resources, IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges) => Spec(nodes, edges) with { Resources = resources };

    public static GraphSpec Spec(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges) => new(Version: 1, Name: "Test", Description: null, Concurrency: null, Resources: null, Nodes: nodes, Edges: edges);

    private static IReadOnlyList<string> Template(string text) => Core.Graph.Interpolation.Template.References(text);

    private sealed record TestBlock(string Type, bool IsTrigger, BlockSchema Config, string Example, BlockOutputs Declared) : IGraphBlockType
    {
        public TestBlock(string type, bool trigger, IReadOnlyList<BlockField> fields, IReadOnlyDictionary<string, string> outputs, IReadOnlyList<string>? branches = null)
            : this(type, trigger, new BlockSchema(fields), "{}", new BlockOutputs(outputs, branches))
        {
        }

        public string Title => Type;

        public BlockGroup Group => IsTrigger ? BlockGroup.Triggers : BlockGroup.Logic;

        public BlockOutputs OutputsFor(JsonObject config) => Declared;
    }

    /// <summary>A world that answers every repo name with <paramref name="repo"/>.</summary>
    public sealed class RepoWorld(GraphLookup repo) : IGraphWorld
    {
        public GraphLookup Repo(string name) => repo;

        public GraphLookup Connection(string name) => GraphLookup.Known;

        public GraphLookup Memory(string name) => GraphLookup.Known;
    }

    private sealed class KnownWorld : IGraphWorld
    {
        public GraphLookup Repo(string name) => GraphLookup.Known;

        public GraphLookup Connection(string name) => GraphLookup.Known;

        public GraphLookup Memory(string name) => GraphLookup.Known;
    }
}
