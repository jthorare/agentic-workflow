---
name: shipRelease
description: "Use when a branch is finished and the person wants it out the door: sync it with the base branch, run the tests, check coverage, then publish the branch and create the pull request, and hand off to landAndDeploy (skip with --no-deploy). Not for waiting on a merge or deploying a PR that is already open (landAndDeploy), or watching a live release (canary)."
argument-hint: "[--base main] [--skip-docs] [--no-deploy] [--min-coverage <pct>]"
allowed-tools: Bash(git *), Bash(gh *), Bash(npm *), Bash(npx *), Bash(pytest *), Bash(cargo *), Bash(go *), Bash(bundle *), Bash(coverage *), Bash(ls *), Bash(cat *), Bash(mkdir *), Bash(test *), Bash(SHARED_DIR=*), Bash(source *), Read, Write, Glob, Grep, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

# Ship Release

Syncs your branch, runs tests, audits coverage, pushes, opens a PR, and optionally invokes `/syncDocs`.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

## Step 1: Pre-flight Checks

1. Confirm the working tree is clean:
   ```bash
   git status --porcelain
   ```
   If the output is non-empty, **stop** and ask the user to commit or stash their changes before continuing.

2. Parse arguments:
   - `--base <branch>` — the base branch to rebase onto and target for the PR. Default: `main`.
   - `--skip-docs` — if present, skip the `/syncDocs` invocation in the final auto-chain step.
   - `--no-deploy` — if present, skip the `/landAndDeploy` auto-chain (Step 7) and fall through directly to `/syncDocs`. Use for release-branch workflows where merge happens elsewhere or deployment is manual.
   - `--min-coverage <pct>` — optional. If coverage is measurable and below this threshold, **stop** before push and report.

3. Derive the current branch name:
   ```bash
   git branch --show-current
   ```
   If on `main` (or the same as `--base`), **stop** and tell the user: "You are on the base branch. Check out a feature branch first."

## Step 2: Sync

Fetch the latest from origin and rebase on the base branch:

```bash
git fetch origin
git rebase origin/{base}
```

If the rebase encounters conflicts, **stop** immediately and tell the user:
> "Rebase conflicts detected. Resolve them manually, then run `/shipRelease` again."

Do **not** attempt to auto-resolve conflicts.

## Step 3: Test

Resolve the shared dir from this skill's own symlink, then detect the runner:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
```

Read `$SHARED_DIR/test-runner-detection.md` and set `TEST_CMD` from its runner table. If no runner is detected, report "tests: n/a — excluded from scoring" and continue (never score an undetected runner as a failure).

Run `TEST_CMD`. If **any tests fail**, **stop** and report:
- Which test runner was used
- The full failure output
- A summary of which tests failed

Do not proceed to push or PR creation on test failure.

## Step 4: Coverage Audit

Set `COVERAGE_CMD` from the coverage table in `$SHARED_DIR/test-runner-detection.md` (already read in Step 3).

If coverage tooling is available:
- Run `COVERAGE_CMD` and report the overall coverage percentage.
- List any files below 80% coverage as warnings (do not fail the release for low coverage).
- If `--min-coverage` was passed and overall coverage is below it, **stop** and report the shortfall.

If no coverage tools are detected, note "Coverage: not available" and continue.

## Step 4.5: App Verification (user-facing diffs)

Check whether the diff touches UI or routes:

```bash
git diff origin/{base}..HEAD --name-only
```

If any changed file is a UI component, page, route, template, or stylesheet:

1. **Invoke skill `verify-app`** with args `--yes auto` — auto mode infers screens from the diff; `--yes` skips the confirmation re-prompt. A `FAIL` verdict **blocks the push**: stop and report the evidence-pack path.
   For a desktop UI change (`_shared/platform-detection.md`) this runs `verify-desktop`: the real app on seeded data against its mockups. A desktop UI diff with no desktop evidence pack blocks the push.
2. **Baseline check:** if `$AW_DIR/design/screens.json` exists and any of its screens match the changed files/routes, also **Invoke skill `design-verify`**. A FAIL diff (>10%) blocks the push. A desktop UI change always gets `design-verify` (its mockups come from the repo's screens manifest, not `screens.json`); any FAIL blocks the push.

If the diff is not user-facing, note "verification: n/a (no UI/route changes)" and continue.

## Step 5: Push

Push the branch to origin:

```bash
git push origin {branch}
```

If the push fails (e.g., rejected due to non-fast-forward), report the error and stop.

## Step 6: Open PR

**Duplicate check first.** If a PR already exists for this branch, report its URL and skip creation:
```bash
gh pr list --head {branch} --base {base} --json number,url
```

Build the PR body from the template in `$SHARED_DIR/pr-body.md` (read it; re-source `SHARED_DIR` as in Step 3 — shell state does not persist between shell calls):

- `## Summary` — commits since divergence: `git log origin/{base}..HEAD --format="- %s" --no-merges`, plus the "why".
- `## Evidence` — pull the **newest verification pack**:
  ```bash
  source "$SHARED_DIR/repo-slug.sh"
  ls -1dt "$AW_DIR/verification/"*/ 2>/dev/null | head -1
  ```
  Embed the pack's `evidence` text per `$SHARED_DIR/evidence-pack.md`: the verdict line, journey table, mockup diff %, and cross-check raw output — plus artifact paths. Never just a pointer.
