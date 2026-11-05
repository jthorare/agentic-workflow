--------------------------- MODULE RecipeWorkflow ---------------------------
(* TransitionDecision.Decide on a fixed small recipe shaped like workflow.build:

     produce (active, loop "rev", cap Cap)  --StepCompleted ok [gate G]-->  checks (active)
     checks  --StepCompleted pass-->  review (wait)       checks --StepCompleted fail-->  produce   (re-enters the loop)
     review  --WaitSatisfied-->  done (terminal)          review --CommandFired reopen-->  produce   (re-enters the loop)

   Every other (state, trigger, outcome) has no transition. A trigger the engine rejects or absorbs changes nothing, so
   the spec has no action for it. Code: TransitionDecision.cs (Decide, DecideFrom, Unmatched, Unavailable),
   .Matching.cs (Allowed), .Applying.cs (Reenter), .Refusing.cs, .Answering.cs, .Rewind.cs, .Timers.cs,
   LoopRules.Reenter, RefusalRules.RefuseStep, Unavailables.MaxRetries.  See README.md. *)
EXTENDS Integers, FiniteSets

CONSTANTS
    Cap,               \* rounds of the loop (LoopSpec.Cap)
    MaxRewinds,        \* rewinds per run
    MaxTakeovers,      \* takeovers per run (coarse: the timer detail is in Takeover/)
    GapLoopOffByOne,   \* TRUE plants the bug: the loop runs one round past its cap
    GapGateFallsThrough \* TRUE plants the bug: a refused gate takes the transition anyway

VARIABLES
    st,        \* the recipe state
    susp,      \* "none", "park" or "takeover"
    why,       \* the park's reason: "rounds", "unavailable" or "timeout" ("none" without a park)
    underS, underW, \* what a takeover covers (susp and why before it)
    round,     \* the loop's current round
    retries,   \* the engine's reruns of this entry's run after unmatched StepFailed (Run.Retries)
    curT,      \* the current entry's timer is outstanding
    staleT,    \* a timer of an earlier entry or a stopped scope is still outstanding
    visited,
    nRewind, nTake,
    ended,
    via        \* ghost: how the instance last came to be in "checks"

instVars == <<st, susp, why, underS, underW, round, retries, visited, ended, via>>
vars == <<st, susp, why, underS, underW, round, retries, curT, staleT, visited, nRewind, nTake, ended, via>>

States == {"produce", "checks", "review", "done"}
Active == {"produce", "checks"}

\* LoopRules.Reenter: `Round >= Allowed` parks `rounds`. AtCap (RefusalRules.RefuseStep, RerunAfterAnswer) is the same test.
AtCap == IF GapLoopOffByOne THEN round > Cap ELSE round >= Cap

TypeOK ==
    /\ st \in States
    /\ susp \in {"none", "park", "takeover"}
    /\ why \in {"none", "rounds", "unavailable", "timeout"}
    /\ underS \in {"none", "park"} /\ underW \in {"none", "rounds", "unavailable", "timeout"}
    /\ round \in 1..(Cap + 1)
    /\ retries \in 0..2
    /\ curT \in BOOLEAN /\ staleT \in BOOLEAN
    /\ visited \subseteq Active \cup {"review", "done"}
    /\ nRewind \in 0..MaxRewinds /\ nTake \in 0..MaxTakeovers
    /\ ended \in BOOLEAN
    /\ via \in {"start", "gatePass", "rewind", "refused"}

Init ==
    /\ st = "produce" /\ susp = "none" /\ why = "none" /\ underS = "none" /\ underW = "none"
    /\ round = 1 /\ retries = 0 /\ curT = TRUE /\ staleT = FALSE /\ visited = {"produce"}
    /\ nRewind = 0 /\ nTake = 0 /\ ended = FALSE /\ via = "start"

