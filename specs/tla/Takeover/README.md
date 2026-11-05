models: src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/StateTimers.cs:57 src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/Suspensions.cs src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/TransitionDecision.EngineTriggers.cs src/Sindri/Aw.Sindri.Workflows/TimerScopes.cs
# Takeover
Bounds: 1 instance, 2 recipe states (state 1 has a 3-tick timeout, state 2 none), up to 2 takeovers (a start taken over counts), up to 2 parks, timer 0..3 or none, weak fairness on Elapse, Fire and Complete. About 550 states.
- TypeOK: every variable keeps its type and range.
- TimerStoppedWhileTakenOver: while you have an item taken over, its state timer doesn't run.
- NoTimeGained: handing an item back never gives its timer more time than it had when you took it over.
- ParkSurvivesTakeover: a park that arrives (or was already there) during a takeover is still there after the handback; the instance never goes back to active with that park lost.
- NoTimerOnPark: while an item waits on a park, no state timer runs, including right after a handback to that park.
- AtMostOneSuspension: one current suspension, and parks queue only behind it.
- EndedIsFinal: after a cancel or the terminal state, no timer runs, nothing is suspended and nothing changes.
- IdleNeverHandsBack: the 2 h takeoverIdle reminder only notifies; it never ends a takeover.
- HandbackResumes: a handback that resumes a timer is followed by the step completing, the timer firing, or the instance being parked, taken over again or cancelled.
Vars: tmr = ticks left on the running state timer (-1 = no timer) · kept = ticks the takeover paused (-1 = it kept none) · susp = none, park or takeover · pend = parks queued behind the current suspension · idle = the takeoverIdle reminder (off, armed, fired) · retried = the entry's one retry is used
Actions: Takeover = TakeoverRequested · Handback = HandbackRequested · Answer = AnswerGiven · Complete = StepCompleted · Cancel = CancelRequested · Fire = DeadlineFired DeadlineKind.State DeadlineKind.Retry · IdleFire = DeadlineFired DeadlineKind.TakeoverIdle · ParkArrives = (a park from any other trigger, no single C# trigger) · Elapse = (time passing, no C# trigger)
Leaves out: Temporal replay, the ledger mirror, desk timeouts, the approved flag and same-kind park absorption, the timer kind (state or retry) a paused timer keeps, the recipe triggers that move an instance between states (RecipeWorkflow/ models them), more than one instance.

## Known gaps (`gaps/*.cfg`, run as witnesses: `tlc-check.sh --witnesses` expects each to fail on the invariant its line 1 `\* witness: <Name>` names)

| Config | Fails | What TLC shows |
|---|---|---|
| `gaps/FullTimeoutOnHandback.cfg` | `NoTimeGained` | A handback that arms the full timeout even though the takeover paused a running timer: start, one tick, takeover (2 left), handback (3 left). 4 states. Needs the `Elapse` action: without it the timer is always full when paused and the bug is invisible. |
| `gaps/PauseKeepsFull.cfg` | `NoTimeGained` | A takeover that keeps the state's full timeout instead of the ticks left (`TimerScopes.Pause`): start, one tick, takeover (keeps 3, had 2), handback (3 left). 4 states. Also needs `Elapse`. |

Run: `scripts/tlc-check.sh --witnesses specs/tla/Takeover`.
