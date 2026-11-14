--------------------------- MODULE GraphWorkflow ---------------------------
(* Graph run starts, the three concurrency modes, the end-of-run drain and cancel (issue 2258, PR 6), and a test run (mode = test, P4a): it takes no slot, queues behind nothing, blocks nothing and writes nothing outside.
   Triggers (some re-delivered with an already used run id) hit up to two workflows, each in one mode:
   "one" (one-per-workflow), "queue" or "unlimited".
   Code: GraphRunStarter.Start / Launch, SqliteWorkflowRunStore.Admit / InFlight / SetStatus, and the end-of-run drain the
   GraphWorkflow finish path owns (pr-06.card.md "Decided while building").  See README.md. *)
EXTENDS Integers, Sequences, FiniteSets

CONSTANTS
    NTrig,             \* triggers 1..NTrig
    NW1,               \* triggers 1..NW1 belong to workflow w1, the rest to w2
    Redeliver,         \* TRUE: trigger 2 re-delivers trigger 1 (same run id)
    ModeW1, ModeW2,    \* "one", "queue" or "unlimited"
    StartAtomic,       \* TRUE: the in-flight read and the insert are one step; FALSE (gap): two separate transactions
    InsertChecksId,    \* TRUE: Insert refuses a used id (SND-GRAPH-100); FALSE (gap): a blind insert
    DrainOldest,       \* TRUE: the end path starts the oldest Queued run; FALSE (gap): the newest
    LaunchFailDrains,  \* TRUE: a failed launch also starts the next Queued run; FALSE (gap): it does not
    TestTrig,          \* the one trigger that asks for a test run (mode = test, P4a, spec 2026-11-12 §3.2); 0 for none
    TestCounts,        \* FALSE: Admit counts live runs only; TRUE (gap): a test run counts, so it blocks and queues live triggers
    TestFenced         \* TRUE: a test run's write ports record and write nothing; FALSE (gap): a test run writes outside

Triggers == 1..NTrig
IsTest(t) == t = TestTrig
Wf(t) == IF t <= NW1 THEN "w1" ELSE "w2"
Id(t) == IF Redeliver /\ t = 2 THEN 1 ELSE t
Mode(w) == IF w = "w1" THEN ModeW1 ELSE ModeW2
Final == {"succeeded", "failed", "cancelled"}
Open == {"queued", "running", "waiting"}
Live == {"running", "waiting"}

VARIABLES
    rows,       \* workflow_run rows in insertion order: [id, wf, st, started, launched, mode, wrote]
    pc,         \* per trigger: "idle", "insert" (read done, insert pending) or "done"
    bz,         \* per trigger: what its in-flight read saw (another run in flight)
    skipped, refused, drained   \* ghosts for the reachability witnesses

vars == <<rows, pc, bz, skipped, refused, drained>>

Rows == 1..Len(rows)
OpenOf(rs, w) == {i \in 1..Len(rs) : rs[i].wf = w /\ rs[i].st \in Open}
QueuedOf(rs, w) == {i \in 1..Len(rs) : rs[i].wf = w /\ rs[i].st = "queued"}
Pick(S) == IF DrainOldest THEN CHOOSE i \in S : \A j \in S : i <= j
           ELSE CHOOSE i \in S : \A j \in S : i >= j

\* The end path: in queue mode the oldest Queued run of the workflow starts (Running, not yet launched) once no live run of it runs.
AfterEnd(rs, w) ==
    IF Mode(w) = "queue" /\ QueuedOf(rs, w) # {}
       /\ ~(\E i \in 1..Len(rs) : rs[i].wf = w /\ rs[i].mode = "live" /\ rs[i].st \in Live)   \* PromoteOldestQueued: only when no live run is running or waiting (a test run ending frees nothing)
    THEN LET j == Pick(QueuedOf(rs, w))
         IN [rs EXCEPT ![j].st = "running", ![j].started = TRUE]
    ELSE rs

TypeOK ==
    /\ Len(rows) <= NTrig
    /\ \A i \in Rows : rows[i].st \in Open \cup Final /\ rows[i].started \in BOOLEAN /\ rows[i].launched \in BOOLEAN
                      /\ rows[i].mode \in {"live", "test"} /\ rows[i].wrote \in BOOLEAN
    /\ pc \in [Triggers -> {"idle", "insert", "done"}]
    /\ bz \in [Triggers -> BOOLEAN]

