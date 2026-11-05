namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>Why a trigger matched no recipe transition (spec §3.5, §5.2).</summary>
public enum MatchMiss
{
    /// <summary>It matched a transition.</summary>
    None = 0,

    /// <summary>A report for a run that isn't the open one (SND-WF-005): records nothing.</summary>
    StaleRun = 1,

    /// <summary>The trigger isn't allowed in the current state (SND-WF-003): records nothing.</summary>
    NotAllowed = 2,

    /// <summary>Allowed, but no transition's 'when' holds: a step.failed parks with needs-answer; other triggers are refused with SND-WF-003.</summary>
    NoTransition = 3,

    /// <summary>An engine trigger (answer, takeover, handback, cancel, timer, wake, child change, open-request): the engine-owned rules decide it, not a recipe transition.</summary>
    EngineTrigger = 4,

    /// <summary>A desk or agent report for a run the engine is running itself (a block Step, DT spec §8.2): only that run's own result applies, so the report is refused (SND-WF-003). The human waits for the result, cancels, or answers the Step's desk twin once the run fails; a takeover pauses the run and a handback runs the Step again, it never lets a human answer the block run.</summary>
    EngineOwnedRun = 5,

    /// <summary>The instance is parked, escalated or taken over (SND-WF-003), so no recipe transition applies until the answer or handback ends the suspension; a child's end releases a park that mirrors it before the match.</summary>
    Suspended = 6,
}
