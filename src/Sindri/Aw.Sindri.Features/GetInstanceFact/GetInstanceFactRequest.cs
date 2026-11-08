using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.GetInstanceFact;

/// <summary>The scrubbed body behind one fact row (API spec section 3.8, GET /workflow/instances/{id}/facts/{key}; query).</summary>
/// <param name="Instance">The instance id.</param>
/// <param name="Key">The fact key.</param>
public sealed record GetInstanceFactRequest(WorkflowInstanceId Instance, string Key) : IQuery<FactView>;
