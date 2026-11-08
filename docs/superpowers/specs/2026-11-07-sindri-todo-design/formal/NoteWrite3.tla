---- MODULE NoteWrite3 ----
\* Spec rev 3 §4.5 "Writing": Sindri's note service vs Obsidian saving into the same note.
\* Files are inodes (cont[i] = set of edit tokens inside). Paths point at inodes: noteI (the note), tmpI (the dot tmp file,
\* 0 = absent), bakI (hard-link backup, 0 = absent; only the link fallback uses it).
\* Mode "swap"  : rev 3 as written: build tmp, RENAME_EXCHANGE, hash what came out, merge+rebuild if it differs, delete.
\* Mode "naive" : the spec's fallback: fresh hash check, then rename over the note.
\* Mode "link"  : fallback that hard-links the old inode first, renames, then absorbs the link (round 1 fix).
\* Fd=FALSE: Obsidian saves in place atomically (one step).  Fd=TRUE: a save is open(path) then, later, write(fd): the write
\*   lands in the inode the path had at open time, wherever that inode has moved to.
\* Settle=TRUE: Sindri waits (the 2 s quiet) until no editor fd is pending on the inode it is about to read/delete.
EXTENDS Naturals, FiniteSets
CONSTANTS N, MaxBuild, MaxIno, SW, MaxCrash, Mode, Fd, Settle
VARIABLES cont, noteI, tmpI, bakI, nxt, ledger, typed, pend, pc, snap, exp, wr, builds, wl, crashes
vars == <<cont, noteI, tmpI, bakI, nxt, ledger, typed, pend, pc, snap, exp, wr, builds, wl, crashes>>
Edits == 1..N
Inos == 1..MaxIno
Quiet(i) == \A e \in Edits : pend[e] # i      \* no editor write is on its way to inode i

Init ==
   /\ cont = [i \in Inos |-> {}] /\ noteI = 1 /\ tmpI = 0 /\ bakI = 0 /\ nxt = 2
   /\ ledger = {} /\ typed = {} /\ pend = [e \in Edits |-> 0] /\ pc = "idle"
   /\ snap = {} /\ exp = {} /\ wr = {} /\ builds = 0 /\ wl = SW /\ crashes = 0

\* ---- Obsidian ----
TypeInPlace(e) ==
   /\ ~Fd /\ e \notin typed
   /\ typed' = typed \cup {e} /\ cont' = [cont EXCEPT ![noteI] = @ \cup {e}]
   /\ UNCHANGED <<noteI, tmpI, bakI, nxt, ledger, pend, pc, snap, exp, wr, builds, wl, crashes>>
OpenFd(e) ==
   /\ Fd /\ e \notin typed /\ pend[e] = 0
   /\ pend' = [pend EXCEPT ![e] = noteI]
   /\ UNCHANGED <<cont, noteI, tmpI, bakI, nxt, ledger, typed, pc, snap, exp, wr, builds, wl, crashes>>
WriteFd(e) ==
   /\ Fd /\ pend[e] # 0
   /\ cont' = [cont EXCEPT ![pend[e]] = @ \cup {e}] /\ typed' = typed \cup {e} /\ pend' = [pend EXCEPT ![e] = 0]
   /\ UNCHANGED <<noteI, tmpI, bakI, nxt, ledger, pc, snap, exp, wr, builds, wl, crashes>>

\* ---- Sindri: one pass ----
Start ==
   /\ pc = "idle" /\ wl > 0 /\ tmpI = 0 /\ bakI = 0
   /\ wl' = wl - 1 /\ snap' = cont[noteI] /\ exp' = cont[noteI] /\ builds' = 0 /\ pc' = "read"
   /\ UNCHANGED <<cont, noteI, tmpI, bakI, nxt, ledger, typed, pend, wr, crashes>>
Merge ==
   /\ pc = "read" /\ ledger' = ledger \cup snap /\ pc' = "merged"
   /\ UNCHANGED <<cont, noteI, tmpI, bakI, nxt, typed, pend, snap, exp, wr, builds, wl, crashes>>
\* step 1: build the new note from the merged state into the tmp file (exclusive create). Give up after MaxBuild builds.
Build ==
   /\ pc = "merged" /\ nxt <= MaxIno /\ builds < MaxBuild
   /\ cont' = [cont EXCEPT ![nxt] = ledger] /\ tmpI' = nxt /\ nxt' = nxt + 1 /\ wr' = ledger
   /\ builds' = builds + 1 /\ pc' = "built"
   /\ UNCHANGED <<noteI, bakI, ledger, typed, pend, snap, exp, wl, crashes>>
GiveUp ==
   /\ pc = "merged" /\ (builds >= MaxBuild \/ nxt > MaxIno) /\ pc' = "idle"
   /\ UNCHANGED <<cont, noteI, tmpI, bakI, nxt, ledger, typed, pend, snap, exp, wr, builds, wl, crashes>>

\* -- swap mode --
Swap ==
   /\ Mode = "swap" /\ pc = "built" /\ noteI' = tmpI /\ tmpI' = noteI /\ pc' = "swapped"
   /\ UNCHANGED <<cont, bakI, nxt, ledger, typed, pend, snap, exp, wr, builds, wl, crashes>>
\* step 3: hash what came out (the inode now at the tmp path)
HashOut ==
   /\ Mode = "swap" /\ pc = "swapped" /\ (~Settle \/ Quiet(tmpI))
   /\ IF cont[tmpI] = exp
         THEN /\ pc' = "del-ok" /\ UNCHANGED ledger
         ELSE /\ pc' = "del-retry" /\ ledger' = ledger \cup cont[tmpI]
   /\ UNCHANGED <<cont, noteI, tmpI, bakI, nxt, typed, pend, snap, exp, wr, builds, wl, crashes>>
