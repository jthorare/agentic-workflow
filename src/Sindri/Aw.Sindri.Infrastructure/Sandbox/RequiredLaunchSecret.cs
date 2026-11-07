using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>#2163: a plan that leaves <c lang="text">AW_SINDRI_LAUNCH_SECRET</c> to the launcher is not wrapped without a value for it, or the agent would start with no secret and its own reports would be refused.</summary>
internal static class RequiredLaunchSecret
{
    internal const string Variable = "AW_SINDRI_LAUNCH_SECRET";

    /// <summary>Checks a launch's values against the plan.</summary>
    /// <param name="plan">The plan being wrapped for a launched session.</param>
    /// <param name="values">What the launcher registered for the launch.</param>
    /// <returns>The refusal naming the variable, or null when the plan has no such slot (an exec or a canary) or the value is there.</returns>
    internal static Error? Check(SandboxPlan plan, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(values);
        return BubblewrapArgv.LauncherSupplied(plan).Contains(Variable, StringComparer.Ordinal) && !values.ContainsKey(Variable)
            ? SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.Env, $"{Variable} has no value for this launch: the launcher did not register its launch secret before the wrap")
            : null;
    }
}
