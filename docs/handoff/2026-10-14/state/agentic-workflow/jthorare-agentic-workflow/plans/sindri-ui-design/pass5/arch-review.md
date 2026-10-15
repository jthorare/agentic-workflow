# Architecture Review (pass 5, slim): Sindri UI app design, revision 5

_Reviewed by `/archReview --slim` on 2026-10-11. Focus: §6.7 E-1…E-19, §18, §9, consistency with the API surface and workflow engine specs._

## Verdict: SOUND

No CRITICAL or HIGH findings. The pass-4 fixes hold. A4-1: E-1 now has an in-process claimed set under the timer's lock, a 10 s `Launch` limit and a race test that asserts `launch.started`. A4-2: no wait when the instance is already taken over. A4-3: E-19 exists and lands in WF P5 per §18.1. A4-4 through A4-7 are fixed (row order and intro, X23 in U5's dependencies, E-12's field rule, the `-007` fix text). New problems: one MEDIUM, a race between E-19 and the launcher's instance check that A4-3's third fix didn't cover, and two LOWs.

## Scores

| Dimension | Score | Notes |
|---|:---:|---|
| Complexity | 4 | The launcher state machine is now defined: claim, limit, expiry, one outcome key. One way into it from the engine is still open (A5-1) |
| Scalability | 4 | Single-user loopback; debounce, caps, 429 → 5 s |
| Maintainability | 4 | Every edit is numbered and placed (§18.1); goldens (E-17, X26) |

## Prior HIGH/MEDIUM fixes

| Item | Holds | Where |
|---|---|---|
| A4-1 claim, `Launch` bound, race-test outcome | Yes | E-1 |
| A4-2 Attach or second Open waiting on a no-op | Yes (no wait when already `taken-over`) | E-1, §6.4 |
| A4-3 starts-taken-over numbered, reads at the head | Partly: E-19, `ReadsViews`. The router rule for an instance created after the request is missing (A5-1) | E-19, §18.1 |

## Edge-case matrix

| Boundary | Dependency unavailable | Invalid input | Load 10x | State leakage |
|---|---|---|---|---|
| App → API (§9) | OK: §4.1, 2 s look-again, 429 → 5 s | OK: plain text, E-16 | OK: 250 ms debounce per view | OK: nonce fetched at click time |
| `OpenItem` → launcher (E-1) | OK: start-up sweep, 30 s expiry, `-007` | OK: id patterns, YAML serializer | OK: `-008` | **A5-2** |
| Launcher → engine (E-6, E-19) | OK: wait bounded by the deadline | OK: key `wf:{i}:open:{launchId}` | OK | **A5-1** |
| UI → engine commands (§9.1) | OK: X23 is in U5's dependencies | OK: E-12 field rule | OK | OK |
| Launcher → Warp | **A5-2** | OK | OK | OK: 0600 files, 60 s sweep |

## Findings

| id | Severity | Section | Finding | Fix |
|---|---|---|---|---|
| A5-1 | MEDIUM | E-1, E-6, E-19, §6.4 | E-19 reads launches before `StartInstance`'s `Commit`, so a request appended after that read and before the commit starts the instance *not* taken over. The launcher then decides "no active instance → launch at once" from a view that can lag (the X7 item projection), so Warp can open while the new instance runs. E-6 doesn't say that the router applies `open-request` to an instance created after the request (A4-3's third fix) | The launcher decides instance presence at or past the request's ledger sequence (the read waits for the head, as `ReadsViews` does). E-6: the router applies `open-request` to any instance for the item that starts while the launch has no outcome. The launcher always waits for that trigger's W12-recorded result (applied, no-op or refused) when an instance appears, never for a takeover event alone. Add a race test: request between E-19's read and its commit gives the ledger order takeover, then `launch.started` |
| A5-2 | LOW | E-1, §6.6 | A claim at about 29.9 s plus the 10 s `Launch` limit means Warp can open about 10 s after the 30 s deadline, which contradicts §6.6's "Warp never opens late". Separately, a `Launch` timeout recorded as `-001` with the fix "Retry" can produce a second tab if `open` delivered late | §6.6 should say "at most ~40 s". Make the claim cut-off `deadline − limit`, or make the limit `min(10 s, deadline − now)`. Give the `-001` timeout case the same "look for `sindri ITM-…` first" fix text as `-007` |
| A5-3 | LOW | E-6, E-1 | An instance can become `taken-over` by a human Take over between the launcher's check and the trigger. The trigger is then a no-op, and a launcher that waits for a takeover event "matched by causation" waits until the 30 s `-007` | Covered by A5-1's fix: match on the trigger's recorded result, not on the takeover event |

verdict: PASS