- `## Tests` — runner + counts from Step 3. `## Coverage` — % from Step 4 (or "not available") + files below 80%.
- `## Key decisions` / `## Review resolution` per the template (`None.` when empty).

**Draft-only rule (CD13):** if the diff is user-facing (Step 4.5) and the verification pack is **missing or its verdict is FAIL**, do NOT open a ready PR — create it with `--draft` and a warning checkbox noting the missing/failed evidence.

Create the PR with `gh pr create --base {base} --head {branch} --title "{branch}" --body "..."` (adding `--draft` when the rule above applies). Capture the PR URL and number for the report and the next step.

## Step 7: Auto-chain `/landAndDeploy`

Unless `--no-deploy` was passed, first write the structured ship→deploy handoff (read + deleted by `/landAndDeploy`):

```bash
source "$SHARED_DIR/repo-slug.sh"
mkdir -p "$AW_DIR/releases"
cat > "$AW_DIR/releases/.pending-ship.json" <<EOF
{ "branch": "{branch}", "base": "{base}", "tip_sha": "{short-sha}",
  "pr": {number}, "pr_url": "{url}", "tests": "passed ({N} tests)",
  "coverage": "{pct}% | not available", "evidence_pack": "{newest pack dir or null}",
  "shipped_at": "{ISO timestamp}" }
EOF
```

Then **Invoke skill `landAndDeploy`** with args `--wait --chained-from-ship`, passing the PR number captured in Step 6. If the user passed `--skip-docs` to `shipRelease`, also pass `--skip-docs` to `/landAndDeploy`.

