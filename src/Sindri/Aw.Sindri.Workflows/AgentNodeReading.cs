using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Reads a 'run_agent' node for its launch (#2447): the same fence shape the 'prompt' block uses, one fence per reference's value, the author's words bare.</summary>
internal sealed class AgentNodeReading(IWorkflowRunStore runs, IWorkflowStore workflows, IGraphModelPort model)
{
    /// <summary>Reads the node.</summary>
    /// <param name="node">The node run.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>What the launch needs.</returns>
    public async Task<AgentNodeRead> Read(AgentNodeRef node, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        var row = GraphFailures.Value(await runs.Get(node.Run, cancellationToken).ConfigureAwait(false));
        var spec = GraphFailures.Value(await workflows.GetRevision(row.WorkflowId, row.Revision, cancellationToken).ConfigureAwait(false)).Spec;
        var graphNode = spec.Nodes.FirstOrDefault(n => n.Id == node.Node) ?? throw new ApplicationFailureException($"the spec has no node '{node.Node.Value}'", nonRetryable: true);
        var nodeRuns = GraphFailures.Value(await runs.NodeRuns(node.Run, cancellationToken).ConfigureAwait(false));
        var scope = new GraphNodeScope(row, spec, nodeRuns);
        if (node.ItemIndex > 0)
        {
            // why: a node of a for_each body reads the item and the loop's nodes of its own item (#2470), the way the block activity opens them
            var (loop, cutNode) = GraphLoopScopes.Open(scope, spec, node.Node, node.ItemIndex);
            if (cutNode is not null)
            {
                return new AgentNodeRead(row, spec, graphNode.Config, Prompt: null, scope, cutNode);
            }

            scope = new GraphNodeScope(row, spec, nodeRuns, loop);
        }

        var config = GraphConfig.Interpolate(graphNode.Config, scope.Lookup);
        var prompt = graphNode.Config["prompt"] is JsonValue value && value.TryGetValue<string>(out var raw)
            ? FencedPrompt.Render(raw, scope.Lookup, model.Untrusted)   // why: the interpolated prompt has lost which text came from outside
            : null;
        return new AgentNodeRead(row, spec, config, prompt, scope);
    }
}
