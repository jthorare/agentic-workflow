---
name: canary
description: "Use when a deploy just went out and someone wants to know whether prod is okay: error rate climbing, latency creeping up, odd log lines, or a smoke probe failing after release. Watches those for a window against the pre-deploy numbers and returns HEALTHY, DEGRADED or UNHEALTHY. Not for shipping or deploying itself (shipRelease, landAndDeploy)."
argument-hint: "[release-id] [--duration <sec>] [--setup]"
allowed-tools: Bash, Read, Write, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Canary — Post-Deploy Monitoring

Watches prod error rate, latency, logs, and custom probes for a configurable window after deploy. Returns a verdict that drives the next-step chain.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

## Overview

Reads `.agentic-workflow/canary.json` for monitoring URLs and probes. Runs minute-by-minute checks for `duration` seconds (default 900 = 15 min). Computes a verdict: HEALTHY, DEGRADED, or UNHEALTHY. Writes a timeline to `releases/<release-id>/canary.md`. On HEALTHY, auto-chains `/syncDocs`. On DEGRADED, warns and stops. On UNHEALTHY, alerts and suggests `/rootCause` — does NOT auto-chain (human decides next step).

## Inputs

- Release ID (positional arg). If omitted, auto-discover latest:
  ```bash
  . "${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"
  ls -1d "$AW_DIR"/releases/*/ | sort -r | head -1
  ```
- `--duration <sec>` (optional). Overrides `canary.duration` from config.
- `--setup` flag: run interactive wizard to write `.agentic-workflow/canary.json`, then exit.
- `--skip-docs` flag (optional). When set, the `/syncDocs` auto-chain on HEALTHY verdict is suppressed. The flag also propagates from `/shipRelease` → `/landAndDeploy` → `/canary`, so passing `--skip-docs` to any upstream skill carries through to canary's chain decision.
- Config file: `.agentic-workflow/canary.json` in project root.

## Config schema (`.agentic-workflow/canary.json`)

```json
{
  "duration": 900,
  "errorRateUrl": "https://example.com/metrics/error-rate",
  "latencyUrl": "https://example.com/metrics/latency",
  "logSource": "kubectl logs deploy/api --since=60s",
  "logAnomalyPatterns": ["FATAL", "OOMKilled", "panic:"],
  "customProbes": [
    { "name": "checkout-flow", "command": "npm run smoke:checkout", "critical": true }
  ],
  "thresholds": {
    "errorRateDegradedMultiplier": 2,
    "errorRateUnhealthyMultiplier": 5,
    "latencyDegradedMultiplier": 2
  }
}
```

- `duration` — monitoring window in seconds.
- `errorRateUrl` — endpoint returning current error rate (numeric JSON or plain number).
- `latencyUrl` — endpoint returning `{p50,p95,p99}` in ms.
- `logSource` — shell command that prints recent log lines (last 60s).
- `logAnomalyPatterns` — substrings/regexes that mark a critical log anomaly.
- `customProbes[]` — extra checks. `critical:true` failures bump verdict to UNHEALTHY.
- `thresholds` — multipliers vs pre-deploy baseline. Captured from first probe minute.

## --setup Wizard

When invoked with `--setup`, **Ask the user** (one question per call):

1. Monitoring duration in seconds (default 900)
2. Error rate URL
3. Latency URL
4. Log source command (default `kubectl logs deploy/api --since=60s`; can be empty)
5. Log anomaly patterns (comma-separated; common defaults: `FATAL,OOMKilled,panic:`)
6. Custom probe name + command + critical? (loop until user says done)

Write `.agentic-workflow/canary.json`. Exit without monitoring.

## Steps (normal mode)

1. If `--setup`: run wizard and exit.

2. Resolve `$REPO_SLUG` via the shared toolkit path (shell state does not persist between shell calls — re-source in every bash block that uses it):
   ```bash
   SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
   source "$SHARED_DIR/repo-slug.sh"
   ```

3. Resolve release-id (arg or latest `releases/<ISO-date>-<short-sha>/` dir from the discovery command above).

4. Load `.agentic-workflow/canary.json`. If missing, suggest `--setup` and exit.

4a. **Refuse if config is checked into git.** Run `git ls-files --error-unmatch .agentic-workflow/canary.json 2>/dev/null` — if this returns 0 (file is tracked), error out with "canary.json is committed to the repo; remove it and run `/canary --setup` to write a local-only copy. The skill executes shell commands from this file and a tracked copy is a foot-gun." Recommend adding `.agentic-workflow/canary.json` to `.gitignore`.

