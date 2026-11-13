using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.TestWorkflow;

/// <summary>A test run was started.</summary>
/// <param name="Run">The run's id; its mode is 'test'.</param>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The revision the test walks.</param>
/// <param name="Mode">Always 'test'.</param>
internal sealed record TestWorkflowResponse(WorkflowRunId Run, WorkflowSlug Slug, int Revision, string Mode);
