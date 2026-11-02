# Evidence Pack (shared)

Canonical verification-evidence layout (CD5) and `pack.json` schema. Producers: verify-web, verify-ios, verify-desktop (every run). Consumers: verify-app, specToProvenPR, shipRelease, review, postReview, weeklyRetro.
Referenced via: SHARED_DIR pattern (CD2).

## Layout (CD5)

Dir: `$AW_DIR/verification/<run-id>/` where `run-id = $(date -u +%Y%m%d-%H%M%S)-<slug>`.

Contains:
- `pack.json` — machine-readable result (schema below)
- `report.md` — human-readable summary
- screenshots named `<check>-<viewport>.png`
- raw cross-check output files

Newest pack: `ls -1dt "$AW_DIR/verification/"*/ | head -1`.

## pack.json schema

```json
{
  "schema": "evidence-pack/v1",
  "run_id": "20260727-153000-login-flow",
  "skill": "verify-web",
  "verdict": "PASS | WARN | FAIL",
  "platform": "web | ios | desktop",
  "base_url": "http://localhost:3000",
  "plan": "<path to verification-plan.md or null>",
  "lenses": [ { "name": "functional", "status": "PASS|WARN|FAIL|SKIPPED",
                "reason_if_skipped": null,
                "checks": [ { "id": 1, "desc": "...", "status": "PASS|FAIL", "evidence": "<text or artifact filename>" } ] } ],
  "journeys": [ { "name": "...", "status": "PASS|FAIL",
                  "steps": [ { "action": "click", "target": "...", "assertion": "...", "status": "PASS|FAIL" } ] } ],
  "cross_checks": [ { "claim": "...", "command": "...", "raw_output_file": "...", "status": "PASS|FAIL" } ],
  "artifacts": [ "01-login-mobile.png" ],
  "mockup_diff": { "screen": "...", "viewport": "...", "pct": 3.2, "verdict": "WARN" },
  "started_at": "...", "finished_at": "..."
}
```

## Producer/consumer contract

| Role | Skill | Obligation |
|---|---|---|
| Producer | verify-web | writes `pack.json` + `report.md` on every run |
| Producer | verify-ios | writes `pack.json` + `report.md` on every run |
| Producer | verify-desktop | writes `pack.json` + `report.md` on every run, with the desktop run's `summary.json` and images beside them |
| Consumer | verify-app | returns `{verdict, evidence_path}` from the sub-skill's pack |
| Consumer | specToProvenPR | stage proof links the pack's run-id dir (CD6) |
| Consumer | shipRelease | embeds newest pack in the PR `## Evidence` section |
| Consumer | review / postReview | inject newest pack as reviewer `{evidence}` |
| Consumer | weeklyRetro | trend analysis over pack verdicts |

**Gate rule:** a FAIL verdict, or a missing pack on a user-facing diff, blocks PR-open / mark-ready.
