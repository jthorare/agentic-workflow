using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>A 'run_agent' node as the launch reads it: the run, the node's config with references filled in, the prompt with each reference fenced as untrusted, and the node's scope.</summary>
/// <param name="Run">The workflow run.</param>
/// <param name="Spec">The run's spec.</param>
/// <param name="Config">The node's config, interpolated.</param>
/// <param name="Prompt">The node's prompt with every reference's value in its own untrusted fence; null when the node has no text 'prompt'.</param>
/// <param name="Scope">The node's reference scope.</param>
internal sealed record AgentNodeRead(WorkflowRun Run, GraphSpec Spec, JsonObject Config, string? Prompt, GraphNodeScope Scope);