\* StateEntry.Enter: a new entry arms its timer; the previous entry's timer is still out there.
Enter(s) ==
    /\ st' = s /\ visited' = visited \cup {s}
    /\ staleT' = (staleT \/ curT) /\ curT' = (s # "done")
    /\ retries' = 0

\* Suspensions.Suspend: the park stops the timer; a late delay of the stopped scope can still arrive.
ParkOn(w) ==
    /\ susp' = "park" /\ why' = w
    /\ staleT' = (staleT \/ curT) /\ curT' = FALSE

-----------------------------------------------------------------------------
\* StepCompleted with one of the outcomes of the open run (a stale token or an undeclared outcome is Rejected: no change).
Report(o) ==
    /\ ~ended /\ susp = "none" /\ st \in Active
    /\ \/ /\ st = "produce" /\ o = "ok"
          /\ \E g \in {"pass", "refuse"} :
               IF g = "pass"
                  THEN /\ Enter("checks") /\ via' = "gatePass"
                       /\ UNCHANGED <<susp, why, underS, underW, round, nRewind, nTake, ended>>
                  ELSE IF GapGateFallsThrough
                  THEN /\ Enter("checks") /\ via' = "refused"                  \* the planted bug
                       /\ UNCHANGED <<susp, why, underS, underW, round, nRewind, nTake, ended>>
                  ELSE IF AtCap
                  THEN /\ ParkOn("rounds")                                      \* RefuseStep: AtCap -> park rounds
                       /\ UNCHANGED <<st, underS, underW, round, retries, visited, nRewind, nTake, ended, via>>
                  ELSE /\ round' = round + 1                                    \* RefuseStep: Rerun with the next round
                       /\ UNCHANGED <<st, susp, why, underS, underW, retries, curT, staleT, visited, nRewind, nTake, ended, via>>
       \/ /\ st = "checks" /\ o = "pass"
          /\ Enter("review")
          /\ UNCHANGED <<susp, why, underS, underW, round, nRewind, nTake, ended, via>>
       \/ /\ st = "checks" /\ o = "fail"
          /\ IF ~AtCap
                THEN /\ round' = round + 1 /\ Enter("produce")
                     /\ UNCHANGED <<susp, why, underS, underW, nRewind, nTake, ended, via>>
                ELSE /\ ParkOn("rounds")
                     /\ UNCHANGED <<st, underS, underW, round, retries, visited, nRewind, nTake, ended, via>>
       \* no transition on this outcome from this state: Rejected, nothing moves
       \/ /\ ~(st = "produce" /\ o = "ok") /\ ~(st = "checks" /\ o \in {"pass", "fail"})
          /\ UNCHANGED vars

\* StepFailed with no transition: Unavailable reruns the step at most MaxRetries (2) times, then parks `unavailable`.
Fail ==
    /\ ~ended /\ susp = "none" /\ st \in Active
    /\ IF retries < 2
          THEN /\ retries' = retries + 1
               /\ UNCHANGED <<st, susp, why, underS, underW, round, curT, staleT, visited, nRewind, nTake, ended, via>>
          ELSE /\ ParkOn("unavailable")
               /\ UNCHANGED <<st, underS, underW, round, retries, visited, nRewind, nTake, ended, via>>

\* WaitSatisfied, from the review wait state only.
Wait ==
    /\ ~ended /\ susp = "none" /\ st = "review"
    /\ Enter("done") /\ ended' = TRUE
    /\ UNCHANGED <<susp, why, underS, underW, round, nRewind, nTake, via>>

\* CommandFired reopen, a recipe command from the wait state into the loop.
Command ==
    /\ ~ended /\ susp = "none" /\ st = "review"
    /\ IF ~AtCap
          THEN /\ round' = round + 1 /\ Enter("produce")
               /\ UNCHANGED <<susp, why, underS, underW, nRewind, nTake, ended, via>>
          ELSE /\ ParkOn("rounds")
               /\ UNCHANGED <<st, underS, underW, round, retries, visited, nRewind, nTake, ended, via>>

\* AnswerGiven on a park (no grant: the cap stays). Answer.Resume: a rounds park re-applies its move and parks again at the
\* cap; the others rerun the step (counting a round in a loop state, or parking `rounds` at the cap); a timeout park in a
\* wait state arms its timer again.
Answer ==
    /\ ~ended /\ susp = "park"
    /\ \/ /\ why = "rounds"
          /\ UNCHANGED vars
       \/ /\ why # "rounds" /\ st \in Active
          /\ IF st = "produce" /\ AtCap
                THEN /\ why' = "rounds" /\ UNCHANGED <<st, susp, underS, underW, round, retries, curT, staleT, visited, nRewind, nTake, ended, via>>
                ELSE /\ susp' = "none" /\ why' = "none" /\ retries' = 0
                     /\ round' = IF st = "produce" THEN round + 1 ELSE round
                     /\ curT' = TRUE /\ staleT' = (staleT \/ curT)
                     /\ UNCHANGED <<st, underS, underW, visited, nRewind, nTake, ended, via>>
       \/ /\ why # "rounds" /\ st = "review"
          /\ susp' = "none" /\ why' = "none" /\ curT' = TRUE /\ staleT' = (staleT \/ curT)
          /\ UNCHANGED <<st, underS, underW, round, retries, visited, nRewind, nTake, ended, via>>

\* CancelRequested: any live instance, parked or taken over.
Cancel ==
    /\ ~ended
    /\ ended' = TRUE /\ susp' = "none" /\ why' = "none" /\ underS' = "none" /\ underW' = "none"
    /\ staleT' = (staleT \/ curT) /\ curT' = FALSE
    /\ UNCHANGED <<st, round, retries, visited, nRewind, nTake, via>>

\* RewindRequested to a state the instance has visited: clears any park, re-enters the state, spends no loop round.
Rewind ==
    /\ ~ended /\ susp # "takeover" /\ nRewind < MaxRewinds
    /\ \E to \in (visited \cap Active) :
         /\ ~(susp = "none" /\ st = to)
         /\ susp' = "none" /\ why' = "none" /\ Enter(to) /\ via' = "rewind" /\ nRewind' = nRewind + 1
    /\ UNCHANGED <<underS, underW, round, nTake, ended>>

\* TakeoverRequested / HandbackRequested, coarse: the suspension it covers comes back; Takeover/ owns the timer detail.
Takeover ==
    /\ ~ended /\ susp # "takeover" /\ nTake < MaxTakeovers
    /\ underS' = susp /\ underW' = why /\ susp' = "takeover" /\ why' = "none" /\ nTake' = nTake + 1
    /\ UNCHANGED <<st, round, retries, curT, staleT, visited, nRewind, ended, via>>

Handback ==
    /\ ~ended /\ susp = "takeover"
    /\ susp' = underS /\ why' = underW /\ underS' = "none" /\ underW' = "none"
    /\ UNCHANGED <<st, round, retries, curT, staleT, visited, nRewind, nTake, ended, via>>

\* DeadlineFired: the current entry's timer, while nothing is suspended, parks `timeout`; a timer of an earlier entry, a stopped
\* scope, or any timer while suspended is Absorbed (Expired checks Entry, Suspension and Ended).
FireCurrent ==
    /\ ~ended /\ susp = "none" /\ curT /\ st # "done"
    /\ ParkOn("timeout")
    /\ UNCHANGED <<st, underS, underW, round, retries, visited, nRewind, nTake, ended, via>>

FireStale ==
    /\ ~ended /\ staleT
    /\ staleT' \in BOOLEAN
    /\ UNCHANGED <<st, susp, why, underS, underW, round, retries, curT, visited, nRewind, nTake, ended, via>>

Done == ended /\ UNCHANGED vars

Step == (\E o \in {"ok", "pass", "fail", "other"} : Report(o)) \/ Fail

Next == Step \/ Wait \/ Command \/ Answer \/ Cancel \/ Rewind \/ Takeover \/ Handback \/ FireCurrent \/ FireStale \/ Done

\* Fair steps (an open run eventually reports or fails) and a fair wait event; the owner's answers are not forced.
Spec == Init /\ [][Next]_vars /\ WF_vars(Step) /\ WF_vars(Wait)

-----------------------------------------------------------------------------
LoopCapHolds == round <= Cap

\* The only ways into "checks": the gate passed, or a rewind to a state the instance had visited after passing it.
GateRefusalNeverFallsThrough == st = "checks" => via \in {"gatePass", "rewind"}

NoParkWithoutReason == (susp = "park") = (why # "none")

EndedIsFinal == [][ended => UNCHANGED vars]_vars

EndedHasNoSuspension == ended => (susp = "none" /\ ~curT)

\* A timer of an earlier entry never moves the instance.
NoStaleTimer == [][FireStale => UNCHANGED instVars]_vars

\* A StepFailed with no transition reruns the step at most twice, then parks `unavailable`; it never moves the state or the round.
UnmatchedFailureRetriesThenParks ==
    /\ [][Fail => (st' = st /\ round' = round /\ ~ended')]_vars
    /\ []((susp = "park" /\ why = "unavailable") => retries = 2)

\* A StepCompleted whose outcome has no transition from this state changes nothing.
UnmatchedCompletedNeverMoves ==
    [][(\E o \in {"ok", "pass", "fail", "other"} : Report(o) /\ ~(st = "produce" /\ o = "ok") /\ ~(st = "checks" /\ o \in {"pass", "fail"}))
        => UNCHANGED vars]_vars

\* With fair steps, every instance ends or waits on a person (a park or a takeover).
EventuallySettles == <>(ended \/ susp # "none")
=============================================================================
