using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>What a sandbox plan adds to a Step's policy file (sandbox spec §5): its inner policy and whether the CLI's own sandbox runs.</summary>
/// <param name="Inner">The plan's inner policy.</param>
/// <param name="InnerSandboxOn">Whether the CLI's own sandbox is on inside Sindri's.</param>
internal sealed record SandboxPolicyOverlay(InnerPolicy Inner, bool InnerSandboxOn)
{
    /// <summary>The overlay of one launch's plan.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The overlay; the CLI's own sandbox counts as on only when the plan says 'on' (an 'auto' that was not proven stays off).</returns>
    internal static SandboxPolicyOverlay From(SandboxPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new SandboxPolicyOverlay(plan.Policy, plan.Inner == InnerSandbox.On);
    }
}
