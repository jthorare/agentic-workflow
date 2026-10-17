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

    /// <summary>A desk or agent report for a run the engine is running itself (a block Step, DT spec §8.2): only that run's own result applies, so the report is refused (SND-WF-003) and a takeover is the way to answer it.</summary>
    EngineOwnedRun = 5,
}
