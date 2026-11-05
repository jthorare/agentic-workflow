------------------------------ MODULE Takeover ------------------------------
(* The owner taking an instance over and handing it back, against the state timer, the parks and the cancel.
   One instance on a recipe of two active states. State 1 has a timeout (MaxTime ticks), state 2 has none.
   Code: StateTimers.Takeovers / HandedBack / Entries, Suspensions.TakeOver / Cover / EndCurrent / Cancel,
   TimerScopes.Pause / Resume / Held, RecipeWorkflow.Enter / First.  See README.md. *)
EXTENDS Integers

CONSTANTS
    MaxTime,           \* the full timeout of state 1, in ticks
    MaxTakeovers,      \* takeovers per run (a start taken over counts)
    MaxParks,          \* parks the engine raises per run (a timeout park does not count)
    GapFullTimeout     \* TRUE plants the bug: a handback arms the full timeout even when a running timer was paused

NoTimer == -1   \* "no timer": an integer, because TLC refuses to compare a number with a string

VARIABLES
    st,         \* recipe state: 1 or 2
    susp,       \* the current suspension: "none", "park" or "takeover"
    pend,       \* parks queued behind the current suspension (a takeover covers the park it finds at the front)
    tmr,        \* the running state timer: ticks left, or NoTimer
    kept,       \* what the takeover paused (Suspension.Remaining): ticks, or NoTimer
    idle,       \* the 2 h takeoverIdle reminder: "off", "armed" or "fired"
    retried,    \* the one retry of this entry has been used
    ended,      \* cancelled or terminal
    nTake, nPark,
    held,       \* ghost: parks not yet answered, wherever they sit
    ceiling,    \* ghost: the ticks a takeover paused with, until the next entry, park, fire or answer
    resumed     \* ghost: a handback re-armed a timer and nothing has happened to it since

vars == <<st, susp, pend, tmr, kept, idle, retried, ended, nTake, nPark, held, ceiling, resumed>>

Full(s) == IF s = 1 THEN MaxTime ELSE NoTimer
Ticks(x) == x >= 0

TypeOK ==
    /\ st \in {1, 2}
    /\ susp \in {"none", "park", "takeover"}
    /\ pend \in 0..(MaxParks + 1)
    /\ tmr \in -1..MaxTime
    /\ kept \in -1..MaxTime
    /\ idle \in {"off", "armed", "fired"}
    /\ retried \in BOOLEAN
    /\ ended \in BOOLEAN
    /\ nTake \in 0..MaxTakeovers
    /\ nPark \in 0..MaxParks
    /\ held \in 0..(MaxParks + 1)
    /\ ceiling \in -1..MaxTime
    /\ resumed \in BOOLEAN

\* A fresh run: starts in state 1 with its full timeout, or taken over (RecipeWorkflow.First keeps the full timeout
\* on the suspension for the handback and arms takeoverIdle).
Init ==
    /\ st = 1 /\ pend = 0 /\ retried = FALSE /\ ended = FALSE /\ nPark = 0 /\ held = 0 /\ ceiling = NoTimer /\ resumed = FALSE
    /\ \/ /\ susp = "none" /\ tmr = Full(1) /\ kept = NoTimer /\ idle = "off" /\ nTake = 0
       \/ /\ susp = "takeover" /\ tmr = NoTimer /\ kept = Full(1) /\ idle = "armed" /\ nTake = 1

\* TakeoverRequested -> Suspensions.TakeOver / Cover; StateTimers.Takeovers (false,true) -> Pause.
Takeover ==
    /\ ~ended /\ susp # "takeover" /\ nTake < MaxTakeovers
    /\ nTake' = nTake + 1
    /\ susp' = "takeover"
    /\ idle' = "armed"
    /\ tmr' = NoTimer
    /\ resumed' = FALSE
    /\ IF susp = "none"
          THEN /\ kept' = tmr
               /\ ceiling' = tmr
               /\ pend' = pend
          ELSE /\ kept' = NoTimer            \* a park runs no timer: nothing to pause
               /\ ceiling' = NoTimer
               /\ pend' = pend + 1           \* the covered park joins the front of the queue
    /\ UNCHANGED <<st, retried, ended, nPark, held>>

\* HandbackRequested -> Suspensions.End -> EndCurrent; StateTimers.Takeovers (true,false) / HandedBack.
Handback ==
    /\ ~ended /\ susp = "takeover"
    /\ idle' = "off"
    /\ IF pend > 0
          THEN /\ susp' = "park" /\ pend' = pend - 1     \* back to the park the takeover covered or a later one
               /\ tmr' = NoTimer /\ kept' = NoTimer /\ ceiling' = NoTimer /\ resumed' = FALSE
          ELSE /\ susp' = "none" /\ pend' = pend
               /\ kept' = NoTimer
               /\ IF Ticks(kept) /\ ~GapFullTimeout
                     THEN /\ tmr' = kept                  \* resume what was left
                          /\ ceiling' = ceiling /\ resumed' = TRUE
                     ELSE /\ tmr' = Full(st)              \* nothing was kept (or the planted bug): the full timeout
                          /\ ceiling' = IF Ticks(kept) THEN ceiling ELSE NoTimer
                          /\ resumed' = Ticks(Full(st)) /\ Ticks(kept)
    /\ UNCHANGED <<st, retried, ended, nTake, nPark, held>>

