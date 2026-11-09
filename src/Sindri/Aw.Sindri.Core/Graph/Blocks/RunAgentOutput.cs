using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a finished 'run_agent' node hands on (PR 7): the declared fields plus status, branch, commit, sessionId and usage.</summary>
public static class RunAgentOutput
{
    /// <summary>Builds the node's outputs.</summary>
    /// <param name="declared">The checked values of the declared fields.</param>
    /// <param name="end">How the session ended.</param>
    /// <returns>The outputs, the branch taken is 'out'.</returns>
    public static BlockResult Compose(IReadOnlyDictionary<string, JsonNode?> declared, AgentNodeEnd end) => throw new NotSupportedException("red");
}
