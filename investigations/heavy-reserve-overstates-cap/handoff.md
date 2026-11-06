# Handoff: heavy-reserve-overstates-cap

status: diagnosed
report: /home/thor/repos/aw-wt/api-2100/investigations/heavy-reserve-overstates-cap/report.md
boundary: src/Meta/Aw.Meta.Core
repro: cap 3, merge-queue depth 8: `aw-meta status` prints "heavy reservation: 3 of 3 slots (queue depth 8) - set it with meta-gate-reserve 7"; `scripts/meta-gate-reserve.sh 7` writes 2. Unit-level: `ReserveSetting.Default.For(7, 3)` returns 3 (should be 2); `For(5, 2)` with a tier at 5 returns 2 (should be 1).

## Root Cause
`ReserveSlots.Resolve` (src/Meta/Aw.Meta.Core/Config/ReserveSlots.cs:16) clamps to `cap` only, so the default `ReserveSlots.All` tier (ReserveSetting.cs:16) and any custom number >= cap resolve to `cap`; `ReserveSetting.For` (ReserveSetting.cs:24-28) returns it unchanged. The script (scripts/meta-gate-reserve.sh) now clamps every tier to cap-1 (owner 2026-11-05, #2060), so aw-meta's computed reservation and its printed script argument (HeavySlots.ScriptDepth, HeavySlots.cs:18-25, `slots >= Cap => 7`) overstate by one.

## Hypotheses

| # | Hypothesis | Cause-site files | Likelihood | Result |
|---|-----------|------------------|------------|--------|
| 1 | Resolve/For clamp to cap, not cap-1; default All tier = cap | `src/Meta/Aw.Meta.Core/Config/ReserveSlots.cs`, `src/Meta/Aw.Meta.Core/Config/ReserveSetting.cs` | High | confirmed |
| 2 | ScriptDepth maps Reserve >= Cap to 7, docs describe old tiers | `src/Meta/Aw.Meta.Core/Health/HeavySlots.cs`, `docs/csharp/meta.md:146-158` | High | confirmed |
| 3 | A custom ReserveSlots(9) is clamped elsewhere | `src/Meta/Aw.Meta.Core/Coordination/HeavyReservation.cs` | Medium | ruled-out |

## Ruled Out
- Custom tier above cap-1 clamped elsewhere — the only clamp is ReserveSlots.Resolve (0..cap); HeavyReservation.cs:73 uses For's result as-is and no other caller of For/Resolve exists in src/Meta.

## Suggested Fix
Clamp in `ReserveSetting.For` (single choke point for default and custom tiers): result = Math.Min(resolved, Math.Max(cap - 1, 0)); cap 1 -> 0. Then `HeavySlots.ScriptDepth` can drop the `>= Cap => 7` arm: Reserve can't reach Cap, so with Reserve = cap-1 map via the 1/2 tiers (reserve 1 -> 3, 2 -> 5; cap-1 >= 3 has no depth and keeps the fallback 5, matching the script's own cap-1 for depth 7+: only if cap-1 == script result). Check ScriptDepth against the script: depth 7 on cap 3 writes 2 (arg 5 or 7 both write 2; arg 7 is exact for any cap, so keep 7 for Reserve == Cap-1 when Cap-1 > 2). Update tests/Aw.Meta.Tests/Reserve/HeavyReservationTests.cs (ScriptDepth theory) and Config/ReserveSettingTests.cs, the "7 or more reserves all of them / N = C prints 7" text in docs/csharp/meta.md:146-158 and the NOTE example. Keep PublicAPI unchanged.