\* A park arrives (a child, a gate, a drift ...): the current one when nothing is suspended, else it queues.
ParkArrives ==
    /\ ~ended /\ nPark < MaxParks
    /\ nPark' = nPark + 1 /\ held' = held + 1
    /\ IF susp = "none"
          THEN /\ susp' = "park" /\ tmr' = NoTimer /\ ceiling' = NoTimer /\ resumed' = FALSE /\ pend' = pend
          ELSE /\ UNCHANGED <<susp, tmr, ceiling, resumed>> /\ pend' = pend + 1
    /\ UNCHANGED <<st, kept, idle, retried, ended, nTake>>

\* AnswerGiven on a park -> EndCurrent: the next queued park, else active again with the full timeout (Rearm).
Answer ==
    /\ ~ended /\ susp = "park"
    /\ held' = held - 1
    /\ IF pend > 0
          THEN /\ pend' = pend - 1 /\ UNCHANGED <<susp, tmr>>
          ELSE /\ susp' = "none" /\ tmr' = Full(st) /\ pend' = pend
    /\ ceiling' = NoTimer /\ resumed' = FALSE
    /\ UNCHANGED <<st, kept, idle, retried, ended, nTake, nPark>>

\* StepCompleted: state 1 moves to state 2 (a new entry, full timeout of state 2), state 2 reaches the terminal state.
Complete ==
    /\ ~ended /\ susp = "none"
    /\ ceiling' = NoTimer /\ resumed' = FALSE /\ retried' = FALSE
    /\ IF st = 1
          THEN /\ st' = 2 /\ tmr' = Full(2) /\ ended' = FALSE /\ idle' = idle
          ELSE /\ st' = st /\ tmr' = NoTimer /\ ended' = TRUE /\ idle' = "off"
    /\ UNCHANGED <<susp, pend, kept, nTake, nPark, held>>

\* CancelRequested: ends the instance wherever it is, a takeover included; every timer stops.
Cancel ==
    /\ ~ended
    /\ ended' = TRUE /\ susp' = "none" /\ pend' = 0 /\ tmr' = NoTimer /\ kept' = NoTimer /\ idle' = "off"
    /\ held' = 0 /\ ceiling' = NoTimer /\ resumed' = FALSE
    /\ UNCHANGED <<st, retried, nTake, nPark>>

\* Time passing: a running timer loses a tick unless the instance is taken over or suspended (its timer is stopped then).
Elapse ==
    /\ ~ended /\ susp = "none" /\ tmr \in 1..MaxTime
    /\ tmr' = tmr - 1
    /\ UNCHANGED <<st, susp, pend, kept, idle, retried, ended, nTake, nPark, held, ceiling, resumed>>

\* DeadlineFired (state/retry) -> Expired: the one retry re-arms the full timeout, else the instance parks (timeout park).
Fire ==
    /\ ~ended /\ susp = "none" /\ tmr = 0
    /\ ceiling' = NoTimer /\ resumed' = FALSE
    /\ \/ /\ ~retried /\ retried' = TRUE /\ tmr' = Full(st) /\ UNCHANGED <<susp, held>>
       \/ /\ susp' = "park" /\ tmr' = NoTimer /\ held' = held + 1 /\ UNCHANGED retried
    /\ UNCHANGED <<st, pend, kept, idle, ended, nTake, nPark>>

\* DeadlineFired takeoverIdle -> Idle: notifies once and changes nothing else.
IdleFire ==
    /\ ~ended /\ susp = "takeover" /\ idle = "armed"
    /\ idle' = "fired"
    /\ UNCHANGED <<st, susp, pend, tmr, kept, retried, ended, nTake, nPark, held, ceiling, resumed>>

Done == ended /\ UNCHANGED vars

Next == Takeover \/ Handback \/ ParkArrives \/ Answer \/ Complete \/ Cancel \/ Elapse \/ Fire \/ IdleFire \/ Done

Spec == Init /\ [][Next]_vars /\ WF_vars(Elapse) /\ WF_vars(Fire) /\ WF_vars(Complete)

-----------------------------------------------------------------------------
TimerStoppedWhileTakenOver == susp = "takeover" => tmr = NoTimer

\* After a handback that resumed a paused timer, no more ticks than it had when it paused.
NoTimeGained == (susp = "none" /\ Ticks(ceiling) /\ Ticks(tmr)) => tmr <= ceiling

\* A park that arrives during a takeover is still held after the handback: held parks = queued + the current one.
ParkSurvivesTakeover ==
    /\ held = pend + (IF susp = "park" THEN 1 ELSE 0)
    /\ (~ended /\ susp = "none") => held = 0

NoTimerOnPark == susp = "park" => tmr = NoTimer

\* One current suspension, and parks queue only behind it.
AtMostOneSuspension == (susp = "none" => pend = 0) /\ susp \in {"none", "park", "takeover"}

EndedHasNoTimers ==
    ended => (tmr = NoTimer /\ susp = "none" /\ idle = "off" /\ pend = 0 /\ kept = NoTimer)

IdleOnlyWhileTakenOver == idle # "off" => susp = "takeover"

EndedIsFinal == [][ended => UNCHANGED vars]_vars

\* The reminder never ends the takeover.
IdleNeverHandsBack == [][IdleFire => susp' = "takeover"]_vars

\* A handback that resumed a timer is followed by the step completing, the timer firing, or something else moving
\* the instance on (with the step and the timer weakly fair).
HandbackResumes == resumed ~> ~resumed
=============================================================================
