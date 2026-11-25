------------------------- MODULE StartLockTakeover -------------------------
(* The takeover guard of scripts/meta-start-lock.sh (#2613). See README.md.
   Takers are the integers in Takers; the integer order is the glob order of their pid files.
   pc: idle, mk_ann (mkdir won, pid file next), body, w_ann, w_glob, w_chk (reading entries), done;
   the Old loop uses o_glob, o_chk, o_ann, o_rm. *)
EXTENDS Integers, FiniteSets

CONSTANTS Takers, MaxDeaths, Old

VARIABLES gx,      \* the guard directory exists
          g,       \* the pid files in it (a set of takers; a dead taker's file stays)
          alive, pc,
          todo,    \* entries the taker still has to check (from its glob)
          dead,    \* entries it found dead
          tgt,     \* the Old loop's chosen entry
          win,     \* the taker won the mkdir
          deaths

vars == <<gx, g, alive, pc, todo, dead, tgt, win, deaths>>

Min(S) == CHOOSE p \in S : \A q \in S : p <= q
NoOne == 0

TypeOK ==
    /\ gx \in BOOLEAN /\ g \subseteq Takers
    /\ alive \in [Takers -> BOOLEAN]
    /\ pc \in [Takers -> {"idle", "mk_ann", "body", "w_ann", "w_glob", "w_chk", "o_glob", "o_chk", "o_ann", "o_rm", "done"}]
    /\ deaths \in 0..MaxDeaths

Init ==
    /\ gx = FALSE /\ g = {} /\ alive = [t \in Takers |-> TRUE] /\ pc = [t \in Takers |-> "idle"]
    /\ todo = [t \in Takers |-> {}] /\ dead = [t \in Takers |-> {}] /\ tgt = [t \in Takers |-> NoOne] /\ win = [t \in Takers |-> FALSE] /\ deaths = 0

Go(t, to) == pc' = [pc EXCEPT ![t] = to]

\* mkdir "$guard": the winner owns the guard; anyone else becomes a waiter.
Start(t) ==
    /\ pc[t] = "idle"
    /\ IF ~gx THEN gx' = TRUE /\ Go(t, "mk_ann") /\ win' = [win EXCEPT ![t] = TRUE]
              ELSE gx' = gx /\ Go(t, IF Old THEN "o_glob" ELSE "w_ann") /\ win' = win
    /\ UNCHANGED <<g, alive, todo, dead, tgt, deaths>>

MkAnn(t) ==
    /\ pc[t] = "mk_ann"
    /\ g' = g \cup {t} /\ Go(t, IF Old THEN "body" ELSE "w_glob")   \* after #2613 the winner scans like a waiter
    /\ UNCHANGED <<gx, alive, todo, dead, tgt, win, deaths>>

\* _start_guard_body's end: rm own file, rmdir (fails while any other file is there).
BodyDone(t) ==
    /\ pc[t] = "body"
    /\ g' = g \ {t} /\ gx' = (IF g \ {t} = {} THEN FALSE ELSE gx) /\ Go(t, "done")
    /\ UNCHANGED <<alive, todo, dead, tgt, win, deaths>>

\* ---- the protocol after #2613: announce first, read every entry, back off on any other live one ----
\* (a waiter whose mkdir failed; the mkdir winner announces in MkAnn and goes on to WGlob)
WAnn(t) ==
    /\ pc[t] = "w_ann"
    /\ IF gx THEN g' = g \cup {t} /\ Go(t, "w_glob") ELSE g' = g /\ Go(t, "done")
    /\ UNCHANGED <<gx, alive, todo, dead, tgt, win, deaths>>

WGlob(t) ==
    /\ pc[t] = "w_glob"
    /\ todo' = [todo EXCEPT ![t] = g \ {t}] /\ dead' = [dead EXCEPT ![t] = {}] /\ Go(t, "w_chk")
    /\ UNCHANGED <<gx, g, alive, tgt, win, deaths>>

WChk(t) ==
    /\ pc[t] = "w_chk" /\ todo[t] # {}
    /\ \E p \in todo[t] :
         /\ todo' = [todo EXCEPT ![t] = @ \ {p}]
         /\ IF alive[p] THEN g' = g \ {t} /\ Go(t, "done") /\ dead' = dead
                        ELSE g' = g /\ pc' = pc /\ dead' = [dead EXCEPT ![t] = @ \cup {p}]
    /\ UNCHANGED <<gx, alive, tgt, win, deaths>>

WDecide(t) ==
    /\ pc[t] = "w_chk" /\ todo[t] = {}
    /\ IF dead[t] = {}                                          \* no other file at all: the winner enters, a waiter
          THEN IF win[t] THEN g' = g /\ Go(t, "body")           \* cannot tell it from an empty guard of a winner
                         ELSE g' = g \ {t} /\ Go(t, "done")    \* that has not announced yet, so it leaves
       ELSE \E f \in dead[t] :
              IF f \in g THEN g' = g \ {f} /\ Go(t, "body")      \* the rm won
                         ELSE g' = g \ {t} /\ Go(t, "done")      \* the rm failed
    /\ UNCHANGED <<gx, alive, todo, dead, tgt, win, deaths>>

\* ---- the loop before #2613 (the planted witness): the first entry in glob order decides ----
OGlob(t) ==
    /\ pc[t] = "o_glob"
    /\ IF g = {} THEN Go(t, "done") /\ tgt' = tgt
       ELSE Go(t, "o_chk") /\ tgt' = [tgt EXCEPT ![t] = Min(g)]
    /\ UNCHANGED <<gx, g, alive, todo, dead, win, deaths>>

OChk(t) ==
    /\ pc[t] = "o_chk"
    /\ Go(t, IF alive[tgt[t]] THEN "done" ELSE "o_ann")
    /\ UNCHANGED <<gx, g, alive, todo, dead, tgt, win, deaths>>

OAnn(t) ==
    /\ pc[t] = "o_ann"
    /\ IF gx THEN g' = g \cup {t} /\ Go(t, "o_rm") ELSE g' = g /\ Go(t, "done")
    /\ UNCHANGED <<gx, alive, todo, dead, tgt, win, deaths>>

ORm(t) ==
    /\ pc[t] = "o_rm"
    /\ IF tgt[t] \in g THEN g' = g \ {tgt[t]} /\ Go(t, "body") ELSE g' = g \ {t} /\ Go(t, "done")
    /\ UNCHANGED <<gx, alive, todo, dead, tgt, win, deaths>>

\* An empty guard over a minute old is removed (never while a holder sits between mkdir and its pid file).
Age ==
    /\ gx /\ g = {} /\ \A t \in Takers : ~(alive[t] /\ pc[t] = "mk_ann")
    /\ gx' = FALSE
    /\ UNCHANGED <<g, alive, pc, todo, dead, tgt, win, deaths>>

Die(t) ==
    /\ alive[t] /\ pc[t] \notin {"idle", "done"} /\ deaths < MaxDeaths
    /\ alive' = [alive EXCEPT ![t] = FALSE] /\ deaths' = deaths + 1
    /\ UNCHANGED <<gx, g, pc, todo, dead, tgt, win>>

Next ==
    \/ \E t \in Takers :
         alive[t] /\ (Start(t) \/ MkAnn(t) \/ BodyDone(t) \/ WAnn(t) \/ WGlob(t) \/ WChk(t) \/ WDecide(t)
                      \/ OGlob(t) \/ OChk(t) \/ OAnn(t) \/ ORm(t))
    \/ \E t \in Takers : Die(t)
    \/ Age
    \/ UNCHANGED vars

Spec == Init /\ [][Next]_vars

MutualExclusion == Cardinality({t \in Takers : alive[t] /\ pc[t] = "body"}) <= 1
=============================================================================
