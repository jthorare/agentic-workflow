using System.Text.Json.Nodes;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/workflow-runs/{run}/rerun.</summary>
/// <param name="FromNode">The step to run first again.</param>
/// <param name="Input">A JSON object that replaces the step's stored input, or null. The service refuses a value that is not an object (SND-GRAPH-106).</param>
internal sealed record RerunWorkflowRunBody(string FromNode, JsonNode? Input = null);
