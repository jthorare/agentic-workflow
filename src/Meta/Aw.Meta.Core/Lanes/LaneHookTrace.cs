namespace Aw.Meta.Core.Lanes;

/// <summary>The one line a lane's hook leaves in lane-sessions/&lt;lane&gt;.hook each time it runs, whether or not it acts.</summary>
/// <param name="At">When the hook ran.</param>
/// <param name="Hook">Which hook: meta-lane-gate or session-id-report.</param>
/// <param name="Event">The normalised event (UserPromptSubmit, SessionStart, PostToolUse, Stop, AfterAgentResponse).</param>
/// <param name="Outcome">ran, not-own-agent, aw-meta-missing, meta-lane, lane-inbox-failed, lane-reply-failed or lane-stop-check-failed.</param>
public sealed record LaneHookTrace(DateTimeOffset At, string Hook, string Event, string Outcome);
