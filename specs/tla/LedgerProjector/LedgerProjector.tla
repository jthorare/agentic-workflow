------------------------------ MODULE LedgerProjector ------------------------------
(***************************************************************************)
(* The ledger -> views.db projector of `sindri serve` (API spec §2.3).      *)
(*                                                                         *)
(* Models:                                                                 *)
(*   src/Sindri/Aw.Sindri.Host/Server/Projector.cs        one pass, the    *)
(*       30 s stall gate, Prepare (rebuild), Fold (pages), Finish, Publish *)
(*   src/Sindri/Aw.Sindri.Infrastructure/Views/SqliteViewStore.cs          *)
(*       Open / Reset / Apply (one transaction per page, a savepoint per   *)
(*       event) / FinishRebuild                                            *)
(*   src/Sindri/Aw.Sindri.Core/Views/ProjectorProgress.cs   the published  *)
(*       status and the in-memory rebuild request                         *)
(*   src/Sindri/Aw.Sindri.Core/Health/HealthSummary.cs   the health word   *)
(*                                                                         *)
(* Each SQLite transaction is one atomic step: a crash inside one commits  *)
(* nothing, so a crash is modelled between steps.  A crash loses all of    *)
(* the projector's memory (published status, stall gate, rebuild request); *)
(* serve restarts at once with ProjectorProgress's initial status.         *)
(* Time is abstract: the 30 s StallRetry is the RetryDue step.             *)
(***************************************************************************)
EXTENDS Naturals, Sequences, FiniteSets

CONSTANTS
    MaxEvents,   \* ledger events appended in the model
    PageSize,    \* Projector.PageSize (100 in the code; small here)
    Poison,      \* the events whose fold fails (an Error or a throw) until a build that folds them is deployed
    MaxCrashes,  \* crashes (each followed by a restart) in one behaviour
    MaxRebuilds, \* `sindri views rebuild` requests in one behaviour
    CanThrow     \* TRUE: a pass may throw past its Result handling (ProjectorHost's catch); FALSE: it never does

ASSUME PageSize >= 1 /\ Poison \subseteq 1..MaxEvents /\ CanThrow \in BOOLEAN

NULL == 0   \* no event: sequences start at 1

VARIABLES
    head,       \* ledger head: events 1..head are appended (append-only, never rewritten)
    db,         \* views.db, committed: rows folded (as a sequence of event numbers) + projector_state
    pc,         \* "idle" between passes, "folding" inside one
    passHead,   \* the ledger head the pass read at its start
    gate,       \* TRUE while the 30 s stall gate holds the next pass back (_stalledSince within StallRetry)
    rebuildReq, \* ProjectorProgress._rebuildRequested (memory only)
    pub,        \* ProjectorProgress._current: the status every reader sees
    fixed,      \* TRUE once a build that folds the Poison events runs
    crashes,
    requests    \* the ledger's views.rebuilt events so far: the newest rebuild request's seq (durable, survives a crash)

vars == <<head, db, pc, passHead, gate, rebuildReq, pub, fixed, crashes, requests>>

DbState == [rows: Seq(1..MaxEvents), cursor: 0..MaxEvents, stalledAt: 0..MaxEvents, rebuilding: BOOLEAN, rebuiltThrough: 0..MaxRebuilds]
Status == [stalled: BOOLEAN, rebuilding: BOOLEAN, cursor: 0..MaxEvents]

\* ProjectorProgress's initial _current: (0, 0, Rebuilding: false, Stalled: false)
InitialStatus == [stalled |-> FALSE, rebuilding |-> FALSE, cursor |-> 0]

\* Projector.Publish(state, head) -> ViewsStatus, then ProjectorProgress.Publish (a pending request shows as rebuilding)
Shown(d, req) == [stalled |-> d.stalledAt # NULL, rebuilding |-> d.rebuilding \/ req, cursor |-> d.cursor]

Folds(e) == e \notin Poison \/ fixed

TypeOK ==
    /\ head \in 0..MaxEvents
    /\ db \in DbState
    /\ pc \in {"idle", "folding"}
    /\ passHead \in 0..MaxEvents
    /\ gate \in BOOLEAN /\ rebuildReq \in BOOLEAN /\ fixed \in BOOLEAN
    /\ pub \in Status
    /\ crashes \in 0..MaxCrashes
    /\ requests \in 0..MaxRebuilds

Init ==
    /\ head = 0
    /\ db = [rows |-> <<>>, cursor |-> 0, stalledAt |-> NULL, rebuilding |-> FALSE, rebuiltThrough |-> 0]
    /\ pc = "idle" /\ passHead = 0
    /\ gate = FALSE /\ rebuildReq = FALSE /\ fixed = FALSE
    /\ pub = InitialStatus
    /\ crashes = 0
    /\ requests = 0

\* Another writer appends to the ledger.
AppendEvent ==
    /\ head < MaxEvents
    /\ head' = head + 1
    /\ UNCHANGED <<db, pc, passHead, gate, rebuildReq, pub, fixed, crashes, requests>>

\* `sindri views rebuild` -> RebuildViewsService -> ProjectorProgress.RequestRebuild, after a views.rebuilt event is appended
\* to the ledger (requests is its seq): the request is durable, rebuildReq is only the in-memory copy.
RequestRebuild ==
    /\ requests < MaxRebuilds
    /\ requests' = requests + 1
    /\ rebuildReq' = TRUE
    /\ pub' = [pub EXCEPT !.rebuilding = TRUE]
    /\ UNCHANGED <<head, db, pc, passHead, gate, fixed, crashes>>

\* 30 s pass since the stall: the gate opens.
RetryDue ==
    /\ gate
    /\ gate' = FALSE
    /\ UNCHANGED <<head, db, pc, passHead, rebuildReq, pub, fixed, crashes, requests>>

\* Prepare resets when the in-memory request is set, or when views.db's projector_state.rebuilt_through is behind the
\* ledger's newest views.rebuilt seq (a request that survived a crash only in the ledger).
Pending == db.rebuiltThrough < requests

\* Pass: read the head, Prepare (a requested or pending rebuild resets in one transaction, recording rebuilt_through =
\* the newest views.rebuilt seq it read, and only then takes the request), then
\* Fold's first Publish. A gated pass (stalled, no request, inside 30 s) returns Stalled and changes nothing.
StartPass ==
    /\ pc = "idle"
    /\ ~gate \/ rebuildReq \/ Pending
    /\ LET reset == rebuildReq \/ Pending
           d == IF reset
                  THEN [rows |-> <<>>, cursor |-> 0, stalledAt |-> NULL, rebuilding |-> TRUE,
                        rebuiltThrough |-> requests]                                         \* Reset + ResetState, one transaction
                  ELSE db
       IN /\ db' = d
          /\ gate' = IF reset THEN FALSE ELSE gate
          /\ rebuildReq' = FALSE                                                              \* TakeRebuildRequest
          /\ pub' = Shown(d, FALSE)
    /\ passHead' = head
    /\ pc' = "folding"
    /\ UNCHANGED <<head, fixed, crashes, requests>>

\* SqliteViewStore.Apply of one page read after the cursor (ReadAfter may return events appended after passHead).
\* Events fold in order under a savepoint each; the first that fails is rolled back alone, the ones before it commit,
\* and stalled_at records it. One transaction.
FirstBad(from, to) == {e \in from..to : ~Folds(e)}

ApplyPage ==
    /\ pc = "folding"
    /\ db.cursor < passHead
    /\ LET from == db.cursor + 1
           to   == IF db.cursor + PageSize <= head THEN db.cursor + PageSize ELSE head
           bad  == FirstBad(from, to)
           stop == IF bad = {} THEN to + 1 ELSE CHOOSE b \in bad : \A x \in bad : b <= x
           good == [i \in 1..(stop - from) |-> from + i - 1]
           d    == [db EXCEPT !.rows = db.rows \o good,
                              !.cursor = stop - 1,
                              !.stalledAt = IF bad = {} THEN NULL ELSE stop]
       IN /\ db' = d
          /\ pub' = Shown(d, rebuildReq)
          /\ IF bad = {}
               THEN UNCHANGED <<pc, gate>>
               ELSE pc' = "idle" /\ gate' = TRUE                \* Stalled(): the 30 s gate starts
    /\ UNCHANGED <<head, passHead, rebuildReq, fixed, crashes, requests>>

\* Finish: the cursor reached the pass's head; a rebuild ends with FinishRebuild (its own transaction).
Finish ==
    /\ pc = "folding"
    /\ db.cursor >= passHead
    /\ LET d == [db EXCEPT !.rebuilding = FALSE] IN
         /\ db' = d
         /\ pub' = Shown(d, rebuildReq)
    /\ gate' = FALSE
    /\ pc' = "idle"
    /\ UNCHANGED <<head, passHead, rebuildReq, fixed, crashes, requests>>

\* A pass that throws (not a Result error): ProjectorHost logs it and tries again a second later. Nothing is
\* published, and the transaction in flight commits nothing.
Throw ==
    /\ CanThrow
    /\ pc = "folding"
    /\ pc' = "idle"
    /\ UNCHANGED <<head, db, passHead, gate, rebuildReq, pub, fixed, crashes, requests>>

\* serve dies and starts again (a new build may come with it). Memory is gone; views.db and the ledger stay.
CrashRestart ==
    /\ crashes < MaxCrashes
    /\ crashes' = crashes + 1
    /\ pc' = "idle" /\ gate' = FALSE /\ rebuildReq' = FALSE
    /\ pub' = InitialStatus
    /\ fixed' \in {fixed, TRUE}
    /\ UNCHANGED <<head, db, passHead, requests>>

Next ==
    \/ AppendEvent \/ RequestRebuild \/ RetryDue \/ StartPass \/ ApplyPage \/ Finish \/ Throw \/ CrashRestart

\* The projector's own steps are weakly fair (ProjectorHost loops while serve runs); the clock's 30 s always passes.
Spec ==
    /\ Init /\ [][Next]_vars
    /\ WF_vars(StartPass) /\ WF_vars(ApplyPage) /\ WF_vars(Finish) /\ WF_vars(RetryDue)

-----------------------------------------------------------------------------
(* Safety *)

\* Every folded event lands exactly once, in ledger order: the views hold events 1..cursor, each once.
ExactlyOnceInOrder ==
    /\ Len(db.rows) = db.cursor
    /\ \A i \in 1..Len(db.rows) : db.rows[i] = i

CursorWithinLedger == db.cursor <= head

\* No event the current build can't fold is ever in the views.
NoPoisonFolded == \A i \in 1..Len(db.rows) : db.rows[i] \in Poison => fixed

\* HealthSummary.For: Stalled beats Rebuilding beats Healthy (other services' problems left out).
Healthy == ~pub.stalled /\ ~pub.rebuilding

\* views.db records a stall (projector_state.stalled_at), or the views are behind an event that can't fold: the
\* projector is stalled. It must never say Healthy then.
TrulyStalled == db.stalledAt # NULL

StalledNeverHealthy == TrulyStalled => ~Healthy

-----------------------------------------------------------------------------
(* Liveness *)

\* Once nothing left can fail to fold, the views catch up with the ledger.
AllEventsLand == (\A e \in 1..MaxEvents : Folds(e)) ~> (db.cursor = head)

\* Healthy while behind means the views are moving: they catch up, or the status stops saying Healthy. A pass that
\* throws every time (ProjectorHost's catch) publishes nothing, so the last Healthy would stand forever.
HealthyMeansProgress == (Healthy /\ db.cursor < head) ~> (db.cursor = head \/ ~Healthy)

\* An accepted rebuild request eventually runs: a reset commits that covers it and the views are replayed.  Pending is
\* durable (ledger seq vs views.db), so a crash that loses rebuildReq can't satisfy this vacuously.
RebuildHappens == Pending ~> ~Pending

=============================================================================