5. Determine duration (`--duration` arg > config `duration` > default 900).

6. Resolve baseline values (**pre-deploy first**):
   - Read `releases/<release-id>/baseline.json` (persisted by `/landAndDeploy` **before** it deployed) → `baseline.errorRate`, `baseline.p95`, `baseline.logAnomalyCount`.
   - **Fallback:** if the file is missing (standalone deploy, older landAndDeploy), use the first probe minute's values and print a warning: "no pre-deploy baseline found — using post-deploy first-minute values; a regression introduced by this deploy may be masked."

7. Probe loop — every 60 s for `ceil(duration / 60)` iterations (so the final tail is probed even when duration isn't a multiple of 60):
   - Fetch `errorRateUrl` → current error rate
   - Fetch `latencyUrl` → current p50/p95/p99
   - If `logSource` set: run it; check stdout against `logAnomalyPatterns`. Record any matches.
   - For each `customProbes[]`: run the command; record exit code + duration.
   - Append a row to the timeline (in memory; written at the end).

8. Compute verdict after the loop:
   - **UNHEALTHY** if ANY of:
     - A `critical:true` custom probe failed at any point
     - Error rate exceeded `baseline.errorRate * errorRateUnhealthyMultiplier` for ≥2 consecutive minutes
     - Log anomalies exceeded the **rate threshold**: pattern matches in ≥2 distinct minutes, or ≥3 matches within a single minute, above `baseline.logAnomalyCount` (a single isolated match is NOT UNHEALTHY — noisy prod logs must not flip the verdict on one line)
   - **DEGRADED** if ANY of (and not UNHEALTHY):
     - A non-critical custom probe failed
     - Error rate exceeded `baseline.errorRate * errorRateDegradedMultiplier`
     - p95 latency exceeded `baseline.p95 * latencyDegradedMultiplier`
     - Exactly one isolated log anomaly match occurred
   - **HEALTHY** otherwise.

9. Write `$AW_DIR/releases/<release-id>/canary.md`:
   ```markdown
   # Canary — <release-id>

   **Started:** <ISO date>
   **Duration:** <sec>s
   **Verdict:** HEALTHY | DEGRADED | UNHEALTHY

   ## Baseline
   - Error rate: <baseline>
   - Latency p95: <baseline> ms

   ## Timeline
   | Minute | Error rate | p50 | p95 | p99 | Log anomalies | Probes |
   |---|---|---|---|---|---|---|
   | 1 | … | … | … | … | none | all pass |
   <…>

   ## Triggers
   - <reason verdict was DEGRADED/UNHEALTHY, or "no triggers" if HEALTHY>

   ## Recommended next
   - HEALTHY → /syncDocs (auto-chained)
   - DEGRADED → review timeline, decide whether to roll back
   - UNHEALTHY → /rootCause
   ```

10. Branch on verdict:
    - **HEALTHY:** if `--skip-docs` was passed (directly or propagated from shipRelease/landAndDeploy), print "skipping /syncDocs auto-chain (--skip-docs)" and exit. Otherwise **Invoke skill `syncDocs`**, passing the release-id and `--since <merge-sha>` (from `deploy.md`) so docs-sync scopes to exactly this release.
    - **DEGRADED:** print warning to stdout. Do NOT auto-chain.
    - **UNHEALTHY:** print alert and suggest `/rootCause`, passing **structured incident context** (rootCause's production-incident entry point) instead of a bare suggestion:
      ```json
      { "merge_sha": "<full-sha>", "release_id": "<release-id>",
        "symptom": "<which trigger fired: probe name / error-rate multiplier / anomaly pattern>",
        "logs_excerpt": "<the matching log lines and the timeline rows around the trigger>" }
      ```
      Do NOT auto-chain.

## Outputs

- `$AW_DIR/releases/<release-id>/canary.md` — globbed by `/weeklyRetro` for release-health trends
- `.agentic-workflow/canary.json` (only on `--setup`)

## Next steps

- `/syncDocs` — auto-chained on HEALTHY
- `/rootCause` — recommended on UNHEALTHY
- Manual rollback (`gh pr revert` or repo-specific path) — recommended on DEGRADED if rolling forward isn't viable
