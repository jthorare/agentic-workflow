using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.Design;

/// <summary>Read the stop-2 view of a workflow.plan instance (line spec LN-API2).</summary>
/// <param name="Instance">The instance.</param>
public sealed record GetInstanceDesignStopRequest(WorkflowInstanceId Instance) : IQuery<DesignStopView>;
