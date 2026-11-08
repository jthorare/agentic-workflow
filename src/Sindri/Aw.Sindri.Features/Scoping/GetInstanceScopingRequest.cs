using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The stop view of a workflow.scoping instance (scoping spec §8.4, API2; GET /workflow/instances/{id}/scoping; query).</summary>
/// <param name="Instance">The instance id.</param>
public sealed record GetInstanceScopingRequest(WorkflowInstanceId Instance) : IQuery<ScopingView>;
