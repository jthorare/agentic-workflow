---
name: weeklyRetro
description: "Use when someone wants to look back on the last week or few weeks of work: who did what, how steady the shipping was, streaks, test health, where the churn and reverts are. Reads git history and writes a retro report with a JSON sibling. Not for refreshing README or CHANGELOG after a ship (syncDocs) or the status of one PR."
argument-hint: "[--weeks N] [--team user1,user2,...]"
allowed-tools: Bash(git *), Bash(npm *), Bash(npx *), Bash(pytest *), Bash(cargo *), Bash(go *), Bash(bundle *), Bash(coverage *), Bash(date *), Bash(WEEKS=*), Bash(ls *), Bash(cat *), Bash(SHARED_DIR=*), Bash(source *), Read, Write, Glob, Grep, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Weekly Retrospective

Analyzes git history to produce per-person breakdowns, shipping streaks, test health trends, and actionable insights.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

## Step 1: Parse Arguments

- `--weeks N` — number of weeks to analyze. Default: `1`.
- `--team user1,user2,...` — comma-separated list of contributors to include. Default: all contributors in the period.

Compute the `--since` date (substitute the parsed weeks count into `WEEKS` — never leave a placeholder):
```bash
WEEKS=1   # <- set from --weeks N (default 1)
SINCE_DATE=$(date -v-"${WEEKS}"w +%Y-%m-%d 2>/dev/null || date -d "${WEEKS} weeks ago" +%Y-%m-%d)
if [ -z "$SINCE_DATE" ]; then
  echo "ERROR: could not compute SINCE_DATE (both BSD and GNU date forms failed)."
fi
echo "since: $SINCE_DATE"
```

**If `SINCE_DATE` is empty, stop.** An empty `--since` would silently turn the retro into an all-history analysis — report the error to the user instead of proceeding.

## Step 2: Gather Data

Run the following git commands to collect raw data:

```bash
# Commits by author (summary)
git shortlog -sne HEAD --since="$SINCE_DATE" --no-merges

# Commit details: hash, author name, author email, subject, ISO date
git log --since="$SINCE_DATE" --no-merges --format="%H|%an|%ae|%s|%aI"

# File change stats per commit
git log --since="$SINCE_DATE" --no-merges --stat --format=""

# Lines changed by author (numstat format)
git log --since="$SINCE_DATE" --no-merges --format="%an" --numstat
```

If `--team` was specified, filter all data to only include the listed contributors.

## Step 3: Per-Person Breakdown

For each contributor, compute:

| Metric | How |
|--------|-----|
| **Commits** | Count of commits by this author |
| **Lines added** | Sum of additions from `--numstat` |
| **Lines removed** | Sum of deletions from `--numstat` |
| **Files touched** | Unique file paths from `--numstat` |
| **Top areas** | Top 3 directories by number of files changed |
| **Commit types** | Breakdown by conventional commit prefix: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `other` |

Format as a table per person.

## Step 4: Shipping Streaks

For each contributor, find consecutive calendar days with ≥1 commit, the **longest streak** in the period, and the **current streak** (still active today?). A "day" is the author's commit date (not committer date).

```bash
# Get commit dates per author
git log --since="$SINCE_DATE" --no-merges --format="%an|%aI" | sort
```

## Step 5: Test Health

Run the project's test suite to capture current health:

1. Detect the test runner per `skills/_shared/test-runner-detection.md` (sets `TEST_CMD`). No runner detected ⇒ report test health as **"n/a — excluded"**, never as a failure.
2. Run `$TEST_CMD` and capture:
   - Total pass/fail count
   - Any test failures (names and messages)

3. Find test files added in the period:
   ```bash
   git log --since="$SINCE_DATE" --no-merges --diff-filter=A --name-only --format="" -- "*.test.*" "*.spec.*" "test_*" "*_test.*"
   ```

