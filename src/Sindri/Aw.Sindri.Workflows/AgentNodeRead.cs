using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>A 'run_agent' node as the launch reads it: the run, the node's config with references filled in, the prompt with each reference fenced as untrusted, and the node's scope.</summary>
/// <param name="Run">The workflow run.</param>
/// <param name="Spec">The run's spec.</param>
/// <param name="Config">The node's config, interpolated.</param>
/// <param name="Prompt">The node's prompt with every reference's value in its own untrusted fence; null when the node has no text 'prompt'.</param>
/// <param name="Scope">The node's reference scope; for a node of a 'for_each' body it reads the item and the loop's nodes of its own item (#2470).</param>
/// <param name="CutNode">The node whose output was cut when the loop's list could not be read, else null.</param>
internal sealed record AgentNodeRead(WorkflowRun Run, GraphSpec Spec, JsonObject Config, string? Prompt, GraphNodeScope Scope, string? CutNode = null);
