# <Mechanism>: formal check

**Verdict:** pass | violation | error | blocked. <One sentence: what holds, or what breaks.>
**Model:** `<Module>.tla`, config `<Module>.cfg`. **TLC:** `<last tlc: line>` (tla2tools <version>).
**Bounds:** <e.g. every interleaving of 2 workers and 2 jobs, with up to 1 crash>. Nothing is claimed beyond them.

## Properties

| Name | In plain words | Kind | Result |
|------|----------------|------|--------|
| `TypeOK` | every variable stays in its set | invariant | pass |
| `<Name>` | <never / eventually ...> | invariant / property | pass / VIOLATED |

Fairness assumed: <WF/SF on which steps, and why the real system gives it>.

## Witnesses

| Witness | Guards | Result (must be VIOLATION) |
|---------|--------|----------------------------|
| `<Name>` | <state> is reached | VIOLATION |

## Counterexample

<Only for a violation: one row per TLC state, in the real system's terms.>

| # | Actor | Step | Models | What changed |
|---|-------|------|--------|--------------|
| 1 | - | start | - | <initial values that matter> |
| 2 | <worker 1> | <step> | `path:line` | <values> |

**Breaks:** <the property, and the state that breaks it, in plain words>.

## Fixes

1. <Smallest change> at `path:line`. Modeled as <action change>; same config: `<tlc: line before>` -> `<tlc: line after>`.

## What the model leaves out

- <timeouts, more actors, partitions, anything abstracted away>
