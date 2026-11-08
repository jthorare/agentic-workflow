---- MODULE NoteWrite4 ----
\* Rev 4 §4.5 Writing step 3: "Empty or shorter than the note Sindri merged is 'not settled yet': wait again".
\* Question: does a legitimate Obsidian save that DELETES lines wedge the pass? Swap mode, editor saves in place, settle on.
\* ShortRule=TRUE: HashOut/Recover are blocked while the came-out file has fewer lines than the note Sindri wrote (rev 4 as written).
\* ShortRule=FALSE: only an empty came-out file (while the written note isn't empty) is blocked, with a bound (Giveup) after which the file is kept.
EXTENDS Naturals, FiniteSets
CONSTANTS N, ShortRule, KeepOne
VARIABLES cont, noteI, tmpI, ledger, typed, deleted, pc, exp, wr, built, crashed
vars == <<cont, noteI, tmpI, ledger, typed, deleted, pc, exp, wr, built, crashed>>
Edits == 1..N
Init == cont = [i \in 1..3 |-> {}] /\ noteI = 1 /\ tmpI = 0 /\ ledger = {} /\ typed = {} /\ deleted = {}
        /\ pc = "idle" /\ exp = {} /\ wr = {} /\ built = FALSE /\ crashed = FALSE
Type(e) == e \notin typed /\ typed' = typed \cup {e} /\ cont' = [cont EXCEPT ![noteI] = @ \cup {e}]
            /\ UNCHANGED <<noteI, tmpI, ledger, deleted, pc, exp, wr, built, crashed>>
Del(e) == e \in cont[noteI] /\ (~KeepOne \/ Cardinality(cont[noteI]) > 1) /\ e \notin deleted /\ deleted' = deleted \cup {e} /\ cont' = [cont EXCEPT ![noteI] = @ \ {e}]
            /\ UNCHANGED <<noteI, tmpI, ledger, typed, pc, exp, wr, built, crashed>>
Start == pc = "idle" /\ tmpI = 0 /\ ~built /\ pc' = "merged" /\ ledger' = ledger \cup cont[noteI] /\ exp' = cont[noteI]
            /\ UNCHANGED <<cont, noteI, tmpI, typed, deleted, wr, built, crashed>>
Other == IF noteI = 1 THEN 2 ELSE 1
Build == pc = "merged" /\ cont' = [cont EXCEPT ![Other] = ledger] /\ tmpI' = Other /\ wr' = ledger /\ pc' = "built" /\ built' = TRUE
            /\ UNCHANGED <<noteI, ledger, typed, deleted, exp, crashed>>
Swap == pc = "built" /\ noteI' = tmpI /\ tmpI' = noteI /\ pc' = "swapped"
            /\ UNCHANGED <<cont, ledger, typed, deleted, exp, wr, built, crashed>>
Settled(i) == IF ShortRule THEN Cardinality(cont[i]) >= Cardinality(wr) ELSE TRUE
HashOut == pc = "swapped" /\ Settled(tmpI)
            /\ pc' = "idle" /\ ledger' = (IF cont[tmpI] = exp THEN ledger ELSE ledger \cup cont[tmpI])
            /\ tmpI' = 0 /\ built' = FALSE
            /\ UNCHANGED <<cont, noteI, typed, deleted, exp, wr, crashed>>
\* a crash after the swap: restart reads the leftover with the same step-3 rule
Crash == pc = "swapped" /\ ~crashed /\ crashed' = TRUE /\ pc' = "recover"
            /\ UNCHANGED <<cont, noteI, tmpI, ledger, typed, deleted, exp, wr, built>>
Recover == pc = "recover" /\ Settled(tmpI) /\ ledger' = ledger \cup cont[tmpI] /\ tmpI' = 0 /\ built' = FALSE /\ pc' = "idle"
            /\ UNCHANGED <<cont, noteI, typed, deleted, exp, wr, crashed>>
Next == (\E e \in Edits : Type(e) \/ Del(e)) \/ Start \/ Build \/ Swap \/ HashOut \/ Crash \/ Recover
Spec == Init /\ [][Next]_vars
\* The pass must be able to finish: a state is never stuck in "swapped"/"recover" with nothing enabled (deadlock check does this).
NoLost == (typed \ deleted) \subseteq (ledger \cup cont[noteI] \cup (IF tmpI = 0 THEN {} ELSE cont[tmpI]))
====
