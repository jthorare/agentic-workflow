# Prevent the Class (shared)

The one copy of the prevention-first rule for every bug-fix path: rootCause, bugFixOrchestrator, bugHunt, addressReview's
implementers, specToProvenPR's review fixes, and Sindri's bug-fix and repair Steps (`BugClassPrevention` in
`src/Sindri/Aw.Sindri.Features/BugFix/`, which carries the question below word for word; a test holds the two equal).
Referenced via: SHARED_DIR pattern (CD2).

## The question

Ask it once the root cause is confirmed, and before choosing a fix:

> How can we prevent this class of bugs from happening again—not just fix this instance? Consider enforceable constraints such as lint rules, analyzers, type/API design, shared primitives, state-machine or architecture changes, and checks that catch other instances of the same failure.

The answer shapes the fix you propose. It is not a "future improvements" paragraph written after a local patch.

## What to do, in order

1. **Name the class and the invariant.** One sentence each, grounded in the code and evidence you have: "callers
   build SQL by string concatenation" / "every query takes parameters". Don't invent adjacent defects you haven't seen.
2. **Find the other instances.** Search for the same pattern (grep, symbol usages, the same API called elsewhere).
   List what you found with file:line, or say you searched and found none, and how.
3. **Pick the smallest mechanism that holds the invariant**, from: an existing guard that already covers it (say which),
   a lint or analyzer rule, a compiler or type constraint, a safer API, one shared primitive, a data constraint, a
   state-machine or architecture change, or a reusable behavioural check run over every instance. Say why it covers the
   class. A regression test for one fixture is not class-wide prevention by itself.
4. **Keep mitigation and prevention apart.** A quick mitigation (a timeout, a retry, a guard at one call site) is fine
   when the bug hurts now; call it mitigation. If the prevention can't be built inside the fix's allowed scope, say what
   remains and record a concrete follow-up (an issue with the class, the instances and the proposed mechanism). Never
   say the class is gone when only the instance is.
5. **Prove both.** The original check that failed must pass. The mechanism must be shown working: an analyzer rejects a
   second representative bad instance and accepts valid code; a contract check runs against more than one
   implementation, or interrupts at more than one point.
6. **No absolute claims without evidence.** "Can't happen again" needs the mechanism and its proof; otherwise say what
   it covers and what it doesn't.

Don't turn every bug into a redesign or a new lint rule. The confirmed cause and how widely the pattern repeats decide
the mechanism. Reproduction comes first, the frozen check is never weakened to make a broader change pass, and scope
and approval limits still hold.

## Three bugs, three different answers

| Bug | Class | Prevention decision |
|---|---|---|
| A typo in one config key, and the config loader already rejects unknown keys at startup, but this key was read with a raw dictionary lookup | One call site bypassed an existing guard | Existing guard: route the read through the typed loader. Instances: grep shows no other raw lookup. No new rule: one bypass isn't a pattern |
| `DateTime.Now` in a time-sensitive path; grep finds it in 9 more places | Wall-clock reads instead of the injected clock | Static enforcement: ban `DateTime.Now` in `BannedSymbols.txt`, fix all 10. Proof: the analyzer fails a sample that uses it and passes one that takes the clock |
| A connector loses all progress when a large page is cancelled (raising its timeout made one run pass) | Ingestion isn't resumable: no checkpoint between pages | Design: a checkpointed connector contract plus a reusable interrupt-and-resume check run against every connector. The timeout bump is mitigation. If only one connector moves in this fix, the others are a filed follow-up |

## The handoff block

Every step that hands a bug on (investigation → reproduction → fix → review → report) carries this block forward
unchanged, adding only its own proof. A step that receives a root cause without it asks for it, or writes it before
going on.

```markdown
## Failure class
<one sentence: the mistake that can recur>

## Invariant
<one sentence: what should always hold>

## Affected scope
<other instances found, file:line each, or "searched <how>, none found">

## Prevention decision
<existing guard <which> | new mechanism <which> | deferred: <follow-up link>>, and why it covers the class

## Mitigation
<the instance fix and any stopgap, or "none">

## Proof
- Original failure: <the frozen check, red before, green after>
- Prevention: <the mechanism's own proof, or "deferred">
```

## In the resolution report

Three separate lines, never merged into one "fixed":

- **Original bug:** the frozen check's before and after, and the judge's verdict.
- **Class prevention:** what was built and its proof, or "existing guard <which>", or "none built".
- **Deferred:** the follow-ups recorded, with links, or "none".
