using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Features.PlanSandbox;

/// <summary>The plan of one launch.</summary>
/// <param name="Plan">The plan.</param>
public sealed record PlanSandboxResponse(SandboxPlan Plan);
