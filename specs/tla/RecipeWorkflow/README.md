models: src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/TransitionDecision.cs src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/TransitionDecision.Matching.cs src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/TransitionDecision.Applying.cs src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/TransitionDecision.Refusing.cs src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/TransitionDecision.Answering.cs src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/TransitionDecision.Rewind.cs src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/TransitionDecision.Timers.cs src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/LoopRules.cs src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/RefusalRules.cs src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/Unavailables.cs
# RecipeWorkflow
Bounds: one fixed recipe shaped like workflow.build (produce loops with a cap of 2, then checks, a review wait, done; one gate on produce -> checks), up to 1 rewind, up to 1 takeover, up to 2 reruns after a failed step, weak fairness on the step and the wait event. About 1.6k states.
- TypeOK: every variable keeps its type and range.
- LoopCapHolds: a loop never runs past its cap. At the cap the instance parks `rounds`, whether the next round comes from a failed check, a refused gate, a reopen command or an answer.
- GateRefusalNeverFallsThrough: a refused gate never takes the transition; the instance is in checks only after the gate passed (or a rewind back to it).
- EndedIsFinal: after a cancel or the terminal state nothing changes, nothing is parked and no timer is running.
- NoStaleTimer: a timer of an earlier entry, or of a scope a park stopped, never moves the instance.
- UnmatchedFailureRetriesThenParks: a failed step with no transition runs again at most twice, then parks `unavailable`, and never moves the state or the round.
- UnmatchedCompletedNeverMoves: a report whose outcome has no transition from the state changes nothing.
- EventuallySettles: with the step and the wait event fair, every instance ends or waits on a person (a park or a takeover).
Vars: st = recipe state · susp = none, park or takeover · why = the park's reason (rounds, unavailable, timeout) · round = the loop's current round · retries = reruns after unmatched failures · curT, staleT = a timer of this entry, or of an earlier one, is still out · via = how the instance last reached checks
Actions: Report = StepCompleted · Fail = StepFailed · Wait = WaitSatisfied · Command = CommandFired · Answer = AnswerGiven · Cancel = CancelRequested · Rewind = RewindRequested · Takeover = TakeoverRequested · Handback = HandbackRequested · FireCurrent = DeadlineFired DeadlineKind.State · FireStale = (a DeadlineFired of an earlier entry or a stopped scope, absorbed)
Leaves out: ChildChanged: the recipe has no child wait, so no parent mirrors a child · OpenRequested, LaunchRefused, RuntimeUnavailable, SessionLost: the agent launch and session path, which this recipe's steps don't take; a failed launch is StepFailed here · OutboxRefused: the recipe has no effects to refuse · RecipeWake: wakes a recipe that waits on a deferred event; this one has none · DeadlineKind.Retry: a `then: retry` timeout action; the timeout here always parks · DeadlineKind.TakeoverIdle: Takeover/ owns it · Temporal replay, the ledger mirror, desk timeouts, budgets and their parks, gates that escalate, no-progress detection, grants that raise the cap, facts and outputs, approval drift, more than one instance · the takeover's timer detail (Takeover/ models it; here a takeover only covers the suspension and a handback restores it).

## Known gaps (`gaps/*.cfg`, run as witnesses: `tlc-check.sh --witnesses` expects each to fail on the invariant its line 1 `\* witness: <Name>` names)

| Config | Fails | What TLC shows |
|---|---|---|
| `gaps/LoopCapOffByOne.cfg` | `LoopCapHolds` | The cap test is `round > Cap` instead of `round >= Cap`: a refused gate reruns the step into a third round under a cap of 2. 3 states. |
| `gaps/GateRefusalFallsThrough.cfg` | `GateRefusalNeverFallsThrough` | A refused gate takes the produce -> checks transition anyway. 2 states. |

Run: `scripts/tlc-check.sh --witnesses specs/tla/RecipeWorkflow`.
