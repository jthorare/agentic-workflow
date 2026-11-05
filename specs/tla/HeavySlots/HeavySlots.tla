------------------------------ MODULE HeavySlots ------------------------------
(***************************************************************************)
(* The box-wide heavy-job slots: <locks>/heavy-job.lock (slot 1) and       *)
(* heavy-job.lock.k (slot k), mkdir mutexes shared by three takers.        *)
(*                                                                         *)
(* Models:                                                                 *)
(*   scripts/heavy.sh  (the `heavy` wrapper)  acquire box-wide: the cap   *)
(*       file read once per job, the meta reserve re-read every pass,      *)
(*       mkdir, then the owner write; reclaim moves owner aside first,     *)
(*       like locks.sh                                                     *)
(*   config/lib/locks.sh  acquire_any_lock: AW_HEAVY_SLOTS (default 2),    *)
(*       never the cap file or the reserve; <dir>/pid; reclaim moves the   *)
(*       record aside first (_lock_reclaim_dead)                           *)
(*   src/Sindri/Aw.Sindri.Infrastructure/Locks/DirectoryHeavySlots.cs      *)
(*       the cap read once at serve start (HeavyLockSettings singleton),   *)
(*       never the reserve; <dir>/owner; reclaim renames the record aside  *)
(*       (Claim), then a non-recursive delete; HeavySlotLease leaves a     *)
(*       slot whose owner names someone else                               *)
(*                                                                         *)
(* Each syscall (mkdir, rmdir, unlink, a write, a rename) is one step.     *)
(* A slot holds at most one record file (rec); "pid" is locks.sh's,        *)
(* "owner" the wrapper's and Sindri's, "reclaim" a record a reclaimer      *)
(* moved aside (<file>.reclaim.<its pid>...; by = that reclaimer).  The    *)
(* move and the compare are two steps: a reclaimer SIGKILLed between      *)
(* them leaves the moved record, which any taker frees once its mover is  *)
(* dead (#2065).  A folder with no record is freed by any taker once its  *)
(* mkdir-er is dead (#2059; the code waits a minute).  A process dies by SIGKILL at any step: its folder and    *)
(* record stay, and its pid is never reused.  Not modelled: the per-session*)
(* heavy-session/<pid>.lock (taken first, a separate pool), the memory     *)
(* guard (it only skips slots), timeouts, the 6 h held-too-long grace of  *)
(* DirectoryHeavySlots and the TS holder.json.                             *)
(***************************************************************************)
EXTENDS Naturals, FiniteSets

CONSTANTS
    Heavy,        \* processes running the `heavy` wrapper
    Locksh,       \* processes using config/lib/locks.sh acquire_any_lock
    Sindri,       \* Sindri serve jobs (DirectoryHeavySlots)
    Meta,         \* the Heavy processes with AW_HEAVY_PRIORITY=meta
    CapMax,       \* how many slot folders exist in the model (heavy-job.lock .. .CapMax)
    CapInit,      \* the heavy-slots-cap file at the start (and what Sindri read at serve start)
    LockshSlots,  \* AW_HEAVY_SLOTS as locks.sh sees it
    ReserveInit,  \* the heavy-meta-reserve file at the start
    MaxDeaths,    \* SIGKILLs in one behaviour
    CanSetCap,    \* the owner may rewrite the cap file
    CanSetReserve \* meta may rewrite the reserve file

Procs == Heavy \cup Locksh \cup Sindri
Slot == 1..CapMax
None == "none"
NoRec == [file |-> "none", who |-> None, ofile |-> "none", by |-> None]
\* A folder with no record that was made or emptied within the last minute: a holder may be mid-write, or a
\* reclaimer mid-rmdir. Only the folder's mtime tells; Age is the minute passing. The empty-folder rule skips it.
YoungNone == [file |-> "none", who |-> None, ofile |-> "none", by |-> "young"]

ASSUME /\ Meta \subseteq Heavy
       /\ Heavy \cap Locksh = {} /\ Heavy \cap Sindri = {} /\ Locksh \cap Sindri = {}
       /\ CapInit \in Slot /\ LockshSlots \in Slot /\ ReserveInit \in 0..CapMax

VARIABLES
    dir,      \* dir[k]: the folder of slot k exists
    rec,      \* rec[k]: its record file
    pc,       \* each process's step
    at,       \* the slot a process is trying (or holds, after its mkdir)
    seen,     \* the dead record a reclaimer read
    myCap,    \* the slot count each process loops over
    myRes,    \* the reserve the wrapper read this pass
    holds,    \* the slot a process believes it holds (0: none), set by its winning mkdir
    alive,
    cap,      \* the heavy-slots-cap file
    reserve,  \* the heavy-meta-reserve file
    deaths,
    capBreak, \* history: some process took a slot above the cap file at that moment
    resBreak  \* history: a non-meta process took a slot inside the reserve it should honour

vars == <<dir, rec, pc, at, seen, myCap, myRes, holds, alive, cap, reserve, deaths, capBreak, resBreak>>

Min(a, b) == IF a < b THEN a ELSE b
FileOf(p) == IF p \in Locksh THEN "pid" ELSE "owner"
\* ofile: the record's own name before a move (its file otherwise); by: who moved it aside.
Rec == [file: {"none", "pid", "owner", "reclaim"}, who: Procs \cup {None}, ofile: {"none", "pid", "owner"}, by: Procs \cup {None, "young"}]
RecOf(p) == [file |-> FileOf(p), who |-> p, ofile |-> FileOf(p), by |-> None]
PCs == {"idle", "try", "claim", "c_check", "c_rmdir", "mkdir2", "write", "run", "rel1", "rel2"}

TypeOK ==
    /\ dir \in [Slot -> BOOLEAN]
    /\ rec \in [Slot -> Rec]
    /\ pc \in [Procs -> PCs]
    /\ at \in [Procs -> 1..(CapMax + 1)]
    /\ seen \in [Procs -> Rec]
    /\ holds \in [Procs -> 0..CapMax]
    /\ alive \in [Procs -> BOOLEAN]
    /\ cap \in Slot /\ reserve \in 0..CapMax

Init ==
    /\ dir = [k \in Slot |-> FALSE]
    /\ rec = [k \in Slot |-> NoRec]
    /\ pc = [p \in Procs |-> "idle"]
    /\ at = [p \in Procs |-> 1]
    /\ seen = [p \in Procs |-> NoRec]
    /\ myCap = [p \in Procs |-> IF p \in Locksh THEN LockshSlots ELSE CapInit]
    /\ myRes = [p \in Procs |-> 0]
    /\ holds = [p \in Procs |-> 0]
    /\ alive = [p \in Procs |-> TRUE]
    /\ cap = CapInit
    /\ reserve = ReserveInit
    /\ deaths = 0
    /\ capBreak = FALSE
    /\ resBreak = FALSE

\* The reserve a process honours when it takes slot k: the wrapper's per-pass read; locks.sh and Sindri never read it.
Honours(p) == IF p \in Heavy THEN myRes[p] ELSE reserve

\* p's mkdir of slot k succeeded.
Take(p, k) ==
    /\ dir' = [dir EXCEPT ![k] = TRUE]
    /\ holds' = [holds EXCEPT ![p] = k]
    /\ at' = [at EXCEPT ![p] = k]
    /\ pc' = [pc EXCEPT ![p] = "write"]
    /\ capBreak' = (capBreak \/ k > cap)
    /\ resBreak' = (resBreak \/ (p \notin Meta /\ k <= Honours(p)))

Next1(p) == /\ at' = [at EXCEPT ![p] = at[p] + 1]
            /\ pc' = [pc EXCEPT ![p] = "try"]
            /\ UNCHANGED <<dir, holds, capBreak, resBreak>>

Same == UNCHANGED <<cap, reserve, deaths, alive, myCap>>

\* A job starts: the wrapper reads the cap file and the reserve; the others keep what they have.
Start(p) ==
    /\ pc[p] = "idle"
    /\ pc' = [pc EXCEPT ![p] = "try"]
    /\ at' = [at EXCEPT ![p] = 1]
    /\ myCap' = IF p \in Heavy THEN [myCap EXCEPT ![p] = cap] ELSE myCap
    /\ myRes' = IF p \in Heavy THEN [myRes EXCEPT ![p] = Min(reserve, cap)] ELSE myRes
    /\ UNCHANGED <<dir, rec, seen, holds, alive, cap, reserve, deaths, capBreak, resBreak>>

\* Every slot tried and busy: sleep 1 and start over (the wrapper re-reads the reserve, not the cap).
Poll(p) ==
    /\ pc[p] = "try" /\ at[p] > myCap[p]
    /\ at' = [at EXCEPT ![p] = 1]
    /\ myRes' = IF p \in Heavy THEN [myRes EXCEPT ![p] = Min(reserve, myCap[p])] ELSE myRes
    /\ UNCHANGED <<dir, rec, pc, seen, holds, capBreak, resBreak>> /\ Same

\* Which record a taker reads as proof of a dead holder: every taker reads both (#2059).
Reads(p, r) == r.file \in {"pid", "owner"}

\* A dead holder's record, or a record moved aside by a reclaimer that was SIGKILLed before it finished (#2065). A moved
\* record whose mover lives is left alone: the mover is mid-reclaim.
Dead(p, r) == \/ Reads(p, r) /\ ~alive[r.who]
              \/ r.file = "reclaim" /\ ~alive[r.by]

Try(p) ==
    LET k == at[p] IN
    /\ pc[p] = "try" /\ k <= myCap[p]
    /\ IF p \in Heavy /\ p \notin Meta /\ k <= myRes[p]
          THEN Next1(p) /\ UNCHANGED <<rec, seen, myRes>>                        \* reserved for meta: skip
       ELSE IF ~dir[k]
          THEN Take(p, k) /\ rec' = [rec EXCEPT ![k] = YoungNone] /\ UNCHANGED <<seen, myRes>>
       ELSE IF rec[k] = NoRec
          \* An empty folder over two minutes old (not YoungNone): its mkdir-er is dead, or it would have written by
          \* now (Age's assumption). The code is `find -mmin +1` then rmdir (fails if a holder wrote meanwhile); modelled as one
          \* step, so a folder re-made between the two can't be removed. The code leaves that window open for
          \* a few microseconds.
          THEN /\ dir' = [dir EXCEPT ![k] = FALSE]
               /\ pc' = [pc EXCEPT ![p] = "mkdir2"]
               /\ rec' = [rec EXCEPT ![k] = YoungNone]
               /\ UNCHANGED <<at, seen, holds, myRes, capBreak, resBreak>>
       ELSE IF Dead(p, rec[k])
          THEN /\ seen' = [seen EXCEPT ![p] = rec[k]]
               /\ pc' = [pc EXCEPT ![p] = "claim"]
               /\ UNCHANGED <<dir, rec, at, holds, myRes, capBreak, resBreak>>
       ELSE Next1(p) /\ UNCHANGED <<rec, seen, myRes>>
    /\ Same

\* All three takers: move the record aside to a name of their own (mv/File.Move: only one mover of a name wins). A
\* holder record is moved by its name, whatever it holds now; a moved-aside record only while it is the one judged.
Claim(p) ==
    LET k == at[p]
        r == rec[k]
    IN
    /\ pc[p] = "claim"
    /\ IF (IF seen[p].file = "reclaim" THEN r = seen[p] ELSE r.file = seen[p].file)
          THEN /\ rec' = [rec EXCEPT ![k] = [file |-> "reclaim", who |-> r.who, ofile |-> r.ofile, by |-> p]]
               /\ pc' = [pc EXCEPT ![p] = "c_check"]
               /\ UNCHANGED <<dir, at, holds, capBreak, resBreak>>
          ELSE Next1(p) /\ UNCHANGED rec
    /\ UNCHANGED <<seen, myRes>> /\ Same

\* Re-read what was moved: the record judged dead is deleted; anything else (the slot changed hands) goes back.
CCheck(p) ==
    LET k == at[p]
        r == rec[k]
    IN
    /\ pc[p] = "c_check"
    /\ IF r.who = seen[p].who /\ r.ofile = seen[p].ofile
          THEN /\ rec' = [rec EXCEPT ![k] = YoungNone]
               /\ pc' = [pc EXCEPT ![p] = "c_rmdir"]
               /\ UNCHANGED <<dir, at, holds, capBreak, resBreak>>
          ELSE /\ rec' = [rec EXCEPT ![k] = [file |-> r.ofile, who |-> r.who, ofile |-> r.ofile, by |-> None]]
               /\ Next1(p)
    /\ UNCHANGED <<seen, myRes>> /\ Same

\* rmdir (non-recursive delete): on failure the slot is skipped.
CRmdir(p) ==
    LET k == at[p] IN
    /\ pc[p] = "c_rmdir"
    /\ IF dir[k] /\ rec[k].file = "none"
          THEN /\ dir' = [dir EXCEPT ![k] = FALSE]
               /\ pc' = [pc EXCEPT ![p] = "mkdir2"]
               /\ UNCHANGED <<at, holds, capBreak, resBreak>>
          ELSE Next1(p)
    /\ UNCHANGED <<rec, seen, myRes>> /\ Same

Mkdir2(p) ==
    LET k == at[p] IN
    /\ pc[p] = "mkdir2"
    /\ IF ~dir[k] THEN Take(p, k) /\ rec' = [rec EXCEPT ![k] = YoungNone] ELSE Next1(p) /\ UNCHANGED rec
    /\ UNCHANGED <<seen, myRes>> /\ Same

\* The record write, a separate step after the mkdir.
Write(p) ==
    /\ pc[p] = "write"
    /\ rec' = [rec EXCEPT ![holds[p]] = RecOf(p)]
    /\ pc' = [pc EXCEPT ![p] = "run"]
    /\ UNCHANGED <<dir, at, seen, myRes, holds, capBreak, resBreak>> /\ Same

Run(p) ==
    /\ pc[p] = "run"
    /\ pc' = [pc EXCEPT ![p] = "rel1"]
    /\ UNCHANGED <<dir, rec, at, seen, myRes, holds, capBreak, resBreak>> /\ Same

\* Release: remove the record file (the wrapper's unlink and locks.sh's rm -f take whatever is there; Sindri leaves another's owner), then rmdir.
Rel1(p) ==
    LET k == holds[p] IN
    /\ pc[p] = "rel1"
    /\ IF p \in Sindri /\ rec[k].file = "owner" /\ rec[k].who # p
          THEN /\ pc' = [pc EXCEPT ![p] = "idle"] /\ holds' = [holds EXCEPT ![p] = 0] /\ UNCHANGED rec
          ELSE /\ rec' = IF rec[k].file = FileOf(p) THEN [rec EXCEPT ![k] = YoungNone] ELSE rec
               /\ pc' = [pc EXCEPT ![p] = "rel2"] /\ UNCHANGED holds
    /\ UNCHANGED <<dir, at, seen, myRes, capBreak, resBreak>> /\ Same

Rel2(p) ==
    LET k == holds[p] IN
    /\ pc[p] = "rel2"
    /\ dir' = IF dir[k] /\ rec[k].file = "none" THEN [dir EXCEPT ![k] = FALSE] ELSE dir
    /\ holds' = [holds EXCEPT ![p] = 0]
    /\ pc' = [pc EXCEPT ![p] = "idle"]
    /\ UNCHANGED <<rec, at, seen, myRes, capBreak, resBreak>> /\ Same

\* A minute passes on a slot folder with no record.
\* Assumptions (a process stalled longer is not modelled: the code would free its folder under it): a live
\* reclaimer finishes its rmdir within the minute, and a live holder's folder is empty for under a minute: between
\* its mkdir and its record write, and between its release's unlink and its rmdir.
Age(k) ==
    /\ rec[k] = YoungNone
    /\ \A q \in Procs : ~(alive[q] /\ pc[q] = "c_rmdir" /\ at[q] = k)
    /\ \A q \in Procs : ~(alive[q] /\ pc[q] \in {"write", "rel2"} /\ holds[q] = k)
    /\ rec' = [rec EXCEPT ![k] = NoRec]
    /\ UNCHANGED <<dir, pc, at, seen, myCap, myRes, holds, alive, cap, reserve, deaths, capBreak, resBreak>>

Step(p) == alive[p] /\ (Start(p) \/ Poll(p) \/ Try(p) \/ Claim(p) \/ CCheck(p) \/ CRmdir(p)
                        \/ Mkdir2(p) \/ Write(p) \/ Run(p) \/ Rel1(p) \/ Rel2(p))

\* SIGKILL: nothing of p runs again; its folder and record stay.
Die(p) ==
    /\ alive[p] /\ deaths < MaxDeaths
    /\ alive' = [alive EXCEPT ![p] = FALSE]
    /\ deaths' = deaths + 1
    /\ UNCHANGED <<dir, rec, pc, at, seen, myCap, myRes, holds, cap, reserve, capBreak, resBreak>>

SetCap == /\ CanSetCap /\ cap' \in Slot \ {cap}
          /\ UNCHANGED <<dir, rec, pc, at, seen, myCap, myRes, holds, alive, reserve, deaths, capBreak, resBreak>>

SetReserve == /\ CanSetReserve /\ reserve' \in (0..CapMax) \ {reserve}
              /\ UNCHANGED <<dir, rec, pc, at, seen, myCap, myRes, holds, alive, cap, deaths, capBreak, resBreak>>

Next == (\E p \in Procs : Step(p) \/ Die(p)) \/ (\E k \in Slot : Age(k)) \/ SetCap \/ SetReserve

\* Polling, no queue: each process keeps stepping (weak fairness) ...
Spec == Init /\ [][Next]_vars /\ \A p \in Procs : WF_vars(Step(p))
             /\ \A k \in Slot : WF_vars(Age(k))

\* ... and, stronger than any poll loop promises, a process whose mkdir would win infinitely often eventually wins.
TakeStep(p) == Step(p) /\ holds[p] = 0 /\ holds'[p] # 0
SpecSF == Spec /\ \A p \in Procs : SF_vars(TakeStep(p))

----------------------------------------------------------------------------
Holders(k) == {p \in Procs : alive[p] /\ holds[p] = k}

\* No two live processes believe they hold the same slot folder.
OneHolderPerSlot == \A k \in Slot : Cardinality(Holders(k)) <= 1

\* Nobody takes a slot above the cap file as it reads at that moment (nothing running is pre-empted when it is lowered).
TakeWithinCap == ~capBreak

\* A non-meta job never takes a slot meta reserved.
ReserveHonored == ~resBreak

Waiting(p) == pc[p] \in {"try", "claim", "c_check", "c_rmdir", "mkdir2"}

\* Every live waiter eventually holds a slot.
NoStarvation == \A p \in Procs : (alive[p] /\ Waiting(p)) ~> (~alive[p] \/ holds[p] # 0)

\* The box never locks up: some job always gets to run again.
SomeoneRuns == []<>(\E p \in Procs : alive[p] /\ pc[p] = "run") \/ <>(\A p \in Procs : ~alive[p])

\* A slot folder no live process holds (its holder was SIGKILLed) is eventually freed or taken again.
Orphan(k) == dir[k] /\ Holders(k) = {}
OrphanReclaimed == \A k \in Slot : Orphan(k) ~> ~Orphan(k)

\* A record moved aside by a reclaimer that was SIGKILLed is eventually removed (#2065).
Leftover(k) == rec[k].file = "reclaim" /\ ~alive[rec[k].by]
LeftoverCleared == \A k \in Slot : Leftover(k) ~> ~Leftover(k)
=============================================================================
