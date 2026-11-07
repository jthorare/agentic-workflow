---
name: review
description: "Use when someone wants a pull request looked over before it merges: \"review my PR\", check this diff for bugs, security holes, weak tests or unhandled edge cases. Runs specialist reviewers in parallel and saves the findings locally without posting. Not for fixing saved comments (addressReview) or publishing them (postReview)."
argument-hint: [pr-number-or-url] [--lens <persona,...>]
allowed-tools: Bash(gh *), Bash(git *), Bash(ls *), Bash(mkdir *), Bash(SHARED_DIR=*), Agent, Read, Write, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

# PR Review Orchestrator

Runs parallel domain-specific reviewers and saves findings locally. Does **not** post to GitHub — run `/postReview` when ready.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Git mode

`AW_GIT_MODE` is `${AW_GIT_MODE:-full}`.

- `full`: do everything this skill describes. The skill reads the PR and writes findings locally; it never posts a review or comment (`/postReview` does that).
- `readonly`: the same, since the skill changes no branch, worktree, commit, push, PR, PR comment or review and no remote ref. If a step would run one, print it under a `Would run:` heading instead.

## Under Sindri (`AW_RESULT_SKILL=review`)

Only when the Result block's gate printed `AW_RESULT_SKILL=review`. The Step has no GitHub network, so:

- **Read the PR from the local ref.** `$AW_SINDRI_PR_REF` is a ref the host already fetched. Replace Step 1 and Step 2's `gh` calls: take `BASE` as the merge base of `$AW_SINDRI_PR_REF` and the default branch (`origin/HEAD` when set, else `main`, else `master`); the diff is `git diff $BASE $AW_SINDRI_PR_REF`, the changed files come from `git diff --name-only $BASE $AW_SINDRI_PR_REF`, the head commit is `git rev-parse $AW_SINDRI_PR_REF`, and the title and body come from the commit messages in `git log $BASE..$AW_SINDRI_PR_REF`. Make no `gh` call at all. The PR number is the `pr` input (a number, or the number at the end of a link).
- **Never post.** No `gh pr review`, no comment; `/postReview` stays the owner's.
- **Questions become `waiting`** (for example, several PRs match or the ref is missing), never AskUserQuestion.
- Add `verdict` and `review_file` to the result.

## Step 1: Resolve the PR

**If an argument was provided**, use it directly:
```bash
gh pr view <argument> --json number,title,headRefName,baseRefName,url,headRepository
```

**If no argument was provided**, auto-detect from the current branch:
```bash
gh pr list --head $(git branch --show-current) --json number,title,url
```

If multiple PRs are returned, **Ask the user** to pick one before proceeding.

If no PRs are found: "No open PR found for the current branch. Use `/review <number>` to specify one."

## Step 2: Fetch PR Context

Run in parallel:
```bash
gh pr diff {number}
gh pr view {number} --json number,title,body,additions,deletions,headRefName,baseRefName,headRepository
gh pr view {number} --json files --jq '[.files[].path]'
gh pr view {number} --json commits --jq '.commits[-1].oid'
```

