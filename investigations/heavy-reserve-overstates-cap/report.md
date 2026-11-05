# Investigation: aw-meta heavy reservation reserves every slot at depth 7+

**Date:** 2026-11-05
**Status:** diagnosed

## Error Description
`aw-meta status`/`gate-run`/`health` print `heavy reservation: 3 of 3 slots (queue depth 8) - set it with meta-gate-reserve 7`.
The script (`scripts/meta-gate-reserve.sh`, in-repo copy on branch feat/meta-gate-reserve-in-repo, owner decision 2026-11-05 on #2060)
now clamps `r` to `cap - 1`, so `meta-gate-reserve 7` writes 2. The status line overstates by one.
(The installed ~/.agentic-workflow/bin-r1b/meta-gate-reserve is still the old script without the clamp.)

## Module Boundary
`src/Meta/Aw.Meta.Core` (cause: Config/ReserveSetting.cs + Config/ReserveSlots.cs + Health/HeavySlots.cs; error site: Coordination/HeavyReservation.cs, same package). Docs: docs/csharp/meta.md.

## Call Chain
HeavyReservation.Compute (Coordination/HeavyReservation.cs:73) -> ReserveSetting.For (Config/ReserveSetting.cs:24-28) -> ReserveSlots.Resolve (Config/ReserveSlots.cs:16) -> HeavySlots.ScriptDepth (Health/HeavySlots.cs:18-25) / Line()

## Hypotheses

| # | Hypothesis | Likelihood | Result |
|---|-----------|------------|--------|
| 1 | `ReserveSlots.Resolve` clamps only to `cap`, and the default top tier `ReserveSlots.All` resolves to cap, so `For` returns cap at depth >= 7; no cap-1 clamp exists anywhere | High | confirmed |
| 2 | `HeavySlots.ScriptDepth` maps Reserve >= Cap to 7 (and the docs describe it), which the script now turns into cap-1 | High | confirmed (consequence of 1: it is why the printed argument 7 and the 'N of C' line disagree with the written value) |
| 3 | A custom tier with a number above cap-1 (e.g. `ReserveSlots(9)`) is clamped elsewhere | Medium | ruled out: only ReserveSlots.Resolve (clamp 0..cap) and ReserveSetting.For touch it; HeavyReservation.cs:73 uses For's result as-is; grep of src/Meta shows no other caller |

## Root Cause
`ReserveSlots.Resolve(int cap)` (`src/Meta/Aw.Meta.Core/Config/ReserveSlots.cs:16`) is `Math.Clamp(Count ?? cap, 0, Math.Max(cap, 0))`, so any tier (the default `All`, or a custom number >= cap) resolves up to `cap`. `ReserveSetting.For` (`ReserveSetting.cs:24-28`) returns that unchanged, `HeavyReservation.Compute` (`HeavyReservation.cs:73`) stores it, and `HeavySlots.Line()` prints `Reserve of Cap` and `ScriptDepth` (`HeavySlots.cs:18-25`: `slots >= Cap => 7`). The script's new `cap - 1` clamp (owner 2026-11-05) has no counterpart in aw-meta, so at depth 7+ on cap 3: aw-meta says 3, script writes 2. Example: cap 3, depth 8 -> prints "3 of 3 ... meta-gate-reserve 7"; script writes 2. Cap 2, depth 5 -> aw-meta 2, script 1. Cap 1: aw-meta 1, script 0.

## Fix Applied
n/a — investigate-only

## Verification
n/a — investigate-only (code reading only; no builds or tests run)
