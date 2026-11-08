using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'run_agent' block (issue 2258, PR 7): launches an agent session through <see cref="IAgentRunPort"/>, waits for its report and hands on the
/// fields the node declared plus 'status', 'branch', 'commit', 'sessionId' and 'usage'.
/// </summary>
public sealed class RunAgentBlock(IAgentRunPort agents) : IGraphBlock
{
    /// <inheritdoc />
    public string Type => "run_agent";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("repo", BlockFieldType.Text),
        new("agent", BlockFieldType.Text),
        new("prompt", BlockFieldType.Text),
        new("outputs", BlockFieldType.Map),
        new("readOnlyRepos", BlockFieldType.TextList, Required: false),
        new("timeout", BlockFieldType.Text, Required: false),
        new("connections", BlockFieldType.TextList, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"repo":"client-repo","agent":"claude-code","prompt":"Compare the contracts in api-repo with the client.","outputs":{"drifted":"boolean","prUrl":"string?"},"timeout":"45m"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => throw new NotSupportedException();

    /// <summary>Checks an agent's report against the node's declared outputs; the error names the field, so the agent can report again.</summary>
    /// <param name="config">The node's config.</param>
    /// <param name="report">The agent's JSON report.</param>
    /// <returns>Success, or the first field that is missing or of the wrong type.</returns>
    public static Result<Unit> CheckReport(RunAgentConfig config, JsonObject report) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        _ = (agents, context, cancellationToken);
        throw new NotSupportedException();
    }
}
