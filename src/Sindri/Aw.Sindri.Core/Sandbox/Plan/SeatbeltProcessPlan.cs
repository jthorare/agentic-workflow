namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The Seatbelt form of a plain process's plan (agent-first PR 16, #2489): the agent path's <see cref="SeatbeltPlan"/> needs a PlanContext only an agent launch builds.</summary>
internal static class SeatbeltProcessPlan
{
    internal static SandboxPlan Rewrite(SandboxPlan plan) => throw new NotSupportedException();
}
