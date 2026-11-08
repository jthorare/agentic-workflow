using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.GetInstanceLine;

/// <summary>Read the line board of a workflow.line instance (line spec LN-API1).</summary>
/// <param name="Instance">The instance.</param>
public sealed record GetInstanceLineRequest(WorkflowInstanceId Instance) : IQuery<LineView>;
