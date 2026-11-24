namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/workflow-runs/{run}/rerun.</summary>
/// <param name="FromNode">The step to run first again.</param>
/// <param name="Input">A JSON object that replaces the step's stored input, or null.</param>
internal sealed record RerunWorkflowRunBody(string FromNode, string? Input = null);