DelOk ==
   /\ pc = "del-ok" /\ tmpI' = 0 /\ pc' = "idle"
   /\ UNCHANGED <<cont, noteI, bakI, nxt, ledger, typed, pend, snap, exp, wr, builds, wl, crashes>>
DelRetry ==
   /\ pc = "del-retry" /\ tmpI' = 0 /\ pc' = "merged" /\ exp' = wr    \* what the note should now hold is what we wrote
   /\ UNCHANGED <<cont, noteI, bakI, nxt, ledger, typed, pend, snap, wr, builds, wl, crashes>>

\* -- naive / link modes: fresh hash check, then rename over the note --
Check ==
   /\ Mode # "swap" /\ pc = "built"
   /\ IF cont[noteI] = exp THEN /\ pc' = "checked" /\ UNCHANGED <<ledger, exp, tmpI>>
      ELSE /\ ledger' = ledger \cup cont[noteI] /\ exp' = cont[noteI] /\ tmpI' = 0 /\ pc' = "merged"
   /\ UNCHANGED <<cont, noteI, bakI, nxt, typed, pend, snap, wr, builds, wl, crashes>>
Link ==
   /\ Mode = "link" /\ pc = "checked" /\ bakI' = noteI /\ pc' = "linked"
   /\ UNCHANGED <<cont, noteI, tmpI, nxt, ledger, typed, pend, snap, exp, wr, builds, wl, crashes>>
Rename ==
   /\ ((Mode = "naive" /\ pc = "checked") \/ (Mode = "link" /\ pc = "linked"))
   /\ noteI' = tmpI /\ tmpI' = 0 /\ pc' = IF Mode = "naive" THEN "idle" ELSE "renamed"
   /\ UNCHANGED <<cont, bakI, nxt, ledger, typed, pend, snap, exp, wr, builds, wl, crashes>>
Absorb ==
   /\ Mode = "link" /\ pc = "renamed" /\ (~Settle \/ Quiet(bakI))
   /\ ledger' = ledger \cup cont[bakI] /\ pc' = "del-bak"
   /\ UNCHANGED <<cont, noteI, tmpI, bakI, nxt, typed, pend, snap, exp, wr, builds, wl, crashes>>
DelBak ==
   /\ pc = "del-bak" /\ bakI' = 0 /\ pc' = "idle"
   /\ UNCHANGED <<cont, noteI, tmpI, nxt, ledger, typed, pend, snap, exp, wr, builds, wl, crashes>>

\* ---- crash and restart (step 5: merge any leftover tmp / backup, then delete it) ----
Crash ==
   /\ pc \notin {"idle", "recover", "recdel"} /\ crashes < MaxCrash /\ crashes' = crashes + 1
   /\ pc' = "recover" /\ snap' = {} /\ exp' = {} /\ wr' = {} /\ builds' = 0
   /\ UNCHANGED <<cont, noteI, tmpI, bakI, nxt, ledger, typed, pend, wl>>
Recover ==
   /\ pc = "recover"
   /\ IF tmpI = 0 /\ bakI = 0 THEN /\ pc' = "idle" /\ UNCHANGED ledger
      ELSE /\ (~Settle \/ ((tmpI = 0 \/ Quiet(tmpI)) /\ (bakI = 0 \/ Quiet(bakI))))
           /\ ledger' = ledger \cup (IF tmpI = 0 THEN {} ELSE cont[tmpI]) \cup (IF bakI = 0 THEN {} ELSE cont[bakI])
           /\ pc' = "recdel"
   /\ UNCHANGED <<cont, noteI, tmpI, bakI, nxt, typed, pend, snap, exp, wr, builds, wl, crashes>>
RecDel ==
   /\ pc = "recdel" /\ tmpI' = 0 /\ bakI' = 0 /\ pc' = "idle"
   /\ UNCHANGED <<cont, noteI, nxt, ledger, typed, pend, snap, exp, wr, builds, wl, crashes>>

Next == (\E e \in Edits : TypeInPlace(e) \/ OpenFd(e) \/ WriteFd(e))
        \/ Start \/ Merge \/ Build \/ GiveUp \/ Swap \/ HashOut \/ DelOk \/ DelRetry
        \/ Check \/ Link \/ Rename \/ Absorb \/ DelBak \/ Crash \/ Recover \/ RecDel
Spec == Init /\ [][Next]_vars

Reach == {noteI} \cup (IF tmpI = 0 THEN {} ELSE {tmpI}) \cup (IF bakI = 0 THEN {} ELSE {bakI})
Held == UNION {cont[i] : i \in Reach}
TypeOK ==
   /\ noteI \in Inos /\ tmpI \in 0..MaxIno /\ bakI \in 0..MaxIno /\ typed \subseteq Edits /\ ledger \subseteq Edits
\* Every Obsidian edit that was saved is in the note, in the ledger, or in a leftover file the next start will absorb.
NoLostEdit == typed \subseteq (ledger \cup Held)
\* Stronger, at rest with no leftovers: in the note or in the ledger.
NoLostAtRest == (pc = "idle" /\ tmpI = 0 /\ bakI = 0) => typed \subseteq (ledger \cup cont[noteI])
\* Witnesses (must be violated): a retry happened; a crash left a swapped leftover.
NoRetry == ~(pc = "del-retry")
NoRecoverWork == ~(pc = "recdel")
====
