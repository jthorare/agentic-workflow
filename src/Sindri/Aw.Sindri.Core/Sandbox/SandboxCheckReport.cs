using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>What Check again found (sandbox spec §6): every blocker on one card, and the parked instances that may resume now.</summary>
/// <param name="Blockers">Every blocker found, none when the box can confine a Step.</param>
/// <param name="ReadyToResume">The instances parked on a sandbox code, oldest first and capped at the free session slots; empty while a blocker stands.</param>
public sealed record SandboxCheckReport(IReadOnlyList<SandboxBlocker> Blockers, IReadOnlyList<WorkflowInstanceId> ReadyToResume)
{
    /// <summary>The same instances as <see cref="ReadyToResume"/>, with when each parked and which item it runs for (the Check again card's resume list).</summary>
    public IReadOnlyList<ParkedOnSandbox> Resume { get; init; } = [];

    /// <summary>The sentence <see cref="NotChecked"/> carries when the hooks canary was skipped because the fixture plan has no login.</summary>
    public const string HooksNotChecked = "The hooks weren't checked: no agent is picked for Steps.";

    /// <summary>The checks that were skipped, as sentences; informational, it changes neither <see cref="Ready"/> nor <see cref="Blockers"/>. Empty when every check ran.</summary>
    public IReadOnlyList<string> NotChecked { get; init; } = [];

    /// <summary>True when nothing blocks a Step.</summary>
    public bool Ready => Blockers.Count == 0;
}
