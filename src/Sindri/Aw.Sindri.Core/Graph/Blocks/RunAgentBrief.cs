using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The first message a 'run_agent' session gets (PR 7): the prompt, the output fields and the exact report command. Text from outside (the prompt carries references' values) sits inside a fence.</summary>
public static class RunAgentBrief
{
    /// <summary>Renders the brief; deterministic for the same node run.</summary>
    /// <param name="address">The node run.</param>
    /// <param name="token">The node run's token on the report's wire.</param>
    /// <param name="config">The node's config.</param>
    /// <returns>The brief.</returns>
    public static string Render(NodeRunAddress address, RunToken token, RunAgentConfig config) => throw new NotSupportedException("red");
}
