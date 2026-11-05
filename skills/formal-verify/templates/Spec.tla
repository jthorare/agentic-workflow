---- MODULE Spec ----
\* Rename the module and the file together (Spec.tla -> JobClaim.tla, MODULE JobClaim).
\* One action per real atomic step; above each, the path:line it models.
EXTENDS Naturals, FiniteSets, Sequences

CONSTANTS Workers          \* e.g. {w1, w2} in the .cfg

VARIABLES pc, state        \* pc: where each worker is; state: the shared data the mechanism guards
vars == <<pc, state>>

TypeOK ==
    /\ pc \in [Workers -> {"idle", "done"}]
    /\ state \in {"free"}

Init ==
    /\ pc = [w \in Workers |-> "idle"]
    /\ state = "free"

\* src/<file>:<line> — what this step does in the real code
Step(w) ==
    /\ pc[w] = "idle"
    /\ pc' = [pc EXCEPT ![w] = "done"]
    /\ UNCHANGED state

\* Every worker has finished: stutter here so TLC does not report a deadlock.
Done == /\ \A w \in Workers : pc[w] = "done"
        /\ UNCHANGED vars

Next == (\E w \in Workers : Step(w)) \/ Done

\* Fairness: a claim about the real scheduler. WF: a step that stays possible eventually happens.
Spec == Init /\ [][Next]_vars /\ \A w \in Workers : WF_vars(Step(w))

\* ---- Properties (the plain-word list is in properties.md) ----

\* Safety: <what must never happen, in plain words>
Safe == TRUE

\* Liveness: <what must eventually happen, in plain words>
EveryoneFinishes == \A w \in Workers : <>(pc[w] = "done")

\* ---- Witnesses (step 6): each must be VIOLATED, or the model never reaches the state it guards ----
NobodyFinishes == \A w \in Workers : pc[w] # "done"
====