The `--chained-from-ship` flag tells `/landAndDeploy` to graceful-degrade if `deploy.json` is missing (a first-time user's `shipRelease` shouldn't hard-fail because the deploy wizard hasn't been run yet). It also tells `/landAndDeploy` to fold the ship-phase metadata from `.pending-ship.json` into its `deploy.md` output rather than it being written by `shipRelease`.

The `--wait` flag tells `/landAndDeploy` to poll for merge before deploying — so this step works correctly even though the PR may not yet be merged at the moment `shipRelease` completes.

The `--skip-docs` flag propagates through the entire chain: `shipRelease → landAndDeploy → canary → syncDocs`. Both `/landAndDeploy` and `/canary` forward the flag; `/canary` suppresses its own auto-`/syncDocs` invocation when the flag is set.

On successful deploy, `/landAndDeploy` auto-chains `/canary`, which on a `HEALTHY` verdict auto-chains `/syncDocs` (unless `--skip-docs` was propagated). The full chain becomes:

```
shipRelease → landAndDeploy → canary → syncDocs
```

If `--no-deploy` was passed, skip this step entirely and fall through to Step 8 (direct `/syncDocs` invocation). Use `--no-deploy` for release-branch workflows where merge happens elsewhere or deployment is manual.

Record whether the deploy chain was started or skipped, and whether `--skip-docs` was forwarded.

## Step 8: Invoke /syncDocs (fallback)

If Step 7 was skipped (because `--no-deploy` was passed) and `--skip-docs` was **not** passed, invoke the `/syncDocs` skill directly to update documentation:

```
/syncDocs
```

If Step 7 ran, do **not** invoke `/syncDocs` here — the `canary → syncDocs` chain handles it.

If `--skip-docs` was passed, skip this step.

Record whether docs were updated, deferred to the canary chain, or skipped.

## Step 9: Report

**Release record handoff.** The canonical release-id scheme (CD7) is `<ISO-date>-<short-sha>`, one dir per release: `releases/<ISO-date>-<short-sha>/`. In the default chain the short SHA is the **merge commit SHA**, generated by `/landAndDeploy` — the canonical post-merge identifier `/canary` also uses. shipRelease cannot know the merge SHA at push time (especially for squash merges), so:

- **Default chain (`/landAndDeploy` is invoked in Step 7):** do **NOT** write a separate `ship.md` here. The ship-phase metadata travels via `releases/.pending-ship.json` (Step 7); `/landAndDeploy` folds it into a `## Ship Phase` subsection at the top of its `deploy.md`. This guarantees `deploy.md` and `canary.md` always live in the same `releases/<ISO-date>-<short-sha>/` subdir — no split across two dirs when tip-SHA ≠ merge-SHA.

- **`--no-deploy` invocation (Step 7 skipped):** since no `/landAndDeploy` will run, shipRelease writes a standalone ship summary to `$AW_DIR/releases/<ISO-date>-<tip-short-sha>/ship.md` — same CD7 id form, using the **tip SHA** since no merge SHA exists yet (e.g. `2026-05-15-a1b2c3d`). Deploy information will not be available.

  ```markdown
  # Release: {branch}

  - **Release ID:** {ISO-date}-{tip-short-sha}
  - **Date:** {ISO timestamp}
  - **Base:** {base}
  - **Branch:** {branch}
  - **Tip SHA:** {short-sha}
  - **Test result:** passed ({N} tests)
  - **Coverage:** {percentage}% (or "not available")
  - **Files below 80%:** {list or "none"}
  - **PR:** {url}
  - **Deploy chain:** skipped (--no-deploy)
  - **--skip-docs forwarded:** n/a (no deploy chain)
  - **Docs updated:** {yes / skipped}
  ```

Print a summary to the user:

```
Release shipped!
  Branch:  {branch} → {base}
  Tests:   passed
  PR:      {url}
  Deploy:  {chained via /landAndDeploy --wait --chained-from-ship | skipped (--no-deploy)}
  Docs:    {updated | deferred to canary chain | skipped}
  Report:  {handed to /landAndDeploy via .pending-ship.json | $AW_DIR/releases/{ISO-date}-{tip-short-sha}/ship.md}
```

## Git mode

`AW_GIT_MODE` is `${AW_GIT_MODE:-full}`.

- `full`: do everything this skill describes.
- `readonly`: work in the current directory. Make no branch, worktree, commit, push, PR, PR comment or review, and change no remote ref. Do the file edits and local checks the skill describes, and print each git or gh command you would have run under a `Would run:` heading.

## Next steps

- (auto) `/landAndDeploy` — chained after PR creation, polls for merge then deploys (skip with `--no-deploy`)
- (auto) `/canary` — chained by `/landAndDeploy` on successful deploy
- (auto) `/syncDocs` — chained by `/canary` on `HEALTHY` verdict (or directly by `shipRelease` if `--no-deploy`)

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/shipRelease-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`shipRelease`), `artifacts` (absolute paths: `$AW_DIR/releases/<id>/ship.md` and `.pending-ship.json` when the deploy chain follows), `summary` (one line),
plus `pr` (PR number); `pr_url` (PR URL); `draft` (true when the PR was opened as a draft); `tip_sha` (branch tip sha).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
