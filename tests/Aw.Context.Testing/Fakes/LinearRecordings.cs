using System.Text.Json.Nodes;
using Aw.Context.Testing.Fixtures;

namespace Aw.Context.Testing.Fakes;

/// <summary>The recorded Linear connections, by root field, read from the fixture's 'linear/*.json' responses.</summary>
internal static class LinearRecordings
{
    /// <summary>Connection root field to its recording.</summary>
    public static readonly IReadOnlyDictionary<string, string> Connections = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["projects"] = "linear/projects.json",
        ["documents"] = "linear/documents.json",
        ["projectUpdates"] = "linear/project-updates.json",
        ["issues"] = "linear/issues.json",
    };

    /// <summary>Single-node root field to the connection it looks in.</summary>
    public static readonly IReadOnlyDictionary<string, string> Singles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["project"] = "projects",
        ["document"] = "documents",
        ["issue"] = "issues",
    };

    /// <summary>Every root field the fake answers, for error messages.</summary>
    public static string Supported => string.Join(", ", Connections.Keys.Concat(Singles.Keys).Order(StringComparer.Ordinal));

    /// <summary>The recorded nodes of a connection (a fresh copy each call).</summary>
    public static List<JsonNode> Nodes(string connection)
    {
        var recording = JsonNode.Parse(SplitPanesFixture.ReadText(Connections[connection]));
        return [.. recording?["data"]?[connection]?["nodes"]?.AsArray().OfType<JsonNode>().Select(n => n.DeepClone()) ?? []];
    }
}
