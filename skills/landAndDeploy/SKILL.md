---
name: landAndDeploy
description: "Wait for PR merge, run deploy command, poll health, run smoke tests, then auto-chain /canary. Configured by .agentic-workflow/deploy.json."
argument-hint: "[pr#] [--wait|--no-wait] [--setup] [--skip-docs] [--chained-from-ship]"
allowed-tools: Bash, Read, Write, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Land and Deploy — Merge, Deploy, Smoke, Chain Canary

Bridges `/shipRelease` and `/canary`. Waits for PR merge, runs the user-defined deploy command, polls health, runs smoke tests, then auto-chains `/canary`.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

## Overview

Reads `.agentic-workflow/deploy.json` for deploy command, health URL, smoke tests, and timeout. On invocation, resolves the target PR (arg or current branch), optionally polls for merge, executes deploy, verifies health, runs smoke tests, writes a release record, and auto-invokes `/canary` on success. Skipped or `--no-deploy` callers from `/shipRelease` bypass this step entirely.

## Inputs

- PR number (positional arg). If omitted, detect from current branch via `gh pr view --json number,state,mergedAt`.
- `--wait` / `--no-wait` flag:
  - Default `--wait` when invoked from `/shipRelease` auto-chain (PR may not yet be merged at chain time)
  - Default `--no-wait` when invoked standalone (user is at the terminal, expects fast feedback)
- `--setup` flag: run interactive wizard to write `.agentic-workflow/deploy.json`, then exit.
- `--skip-docs` flag (optional). Propagates from `/shipRelease` and forwards to `/canary` on auto-chain, suppressing the eventual `/syncDocs` invocation downstream. This skill does not call `/syncDocs` directly — it only forwards the flag.
- `--chained-from-ship` flag (optional). Set automatically by `/shipRelease` when chaining. Activates graceful-degrade if `deploy.json` is missing, and instructs this skill to consume `~/.agentic-workflow/$REPO_SLUG/releases/.pending-ship.json` (written by shipRelease Step 7) and fold its ship-phase metadata into a `## Ship Phase` subsection at the top of `deploy.md`. A CLI flag propagates reliably across the skill-invocation boundary; env vars don't.
- Config file: `.agentic-workflow/deploy.json` in project root.

## Config schema (`.agentic-workflow/deploy.json`)

```json
{
  "command": "npm run deploy:prod",
  "healthUrl": "https://example.com/health",
  "smokeTests": ["curl -fsS https://example.com/api/ping"],
  "timeout": 600,
  "journey": { "baseUrl": "https://example.com", "args": "--lenses functional,error-state" }
}
```

- `command` — shell command to run for deploy. Streamed to stdout.
- `healthUrl` — URL polled after deploy; expects 200 OK.
- `smokeTests` — array of shell commands. Each must exit 0.
- `timeout` — seconds. Used for both deploy command and health polling.
- `journey` — optional. When present, the deployed app is driven post-smoke via `/verify-app` against `baseUrl` (see Step 12).

## --setup Wizard

When invoked with `--setup`, **Ask the user** (one question per call):

1. Deploy command (e.g., `npm run deploy:prod`)
2. Health URL (e.g., `https://example.com/health`)
3. Smoke test commands (comma-separated; can be empty)
4. Timeout in seconds (default 600)

Write `.agentic-workflow/deploy.json` (create the dir if missing). Exit without deploying.

## Steps (normal mode)

1. If `--setup`: run wizard above and exit.

2. Resolve target PR:
   - Arg given → use it.
   - Else: `gh pr view --json number,state,mergedAt` on current branch. If no PR exists, error: "no PR on this branch; create one with /shipRelease first".

3. Determine wait mode (`--wait` vs `--no-wait`) per defaults above unless explicitly overridden.

4. If PR not merged:
   - `--wait`: poll `gh pr view --json mergedAt` every 30 s, max 30 min. Print a progress line every 5 min (`waiting for merge: {elapsed}/{max}`), and write a resumable marker `~/.agentic-workflow/$REPO_SLUG/releases/.pending-deploy.json` (`{pr, branch, started_at}`) so a re-invocation resumes the same target instead of starting over. Stop on merge; delete the marker.
   - `--no-wait`: error: "PR #N not merged; pass `--wait` to poll or merge manually first".

5. Load `.agentic-workflow/deploy.json`.
   - If present: continue normally.
   - If missing AND invoked with `--chained-from-ship`: print a one-line note "no deploy.json — skipping deploy step (run `/landAndDeploy --setup` to enable)" and exit gracefully with success, so first-time users aren't blocked.
   - If missing AND standalone: error and suggest `--setup`.

6. **Refuse if config is checked into git.** Run `git ls-files --error-unmatch .agentic-workflow/deploy.json 2>/dev/null` — if it returns 0, error out: "deploy.json is committed; remove and run `/landAndDeploy --setup`. The skill executes `command` and `smokeTests[]` from this file as shell, so a tracked copy is an RCE foot-gun." Recommend adding `.agentic-workflow/deploy.json` to `.gitignore`. This refusal is the safety gate that justifies the broad Bash permission.

