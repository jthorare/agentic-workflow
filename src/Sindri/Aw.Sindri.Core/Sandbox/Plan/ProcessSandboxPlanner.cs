using Aw.Kernel;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The pure plan builder for a plain process (agent-first PR 16): the agent path's <see cref="SandboxPlanner"/> stays as it is.</summary>
public static class ProcessSandboxPlanner
{
    /// <summary>Builds the plan for one process.</summary>
    /// <param name="mechanism">The mechanism of the calling adapter.</param>
    /// <param name="request">The process.</param>
    /// <returns>The plan, or SND-LAUNCH-015 when the command or a resource path is not absolute.</returns>
    public static Result<SandboxPlan> Build(SandboxMechanism mechanism, ProcessSandboxRequest request) =>
        throw new NotSupportedException("not built yet");
}
