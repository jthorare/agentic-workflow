---
name: addressReview
description: "Use when a PR already has review findings or reviewer comments and you want the code changed to resolve them, blocking ones first, and again as more comments arrive. Reads the saved review file, merges new human comments, fixes in parallel and marks items addressed. Not for running a review (review) or publishing one (postReview)."
argument-hint: [pr-number-or-url] [--all]
allowed-tools: Bash(gh *), Bash(git *), Bash(npm *), Bash(npx *), Bash(SHARED_DIR=*), Agent, Read, Write, Edit, Skill, AskUserQuestion, mcp__prism-mcp__session_search_memory, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

# Address PR Review

Implements fixes for outstanding review issues. The local state file is the source of truth — run this as many times as needed until all issues are resolved.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Step 1: Resolve the PR

**If an argument was provided**, use it directly:
```bash
gh pr view <argument> --json number,title,headRefName,baseRefName,url,headRepository
```

**If no argument**, auto-detect from the current branch:
```bash
gh pr list --head $(git branch --show-current) --json number,title,url
```

If multiple PRs are found, **Ask the user** to pick one.

If no PRs are found: "No open PR found for the current branch. Use `/addressReview <number>` to specify one."

## Step 2: Load State File

Read `$AW_DIR/reviews/{number}.json`.

If the file does not exist:
> "No local review state found for PR #{number}. Run `/review` first."

## Step 3: Fetch New Human Comments from GitHub

Fetch all GitHub comments created **after** the `human_comments_fetched_at` cursor in the state file (fall back to `reviewed_at` only if the cursor is unset — `reviewed_at` never advances):

```bash
# Top-level issue comments
gh api repos/{owner}/{repo}/issues/{number}/comments \
  --jq '[.[] | select(.created_at > "{human_comments_fetched_at}") | {id, body, user: .user.login, created_at, path: null, diff_position: null, source: "human"}]'

# Inline PR review comments
gh api repos/{owner}/{repo}/pulls/{number}/comments \
  --jq '[.[] | select(.created_at > "{human_comments_fetched_at}") | {id, body, user: .user.login, created_at, path, position, source: "human"}]'
```

Filter out comments posted by bots or CI systems (check `user.login` for `[bot]` suffix or known CI usernames).

Append any new comments to a `human_comments` array in the state file, then advance the cursor: set `human_comments_fetched_at` to now — this prevents re-triaging the same comments on every loop iteration.

## Step 4: Build the Issue List

Collect all unaddressed items:

**From state file** (`addressed: false`):
- All issues across all `reviewers[].issues` entries

**From new human comments** (all — humans comment when something needs attention):
- Each comment becomes a candidate issue for triage

**Filter by severity** (for structured issues):
- Default: `blocking` and `issue` only
- Pass `--all` to include `suggestion` and `nit`. `/specToProvenPR` fixes only blocking findings (every `issue`, plus anything that loses data, breaks a real path or makes a relied-on doc false, whatever its label) and resolves the rest in writing; see its "Blocking or not" section

Report to the user:
```
PR #{number}: "{title}"

Outstanding items:
  X blocking   (structured)
  Y issue      (structured)
  Z new human comments
  (W suggestions/nits excluded — pass --all to include)
Already addressed: N items
```

If everything is already addressed, stop:
> "All review items have been addressed. Consider running /postReview if you haven't published yet."

## Step 5: Triage for Implementation

**Spawn a subagent** (general-purpose) with the triage prompt from [address-triage-prompt.md](address-triage-prompt.md).

Inject:
- `{structured_issues}` — JSON array of unaddressed structured issues
- `{human_comments}` — JSON array of new human comments
- `{diff}` — output of `gh pr diff {number}`
- `{file_list}` — changed file paths

Parse the returned JSON array of implementation assignments.

## Step 6: Checkout and Spawn Parallel Implementers

Check out the PR branch:
```bash
gh pr checkout {number}
```

Then **Dispatch in parallel** all implementation agents at once (per `_shared/parallel-dispatch.md`). Each receives the prompt from [implementer-prompt.md](implementer-prompt.md) with:
- `{agent}`, `{focus}`, `{issues}` — from triage output
- `{number}`, `{owner}`, `{repo}`, `{branch}` — PR coordinates
- `{repo_slug}` — repo slug derived in the preamble

## Step 6.5: Re-verify Before Marking Addressed

Self-reported fixes are not proof. After all implementers complete:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
```

1. **Tests** — detect the runner per `$SHARED_DIR/test-runner-detection.md` (read it) and run the detected `TEST_CMD`.
2. **User-facing fixes** (routes, UI components, styles) — **Invoke skill `verify-app`** with args `--yes` and a journey covering each changed flow; require a non-FAIL verdict. A desktop UI fix runs `verify-app` the same way (it dispatches to `verify-desktop`, the real app against its mockups) and needs its pack not FAIL.
3. **Push once** — `git push origin {branch}`, only after 1–2 pass (implementers never push; one serialized push avoids parallel-push races). If tests fail or verify-app returns FAIL, the affected issues stay `addressed: false` — never mark an issue addressed without passing verification.

## Step 7: Update State File

After Step 6.5, update `$AW_DIR/reviews/{number}.json`:

For each result with status `fixed` or `answered` (the only statuses — there is no defer status), when covered by passing verification:
- Set `"addressed": true`
- Set `"addressed_at"` to current ISO timestamp
- Set `"addressed_by"` to the agent name
- Set `"fix_commit"` to the commit SHA the agent reported

For each `unresolved` entry an implementer returned: keep `"addressed": false` and record the reason under `"unresolved_reason"`.

For new human comments that were addressed, add them to the state file under `human_comments` with the same fields.

Update `"last_addressed_at"` at the top level.

Edit the file in place.

## Step 8: Report

```
Address complete for PR #{number}: "{title}"

Implemented:
  • security-engineer — 2 issues fixed (commit abc1234)
  • typescript-pro — 1 issue fixed (commit def5678)

WARNING — unresolved items remain (addressed: false):
  [blocking] src/auth.ts — JWT not verified (reason: requires refactor beyond flagged scope)
These are NOT deferred to a follow-up. Re-run /addressReview or fix manually before shipping.

Run /addressReview again to continue, or /postReview to publish.
```

## Git mode

`AW_GIT_MODE` is `${AW_GIT_MODE:-full}`.

- `full`: do everything this skill describes.
- `readonly`: work in the current directory. Make no branch, worktree, commit, push, PR, PR comment or review, and change no remote ref. Do the file edits and local checks the skill describes, and print each git or gh command you would have run under a `Would run:` heading.

## Next steps

- `/review` — verify fixes don't introduce new issues
- `/shipRelease` — if all blockers resolved, ship the changes

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/addressReview-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`addressReview`), `artifacts` (absolute paths: the review state file `$AW_DIR/reviews/<pr>.json` and any files the fixes changed), `summary` (one line),
plus `pr` (PR number); `fixed` (count of comments fixed); `remaining` (count of comments still open).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
