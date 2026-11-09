using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a finished 'run_agent' node hands on (PR 7): the declared fields plus status, branch, commit, sessionId and usage.</summary>
public static class RunAgentOutput
{
    /// <summary>Builds the node's outputs.</summary>
    /// <param name="declared">The checked values of the declared fields.</param>
    /// <param name="end">How the session ended.</param>
    /// <returns>The outputs, the branch taken is 'out'.</returns>
    public static BlockResult Compose(IReadOnlyDictionary<string, JsonNode?> declared, AgentNodeEnd end)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(end);
        var outputs = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (name, value) in declared)
        {
            outputs[name] = value?.DeepClone();
        }

        // why: the standard names are reserved (the validator refuses a declared output with one), so these never overwrite a declared field
        outputs["status"] = JsonValue.Create("succeeded");
        outputs["branch"] = JsonValue.Create(end.Branch);
        outputs["commit"] = JsonValue.Create(end.Commit);
        outputs["sessionId"] = JsonValue.Create(end.AgentSession);
        outputs["usage"] = new JsonObject { ["tokens"] = end.Tokens, ["emptyDiff"] = end.EmptyDiff };
        return new BlockResult(outputs);
    }
}