7. Compute release-id (CD7): `<ISO-date>-<short-sha>` where `<short-sha>` is the **merge commit SHA**. All release files live in `releases/<ISO-date>-<short-sha>/`.

8. Consume the ship handoff + capture the **pre-deploy baseline**:
   - If `--chained-from-ship`: read `$AW_DIR/releases/.pending-ship.json`, hold its metadata for the `## Ship Phase` subsection, then delete it.
   - If `.agentic-workflow/canary.json` exists: sample `errorRateUrl`, `latencyUrl` (p95), and one `logSource` run **now, before deploying**, and persist to `releases/<release-id>/baseline.json` (`{error_rate, p95, log_anomaly_count, sampled_at}`). `/canary` reads this so a deploy-introduced regression can never become its own baseline.

9. Run `deploy.command`, stream output. Capture exit code.
   - On non-zero exit: write `releases/<release-id>/deploy.md` with FAILED status, suggest `/rootCause`, exit.

10. Poll `deploy.healthUrl` with exponential backoff (1s, 2s, 4s, … capped at 60s between probes; max total `deploy.timeout` seconds). Stop on 200 OK.
    - On timeout: mark deploy DEGRADED in the release record. Continue to smoke tests anyway.

11. Run each `deploy.smokeTests[]` command sequentially. Capture each exit code and stdout.
    - Any non-zero → mark deploy DEGRADED.

12. If `deploy.journey` is configured: drive the deployed app — **Invoke skill `verify-app`** with args `--base-url <journey.baseUrl> --yes <journey.args>`. A deployed desktop app is not driven here (desktop evidence runs on seeded local data, before the release). A FAIL verdict → mark deploy DEGRADED and record the evidence-pack path in `deploy.md`.

13. Write `~/.agentic-workflow/$REPO_SLUG/releases/<release-id>/deploy.md`. When invoked with `--chained-from-ship`, include a `## Ship Phase` subsection at the top from the `.pending-ship.json` metadata. The merge-SHA-based release-id ensures this file lives in the same subdir as `canary.md`, so the full release (ship + deploy + canary + docs-sync) lives under one folder.
    ```markdown
    # Deploy — <release-id>
    
    **PR:** #<num>
    **Merge SHA:** <full-sha>
    **Deployed:** <ISO date>
    **Verdict:** SUCCESS | DEGRADED | FAILED

    ## Ship Phase
    <!-- Only when --chained-from-ship: folded from releases/.pending-ship.json (then deleted). -->
    - **Branch:** <branch> → <base>
    - **Tip SHA (pre-merge):** <short-sha>
    - **Test result:** passed (<N> tests)
    - **Coverage:** <percentage>% (or "not available")
    - **PR URL:** <url>

    ## Deploy command
    `<deploy.command>` — exit <code> in <duration>s
    
    ## Health
    `<deploy.healthUrl>` — first OK at <Ns>, took <total>s
    
    ## Smoke tests
    | Test | Exit | Duration |
    |---|---|---|
    | <cmd> | 0 | <s> |
    
    ## Output excerpts
    <last 50 lines of deploy command output, smoke test stdout snippets>
    ```

14. Branch on verdict:
    - **SUCCESS:** auto-**Invoke skill `canary`**, passing the release-id. If this skill received `--skip-docs` (directly or propagated from `/shipRelease`), forward `--skip-docs` to `/canary` so it suppresses its own `/syncDocs` auto-chain on HEALTHY.
    - **DEGRADED:** do NOT silently stop — **Ask the user:** "Deploy is DEGRADED ({reason}). Proceed to /canary monitoring, roll back, or stop?" Options: `Proceed to canary` (invoke `/canary` with the release-id), `Roll back` (print the rollback path — `gh pr revert` or repo-specific — and stop), `Stop` (record the choice in deploy.md and exit).
    - **FAILED:** already handled in step 9.

## Outputs

- `~/.agentic-workflow/$REPO_SLUG/releases/<release-id>/deploy.md`
- `~/.agentic-workflow/$REPO_SLUG/releases/<release-id>/baseline.json` (pre-deploy metrics for `/canary`)
- Consumes + deletes `releases/.pending-ship.json`; `deploy.md` is read by `/canary` (baseline window) and globbed by `/weeklyRetro`
- `.agentic-workflow/deploy.json` (only on `--setup`)

## Git mode

`AW_GIT_MODE` is `${AW_GIT_MODE:-full}`.

- `full`: do everything this skill describes.
- `readonly`: work in the current directory. Make no branch, worktree, commit, push, PR, PR comment or review, and change no remote ref. Do the file edits and local checks the skill describes, and print each git or gh command you would have run under a `Would run:` heading.

## Next steps

- `/canary` — auto-chained on SUCCESS
- `/rootCause` — if deploy or smoke fails
- `/shipRelease --no-deploy` — for next time if you want to skip the auto-chain (e.g., release-branch workflow)
