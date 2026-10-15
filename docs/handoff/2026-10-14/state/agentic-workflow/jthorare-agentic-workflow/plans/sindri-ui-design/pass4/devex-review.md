# Plan Devex Review — sindri-ui-design (pass 4, revision 4)

**Plan:** docs/superpowers/specs/2026-10-11-sindri-ui-design.md
**Reviewed:** 2026-10-11

## Summary
Both pass-3 MEDIUMs hold in the UI spec: E-17 now names goldens per route and scenario (`<route-slug>/<scenario>.json`), fixes time and ids, calls itself an edit to API §12, and has U1 back-fill merged phases (§18.1). One residual MEDIUM: unlike E-1/E-6/E-7 (X18, X19, X21), E-17 has no cross-spec row in `docs/sindri/README.md`, and the API spec still never says "golden". No HIGH. Time-to-first-success: ~45 min for U0, ~2–3 h for a first proven U1 (needs API P1/P2 with goldens).

## Findings by severity
### CRITICAL
None.
### HIGH
None.
### MEDIUM
- **Backwards compatibility / docs:** E-17 obliges every API phase to write goldens, but an agent building an API phase reads the API spec and README. Neither carries it, while the other cross-spec E-edits are listed as README X-rows.
  - Evidence: §18.1 "E-17 is an edit to the API spec's §12"; absent: searched README for "E-17" and "golden" (0 hits); API spec for "golden" (0 hits).
  - Fix: add an X-row (e.g. X26) to README §13 and to this spec's §13 table: "API §12 gains the E-17 golden rule (per route and scenario, `FakeTimeProvider`, seeded ids)". Or apply the API §12 edit in the same PR that approves this spec.
### LOW
- **Ramp-up cost:** X23 (the nonce POST's class) is still open and gates U5's engine forms (§9.1), but U5's "Depends on" doesn't list it (carried from pass 3). Fix: add "X23 settled" to U5.
- **Error messages & observability:** "Warp hasn't confirmed: run `sindri doctor`" (§6.6, §12.4) points to a doctor that has no launcher check. Nothing in E-1…E-5 or the API spec adds one. Fix: give `sindri doctor` a launcher check in P10a (service alive, `launcher_cursor` lag, last `launch.probed`), or change the copy to name `GET /launch/targets` / Check again.
- **Docs:** E-18 sits between E-15 and E-16 in §6.7's table. Fix: reorder so implementers scanning by number don't miss it.

## Per-dimension summary
| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | — | 0 |
| Setup / install (time-to-first-success: ~45 min U0; ~2–3 h proven U1) | — | 0 |
| Error messages & observability | LOW | 1 |
| Docs completeness | MEDIUM | 2 (one shared with compat) |
| Backwards compatibility | MEDIUM | 1 |
| Ramp-up cost (tribal-knowledge items: 2 — API phases must write E-17 goldens; X23 gates U5) | LOW | 1 |

## Verdict
No CRITICAL or HIGH. The pass-3 E-17 fixes hold. The remaining MEDIUM is that the golden rule isn't yet recorded where API-phase builders will read it.
verdict: PASS
