using Aw.Kernel;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The check that every absolute host path on the agent's command line is visible inside the sandbox (#2151).</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MA0182", Justification = "A red-test stub: the launcher calls it with the fix (#2151).")]
internal static class PlanArgvPaths
{
    /// <summary>Finds an absolute-path argument that a tmpfs or a deny hides in the final mount stack.</summary>
    /// <param name="plan">The plan the agent runs under.</param>
    /// <param name="argv">The agent's arguments.</param>
    /// <returns>The SND-LAUNCH-015 refusal naming the hidden path, or null when every path is visible.</returns>
    internal static Error? Check(SandboxPlan plan, IReadOnlyList<string> argv) => throw new NotSupportedException();
}
