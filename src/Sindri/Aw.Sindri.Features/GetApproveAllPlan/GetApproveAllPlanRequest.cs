using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Features.GetApproveAllPlan;

/// <summary>What one hold would approve (setup-once spec §7.2, GET /workflow/recipes/approve-all; query): SND-PROFILE-001 when there is no profile.</summary>
public sealed record GetApproveAllPlanRequest() : IQuery<ApproveAllPlan>;