Init ==
    /\ rows = <<>>
    /\ pc = [t \in Triggers |-> "idle"]
    /\ bz = [t \in Triggers |-> FALSE]
    /\ skipped = FALSE /\ refused = FALSE /\ drained = FALSE

\* What GraphRunStarter.Start reads: another run of the workflow in flight (queued, running or waiting; SqliteWorkflowRunStore.Admit,
\* WorkflowRunSql.InFlight). The same id in flight is a re-delivery, so it is not "another run" (line 45). Unlimited never reads.
\* A test run takes no slot and queues behind nothing, and a live trigger counts live rows only (WorkflowRunSql: the admit path filters mode = live).
Busy(t) == /\ Mode(Wf(t)) # "unlimited"
           /\ TestCounts \/ ~IsTest(t)
           /\ \E i \in OpenOf(rows, Wf(t)) : rows[i].id # Id(t) /\ (TestCounts \/ rows[i].mode = "live")

\* SqliteWorkflowRunStore.Decide: one-per-workflow skips while a run is in flight.
Skips(t, b) == Mode(Wf(t)) = "one" /\ b

\* SqliteWorkflowRunStore.Admit: a used id is refused in the transaction; else the row is appended.
\* Queued when busy (queue mode), else Running and awaiting its launch (Decide, SqliteWorkflowRunStore.cs:176).
Insert(t, b) ==
    IF InsertChecksId /\ \E i \in Rows : rows[i].id = Id(t)
    THEN /\ rows' = rows /\ refused' = TRUE
    ELSE /\ rows' = Append(rows, [id |-> Id(t), wf |-> Wf(t), st |-> IF b THEN "queued" ELSE "running",
                                  started |-> ~b, launched |-> FALSE,
                                  mode |-> IF IsTest(t) THEN "test" ELSE "live", wrote |-> FALSE])
         /\ refused' = refused

\* The whole Start in one step (StartAtomic): read InFlight and Insert together.
StartAtomic1(t) ==
    /\ StartAtomic /\ pc[t] = "idle"
    /\ pc' = [pc EXCEPT ![t] = "done"]
    /\ IF Skips(t, Busy(t))
       THEN /\ skipped' = TRUE /\ UNCHANGED <<rows, refused>>
       ELSE /\ Insert(t, Busy(t)) /\ skipped' = skipped
    /\ UNCHANGED <<bz, drained>>

\* The planted gap (StartAtomic = FALSE): the in-flight read is its own transaction and the insert comes later, in another one.
\* The code does both in one Admit call.
ReadInFlight(t) ==
    /\ ~StartAtomic /\ pc[t] = "idle"
    /\ bz' = [bz EXCEPT ![t] = Busy(t)]
    /\ IF Skips(t, Busy(t)) THEN pc' = [pc EXCEPT ![t] = "done"] /\ skipped' = TRUE
       ELSE pc' = [pc EXCEPT ![t] = "insert"] /\ skipped' = skipped
    /\ UNCHANGED <<rows, refused, drained>>

InsertAfterRead(t) ==
    /\ ~StartAtomic /\ pc[t] = "insert"
    /\ pc' = [pc EXCEPT ![t] = "done"]
    /\ Insert(t, bz[t])
    /\ UNCHANGED <<bz, skipped, drained>>

\* GraphRunStarter.Launch (GraphRunStarter.cs:56): the launcher starts the Temporal workflow, or fails. A failed launch marks the run Failed
\* (a run that never started has no end path of its own, so the drain only happens here when LaunchFailDrains).
Launch(i) ==
    /\ i \in Rows /\ rows[i].st = "running" /\ ~rows[i].launched
    /\ \/ /\ rows' = [rows EXCEPT ![i].launched = TRUE] /\ drained' = drained
       \/ LET failed == [rows EXCEPT ![i].st = "failed"]
              w == rows[i].wf
          IN /\ rows' = IF LaunchFailDrains THEN AfterEnd(failed, w) ELSE failed
             /\ drained' = (drained \/ (LaunchFailDrains /\ Mode(w) = "queue" /\ QueuedOf(failed, w) # {}))
    /\ UNCHANGED <<pc, bz, skipped, refused>>