4. If a previous retro JSON exists, read the **most recent** `$AW_DIR/retros/*-weekly.json` (excluding today's) and compare:
   - Tests that started failing since last retro
   - Change in total test count
   ```bash
   SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
   source "$SHARED_DIR/repo-slug.sh"
   ls -1t "$AW_DIR/retros/"*-weekly.json 2>/dev/null | head -2
   ```

## Step 5.5: Pipeline Artifacts

Pull this period's skill-pipeline outputs into the retro (skip any dir that doesn't exist):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
ls -1dt "$AW_DIR/releases/"*/ 2>/dev/null | head -10          # release records: ship/deploy/canary/docs-sync
ls -1t "$AW_DIR/qa/"*.md 2>/dev/null | head -10               # bugHunt / bugReport outputs
ls -1t "$AW_DIR/verification/"*/pack.json 2>/dev/null | head -10  # evidence packs
```

Read the files whose timestamps fall in the period:
- `releases/<id>/` (`ship.md`, `deploy.md`, `canary.md`, `docs-sync.md`) → releases shipped + canary verdicts.
- `qa/*.md` → bugs found/fixed, health-score snapshots.
- `verification/*/pack.json` → count PASS / WARN / FAIL verdicts (verification trend).

## Step 6: Generate Insights

Analyze the collected data to produce:

### What Shipped
Group commits by area (top-level directory) and type (feat/fix). Summarize as bullet points:
- **area-name**: description of what changed (N commits)

Include releases from Step 5.5: `- release <id>: canary {verdict}`.

### Velocity Trend
If a previous retro JSON exists (Step 5, item 4):
- Compare total commits, lines changed, and contributors.
- Note if velocity is up, down, or steady.

If no previous retro exists, note "First retro — no baseline for comparison."

### Risk Areas
Identify files or directories that may need attention:
- **High churn**: files modified in 3+ separate commits by 2+ authors.
- **Large files**: any single file with 500+ lines changed.
- **Ownership gaps**: directories touched by only one person (bus factor = 1).

### Suggested Focus
Based on the data, suggest 2-3 concrete actions for the next week. **Every suggestion must cite the metric that motivates it**, in the form `(evidence: <metric>=<value> in <path>)` — a suggestion without computed evidence is dropped:
- Areas with high churn that might benefit from refactoring — e.g. `(evidence: commits=7 by 3 authors in src/db/client.ts)`
- Test coverage gaps, only if coverage data was actually captured — e.g. `(evidence: coverage=64% in verification/<run-id>/pack.json)`
- Knowledge sharing opportunities — e.g. `(evidence: authors=1 in judge/src/prompt-sort/)`
- Verification failures to chase — e.g. `(evidence: verdict=FAIL in verification/<run-id>/pack.json)`

## Step 7: Write Report

Write the retrospective report to `$AW_DIR/retros/{date}-weekly.md` where `{date}` is `YYYY-MM-DD` format:

```markdown
# Weekly Retrospective: {start_date} to {end_date}

## Team Summary

| Contributor | Commits | Lines +/- | Files | Top Area | Streak |
|-------------|---------|-----------|-------|----------|--------|
| {name} | {N} | +{add}/-{del} | {N} | {dir} | {N} days |

## Per-Person Details

### {Name}

| Type | feat | fix | refactor | test | docs | chore | other |
|------|------|-----|----------|------|------|-------|-------|
| Count | {N} | {N} | {N} | {N} | {N} | {N} | {N} |

**Top areas:** {dir1}, {dir2}, {dir3}
**Longest streak:** {N} consecutive days
**Current streak:** {N} days (active/ended)

## Shipping Streaks

| Contributor | Longest | Current | Active? |
|-------------|---------|---------|---------|
| {name} | {N} days | {N} days | {yes/no} |

## Test Health

- **Suite:** {runner, or "n/a — no runner detected (excluded)"}
- **Result:** {pass}/{total} passed
- **New tests added:** {N}
- **Trend:** {+N tests since last retro / first retro}

## Verification & QA

- **Evidence packs this period:** {N} ({N} PASS / {N} WARN / {N} FAIL)
- **QA reports:** {N} ({paths})
- **Releases:** {list of releases/<id> with canary verdicts, or "none"}

## What Shipped

{bulleted list grouped by area}

## Velocity Trend

{comparison to previous retro or "First retro — no baseline."}

## Risk Areas

{bulleted list of high-churn files, large changes, ownership gaps}

## Suggested Focus for Next Week

{2-3 actionable suggestions}
```

**Also write the machine-readable sibling** `$AW_DIR/retros/{date}-weekly.json` — this is what the next retro's trend comparison reads:

```json
{
  "schema": "weekly-retro/v1",
  "period": { "start": "{start_date}", "end": "{end_date}", "weeks": N },
  "totals": { "commits": N, "lines_added": N, "lines_removed": N, "contributors": N },
  "contributors": [ { "name": "...", "commits": N, "added": N, "removed": N, "files": N, "longest_streak": N } ],
  "tests": { "runner": "npm test | pytest | ... | n/a", "passed": N, "total": N, "new_tests": N },
  "verification": { "packs": N, "pass": N, "warn": N, "fail": N },
  "releases": [ "<release-id>" ],
  "qa_reports": N
}
```

Print a summary to the user:

```
Weekly retro complete ({start_date} to {end_date}).
  Contributors: {N}
  Total commits: {N}
  Test health:   {pass}/{total} passed (or n/a)
  Verification:  {N} packs ({N} FAIL)
  Report:        $AW_DIR/retros/{filename} (+ .json sibling)
```

## Next steps

- `/officeHours` — start the next cycle with a feature spec
- `/bugReport` — triage the risk areas surfaced above into a scored health report
- `/syncDocs` — refresh docs if the period shipped releases without a docs pass
