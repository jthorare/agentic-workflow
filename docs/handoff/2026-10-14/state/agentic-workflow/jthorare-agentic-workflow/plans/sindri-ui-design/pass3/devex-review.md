# Plan Devex Review — sindri-ui-design (pass 3, revision 3)

**Plan:** docs/superpowers/specs/2026-10-11-sindri-ui-design.md
**Reviewed:** 2026-10-11

## Summary
Pass-1 HIGHs (H1–H7) and pass-2 items hold in the text: §18.1 owns every E-edit, §18.2 gives fixture, stall, screenshot, five-second, keyboard and launch recipes, `sindri-prove.sh` (API §12) and `recipes/dev/` (WF W24) exist in the cited specs. No new HIGH. Two new MEDIUMs, both about E-17 goldens, the contract every U phase rests on. Time-to-first-success: ~45 min for U0 (shell + theme on S1b), ~2–3 h for a first proven U1 (needs API P1/P2 goldens).

## Findings by severity
### CRITICAL
None.
### HIGH
None.
### MEDIUM
- **Backwards compatibility / docs:** E-17 obliges "every API phase from P1" to write goldens, but the API spec doesn't carry it (no "golden" anywhere in `2026-10-10-sindri-api-surface-design.md`); §18.1 lists only E-10 as an edit to the API spec's phase table. An API phase built from its own spec (or already merged before approval) ships without goldens, and U1's contract unit has nothing to read.
  - Evidence: §18.1 lines 1029, 1035–1038; absent — searched API spec for "golden", "Golden/Api".
  - Fix: add E-17 to the API spec's §12 test rules (README cross-spec row), and say that U1's contract unit back-fills goldens for any API phase already merged (P1, P2).
- **API surface ergonomics (test contract):** goldens are one file per route (`Golden/Api/<route>.json`), but routes have path params and the views have several states (`LaunchView` requested/started/failed, empty vs populated `needs-you`, error bodies such as `SND-ITEM-409`). The fake can't drive §4.9's state matrix from one body per route, and the route→filename mapping (`{id}`, slashes, query strings) is undefined, so parallel API and UI agents will guess differently. Nondeterministic fields other than `asOf` (sequences, `LCH-`/`ITM-` ids, timestamps) also aren't addressed.
  - Evidence: §18.1 lines 1035–1037; §4.9; absent — searched "scenario", "per state" near E-17.
  - Fix: name goldens `<route-template-slug>.<scenario>.json` (e.g. `launches_{id}.failed.json`), require one golden per §4.9 state the route can serve plus each error code the UI branches on, and have the API tests run under a fixed `TimeProvider` and seeded id generator so only `asOf` needs stripping.
### LOW
- **Ramp-up cost:** X23 (nonce POST class) is still open and gates U5's forms; §18 U5's "Depends on" doesn't list it. Fix: add "X23 settled" to U5's dependencies.
- **Setup:** OS screenshots need Screen Recording granted by the owner once; U0's prover may hit it first. Fix: list it in U0's signal as an owner runbook precondition, as U6a does for `claude` login.

## Per-dimension summary
| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | MEDIUM | 1 |
| Setup / install (time-to-first-success: ~45 min U0; ~2–3 h proven U1) | LOW | 1 |
| Error messages & observability | — | 0 |
| Docs completeness | MEDIUM | 1 (shared with compat) |
| Backwards compatibility | MEDIUM | 1 |
| Ramp-up cost (tribal-knowledge items: 2 — goldens must be back-filled for merged API phases; Screen Recording grant) | LOW | 1 |

## Verdict
No CRITICAL or HIGH; prior fixes hold, and the two E-17 MEDIUMs (API spec doesn't carry the golden obligation; one-golden-per-route can't drive the state matrix) should be fixed before U1's contract unit.
verdict: PASS