## Step 3: Ensure output directory exists and gather evidence

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
mkdir -p "$AW_DIR/reviews"
ls -1dt "$AW_DIR/verification/"*/ 2>/dev/null | head -1   # newest pack.json dir
ls -1dt "$AW_DIR/design/verify/"*/ 2>/dev/null | head -1  # newest comparison-report.json dir
```

Read `pack.json` and `comparison-report.json` from those newest dirs (per `_shared/evidence-pack.md`). Their contents become `{evidence}` for reviewers — the literal `none` when neither exists. A diff that changes a desktop UI is only covered by a pack with `platform: "desktop"`; a web or iOS pack does not count for it.

## Step 4: Triage

**Spawn a subagent** (general-purpose) with the triage prompt from [triage-prompt.md](triage-prompt.md).

Inject:
- `{title}`, `{body}` — PR metadata
- `{file_list}` — JSON array of changed file paths
- `{diff}` — full diff output

**Formal check lens.** When the diff touches locks, leases, retries, queues or workers, or a state machine (locks, leases, retries with idempotency, queues and workers, multi-step writes across actors, failover or leader election, ordering between processes), also assign a reviewer with focus "concurrency: **Invoke skill `formal-verify`** on the changed mechanism; report a `violation` as a blocking finding with the trace in real steps and its `report.md` path; list a `pass` with its bounds; name `blocked` as not checked". The bad case: two workers both claim a job because the check and the set are separate statements. Otherwise no assignment, and the review summary says in one line: "Formal check: skipped (no state machine, protocol or concurrency in this diff)".

Parse the returned JSON array of reviewer assignments. If the user passed `--lens <persona,...>`, append one assignment per named lens/persona (dedupe by agent name) — these run in addition to triage output.

## Step 5: Spawn Parallel Reviewers

**Dispatch in parallel** — all reviewers at once, per `_shared/parallel-dispatch.md`. Each reviewer receives the prompt from [reviewer-prompt.md](reviewer-prompt.md) with these values injected:
- `{agent}`, `{focus}`, `{number}`, `{title}`, `{diff}`, `{evidence}` (from Step 3)

Each reviewer returns a **JSON object** as its final output (not GitHub comments). Collect all responses.

### Sub-skill Dispatch

After collecting all reviewer JSON outputs:
1. Check for any reviewer output with `investigation_needed: true`
2. If found: rank candidates deterministically — severity first (`blocking` before `issue`), then longer error trace, then lowest issue `id` (lexicographic) — and take up to **3**
3. **Ask the user:**
   > "Found {N} blocking bug(s) with error traces. Run rootCause on the top {min(N,3)}? (yes/no)"
4. If yes: **Dispatch in parallel** up to 3 rootCause runs — one **Invoke skill `rootCause`** (args: `"<investigation_error value>"`) per candidate, launched together
   - Attach each investigation file path to its issue in the state file under `"investigation"`
   - If a rootCause returns `scope-breach`, note it in the state file and continue — do not block the review
5. If no, or no reviewer flagged `investigation_needed`: skip and proceed to writing the state file

## Step 6: Write State File

Combine all reviewer outputs into `$AW_DIR/reviews/{number}.json`:

```json
{
  "pr": {
    "number": 123,
    "title": "Add auth middleware",
    "branch": "feature/auth",
    "owner": "myorg",
    "repo": "myrepo",
    "url": "https://github.com/myorg/myrepo/pull/123"
  },
  "commit_sha": "<latest commit oid>",
  "review_run": 1,
  "previous_run_finding_count": null,
  "lenses": ["security-sentinel", "kieran-typescript-reviewer"],
  "reviewed_at": "<ISO timestamp>",
  "posted": false,
  "posted_at": null,
  "reviewers": [
    {
      "agent": "security-sentinel",
      "focus": "auth, input validation",
      "summary": "Found 2 blocking issues...",
      "issues": [
        {
          "id": "sec-0",
          "severity": "blocking",
          "path": "src/auth.ts",
          "diff_position": 42,
          "summary": "JWT not verified before use",
          "body": "**[blocking] JWT not verified before use**\n\nFull comment text...",
          "type": "inline",
          "addressed": false,
          "posted_comment_id": null
        }
      ]
    }
  ]
}
```

Save this file (write it in full). `lenses` lists the agent names actually spawned in Step 5. On re-review (file already exists): set `review_run` to prior value + 1 and `previous_run_finding_count` to the prior run's total issue count before overwriting.

## Step 7: Report to User

```
Review complete for PR #{number}: "{title}"
Findings saved to $AW_DIR/reviews/{number}.json
Run #{review_run} (previous run findings: {previous_run_finding_count})
Lenses: {lenses, comma-joined}

Reviewers:
  • security-sentinel (focus: auth, input validation) — 2 blocking, 1 issue
  • kieran-typescript-reviewer (focus: type safety) — 0 issues

Run /postReview to publish comments to GitHub.
Run /addressReview to start implementing fixes.
```

## Next steps

- `/postReview` — publish the findings to GitHub as batched comments
- `/addressReview` — start fixing the issues in parallel
- `/rootCause` — investigate a specific blocking bug from the review

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f" 2>/dev/null; echo "AW_DIR=${AW_DIR:-}"; echo "AW_RESULT_SKILL=${AW_RESULT_SKILL:-}"; echo "AW_RESULT_FILE=${AW_RESULT_FILE:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write the result file with the Write tool, and every other file the run writes, first; then open your final reply (the message after the last tool call) with the same JSON as a fenced `aw-result` block, before any prose and before `## Next steps`. The file path is `$AW_DIR/results/review-<timestamp>.json` (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front), except when the gate printed `AW_RESULT_SKILL=review`: then the path is the `AW_RESULT_FILE` it printed, and the file carries the same keys as the block below, nothing dropped. In that mode a question for the person goes in `"waiting": "<the question, one line>"` (a non-empty string, never `null` or `""`), with the rest of the result as it stands; print it, end the turn, and never call AskUserQuestion. See [Under Sindri](../_shared/output-contract.md#under-sindri-aw_result_skill).

Keys: `skill` (`review`), `artifacts` (absolute paths: the review state file `$AW_DIR/reviews/<pr>.json`), `summary` (one line),
plus `pr` (PR number); `findings` (count of findings kept after triage); `blocking` (count of blocking findings); `verdict` (`PASS` when `blocking` is 0, else `NEEDS_CHANGES`); `review_file` (absolute path of `$AW_DIR/reviews/<pr>.json`, the same file as in `artifacts`).

```aw-result
{
  "skill": "review",
  "pr": 412,
  "findings": 5,
  "blocking": 1,
  "verdict": "NEEDS_CHANGES",
  "review_file": "/home/u/.agentic-workflow/acme-api/reviews/412.json",
  "artifacts": ["/home/u/.agentic-workflow/acme-api/reviews/412.json"],
  "summary": "5 findings, 1 blocking: JWT audience never checked."
}
```

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
