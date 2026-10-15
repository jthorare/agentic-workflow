# Plan Devex Review — sindri-ui-design (pass 5, revision 5)

**Plan:** docs/superpowers/specs/2026-10-11-sindri-ui-design.md
**Reviewed:** 2026-10-11

## Summary
All pass-4 devex items hold in the text. X26 is in §13 (l.816) and README §13 (l.110). X23 is in U5's "Depends on" (l.1055). `sindri doctor` gains a `launcher` line in E-1 (l.519), so §6.6's copy leads somewhere. E-18 is now in order (l.535–537). The revision-5 changes (`launch.confirm`, a claimed set, the 10 s `Launch` limit, E-19) introduce no HIGH or MEDIUM DX friction. Two LOW pointer gaps remain around E-19. Time-to-first-success: ~45 min for U0, ~2–3 h for a first proven U1 (needs API P1/P2 with E-17 goldens).

## Findings by severity
### CRITICAL
None.
### HIGH
None.
### MEDIUM
None.
### LOW
- **Docs / ramp-up:** E-19 is a WF spec edit (`StartInstance` starts taken over). Like E-6 (X19), it lands where WF builders work, but README §13 has no row for it. A WF-P5 agent reads the WF spec and README, not UI §6.7.
  - Evidence: §6.7 l.537, §18.1 l.1071; absent — searched README for "E-19" (0 hits).
  - Fix: fold E-19 into X19's row ("WF P5: the E-6 trigger and E-19"), or add a new X-row.
- **Ramp-up:** U6b's "Depends on" names "API P10b and WF's E-6 trigger" but not E-19. §18.1 pairs them.
  - Evidence: §18 U6b row, l.1056.
  - Fix: change it to "WF's E-6 trigger and E-19".

## Per-dimension summary
| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | — | 0 |
| Setup / install (time-to-first-success: ~45 min U0; ~2–3 h proven U1) | — | 0 |
| Error messages & observability | — | 0 |
| Docs completeness | LOW | 1 |
| Backwards compatibility | — | 0 |
| Ramp-up cost (tribal-knowledge items: 1 — E-19 rides with WF P5's E-6 edit) | LOW | 1 |

## Verdict
No CRITICAL, HIGH or MEDIUM. Every pass-4 fix holds, and only two LOW E-19 pointer gaps remain.
verdict: PASS
