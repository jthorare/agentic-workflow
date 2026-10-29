using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>What the checks found, in the owner's words.</summary>
/// <param name="Blockers">Every blocker, each as the sentence the card and doctor say; none when a Step can be confined.</param>
/// <param name="ReadyToResume">The parked instances the next press would resume, oldest first and no more than the free session slots; empty while a blocker stands.</param>
/// <param name="Ready">True when nothing blocks a Step.</param>
public sealed record CheckSandboxResponse(IReadOnlyList<SandboxBlockerLine> Blockers, IReadOnlyList<WorkflowInstanceId> ReadyToResume, bool Ready)
{
    /// <summary>When the checks ran; left out when the answer was not recorded.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? CheckedAt { get; init; }

    /// <summary>The Steps a resume would start, as <see cref="ReadyToResume"/> orders and caps them, each with its item and title.</summary>
    public IReadOnlyList<SandboxResumeLine> Resume { get; init; } = [];
}
