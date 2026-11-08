using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.ReadInjection;

/// <summary>The text behind the open run's injection plan (API spec section 3.8, GET /workflow/instances/{id}/injection; query).</summary>
/// <param name="Instance">The instance id.</param>
/// <param name="Section">Only this section, or null for all of them.</param>
public sealed record ReadInjectionRequest(WorkflowInstanceId Instance, string? Section = null) : IQuery<InjectionView>;