\* A launched run ends (succeeded, failed or cancelled; cancel of a running run is the workflow's own end) and the end path
\* starts the next queued run.
End(i, f) ==
    /\ i \in Rows /\ rows[i].st \in Live /\ rows[i].launched /\ f \in Final
    /\ LET ended == [rows EXCEPT ![i].st = f]
           w == rows[i].wf
       IN /\ rows' = AfterEnd(ended, w)
          /\ drained' = (drained \/ (Mode(w) = "queue" /\ QueuedOf(ended, w) # {}))
    /\ UNCHANGED <<pc, bz, skipped, refused>>

\* A run writes outside (notify, create_item, http POST, ...). A test run's ports are fenced: they record and write nothing.
Write(i) ==
    /\ i \in Rows /\ rows[i].st = "running" /\ rows[i].launched
    /\ rows' = [rows EXCEPT ![i].wrote = (rows[i].mode = "live" \/ ~TestFenced)]
    /\ UNCHANGED <<pc, bz, skipped, refused, drained>>

WaitFor(i) ==
    /\ i \in Rows /\ rows[i].st = "running" /\ rows[i].launched
    /\ rows' = [rows EXCEPT ![i].st = "waiting"]
    /\ UNCHANGED <<pc, bz, skipped, refused, drained>>

Resume(i) ==
    /\ i \in Rows /\ rows[i].st = "waiting"
    /\ rows' = [rows EXCEPT ![i].st = "running"]
    /\ UNCHANGED <<pc, bz, skipped, refused, drained>>

\* Cancel of a Queued run: it never started, so nothing drains.
CancelQueued(i) ==
    /\ i \in Rows /\ rows[i].st = "queued"
    /\ rows' = [rows EXCEPT ![i].st = "cancelled"]
    /\ UNCHANGED <<pc, bz, skipped, refused, drained>>

Done == /\ \A t \in Triggers : pc[t] = "done"
        /\ \A i \in Rows : rows[i].st \in Final
        /\ UNCHANGED vars

Next ==
    \/ \E t \in Triggers : StartAtomic1(t) \/ ReadInFlight(t) \/ InsertAfterRead(t)
    \/ \E i \in 1..NTrig : Launch(i) \/ Write(i) \/ WaitFor(i) \/ Resume(i) \/ CancelQueued(i)
    \/ \E i \in 1..NTrig, f \in Final : End(i, f)
    \/ Done

\* Fairness: a step that stays possible happens (the starter, launcher and workflow all keep running); cancel and wait are choices.
Spec == Init /\ [][Next]_vars
        /\ \A t \in Triggers : WF_vars(StartAtomic1(t)) /\ WF_vars(ReadInFlight(t)) /\ WF_vars(InsertAfterRead(t))
        /\ \A i \in 1..NTrig : WF_vars(Launch(i)) /\ WF_vars(Resume(i)) /\ WF_vars(\E f \in Final : End(i, f))

NoDuplicateRunIds == \A i, j \in Rows : i # j => rows[i].id # rows[j].id

\* In flight = running or waiting (a Queued run waits for its turn).
AtMostOneInFlight ==
    \A w \in {"w1", "w2"} : Mode(w) # "unlimited" =>
        Cardinality({i \in Rows : rows[i].wf = w /\ rows[i].mode = "live" /\ rows[i].st \in Live}) <= 1

\* No run starts while an older one of its workflow is still Queued.
QueuedStartFIFO ==
    \A i, j \in Rows : (Mode(rows[j].wf) = "queue" /\ rows[i].wf = rows[j].wf /\ i < j /\ rows[j].started)
                       => rows[i].st # "queued"

QueuedLeavesQueued ==
    \A i \in 1..NTrig : (i <= Len(rows) /\ rows[i].st = "queued") ~> (i > Len(rows) \/ rows[i].st # "queued")

\* A test run makes no outside effect (gaps/TestWrites.cfg).
TestRunWritesNothing == \A i \in Rows : rows[i].mode = "test" => ~rows[i].wrote

\* A live run is never Queued behind a test run: something live is in flight in its workflow (gaps/TestBlocksLive.cfg).
LiveNeverWaitsOnTest ==
    \A i \in Rows : (rows[i].mode = "live" /\ rows[i].st = "queued") =>
        \E j \in Rows : j # i /\ rows[j].wf = rows[i].wf /\ rows[j].mode = "live" /\ rows[j].st \in Open

\* Reachability witnesses (gaps/Reach*.cfg): each must fail, or the model never reaches the case it protects.
NeverDrains == ~drained
NeverSkips == ~skipped
NeverRefuses == ~refused
=============================================================================
