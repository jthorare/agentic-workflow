# Design Artifact Paths (shared)

Single source of truth for every file under `$AW_DIR/design/`: owner skill, consumer skills, and the comparison-report schema. Referenced by all design-* skills, verify-web, shipRelease, specToProvenPR, and `.agents/rules/design.md`.
Referenced via: SHARED_DIR pattern (CD2).

## Artifact table

| Artifact | Owner (writes) | Consumers (read) |
|---|---|---|
| `design-tokens.json` | design-analyze-web/-ios, design-evolve-* | design-language, design-mockup-*, design-implement-*, design-refine |
| `.impeccable.md` | design-language (evolve appends `## Sources`) | design-mockup-*, design-implement-*, design-refine |
| `screens.json` (CD4 schema below) | design-mockup-web/-ios | design-verify-web/-ios, verify-web (`--visual`), design-implement gate, shipRelease/specToProvenPR baseline check |
| Baselines (CD3): `mockup-web-<screen>-<viewport>.png` (viewports `mobile` 375×812, `tablet` 768×1024, `desktop` 1440×900); `mockup-ios-<screen>.png`, optional `mockup-ios-<screen>-dark.png` | design-mockup-web/-ios | design-verify-web/-ios, verify-web visual lens |
| `mockup-<screen>.html` | design-mockup-web | design-implement-web |
| `Mockup-<screen>.swift` (persisted, not deleted) | design-mockup-ios | design-implement-ios |
| `raw/<host>.json` (pinned dembrandt output) | design-analyze-web, design-evolve-web | token-merge diff |
| `reference-<host>.png` | design-language | design-refine |
| `shotgun/variant-N.html`, `shotgun/contact-sheet.html`, `shotgun/picked.json` | design-shotgun | design-mockup-web Step 0 |
| `refine-log.md` | design-refine | design-refine (Step 0 resume) |
| `implement-report.json` | design-implement-web | design-verify, review |
| `verify/<run-id>/<screen>-<viewport>-diff.png`, `verify/<run-id>/comparison-report.json` | design-verify-web/-ios | design-verify dispatcher, review, shipRelease |

Legacy baselines `mockup-<screen>.png` / `mockup-ios.png` are still globbed by verify skills with a "legacy baseline — re-run /design-mockup to upgrade" warning.

## screens.json schema (CD4)

```json
{ "schema": "screens/v1",
  "viewports": {"mobile":"375x812","tablet":"768x1024","desktop":"1440x900"},
  "screens": { "<screen>": {
      "route": "/path | null",
      "nav": [ {"action":"tap","target":"<label or x,y from snapshot_ui>"} ],
      "baselines": {"mobile":"mockup-web-<screen>-mobile.png"},
      "approved_at": "<ISO> | null",
      "baseline_stale": "<ISO> | null",
      "source": "design-mockup-web | design-mockup-ios" } } }
```

`nav` may be `null`. `baseline_stale` is set by design-evolve on token adoption.

## comparison-report.json schema

```json
{ "schema": "comparison-report/v1", "run_id": "...",
  "screens": [ { "screen": "...", "viewport": "...", "baseline": "...", "capture": "...",
                 "diff_image": "...", "diff_pct": 0.0, "verdict": "PASS|WARN|FAIL" } ],
  "overall": { "max_diff_pct": 0.0, "verdict": "PASS|WARN|FAIL" } }
```

`diff_pct` is bound to the numeric diff-percentage field of the `mcp: design-comparison/compare_design` response. CD11 thresholds: `≤2%` PASS, `2–10%` WARN, `>10%` FAIL. design-implement completion gate: PASS or WARN (≤10%); FAIL ⇒ `[BLOCKED]`.

## Report-honesty rule

Pixel diffs may only claim **region-level** deviations ("header area differs by N%"). Token-level attribution ("wrong `--color-accent`") requires a `mcp: playwright/browser_evaluate` computed-style step (web) and is otherwise forbidden.
