---
name: review
description: Orchestrate a multi-agent PR code review. Spawns domain-specific reviewer subagents in parallel based on changed files. Findings are saved to $AW_DIR/reviews/<pr>.json — run /postReview to publish to GitHub.
argument-hint: [pr-number-or-url] [--lens <persona,...>]
allowed-tools: Bash(gh *), Bash(git *), Bash(ls *), Bash(mkdir *), Bash(SHARED_DIR=*), Agent, Read, Write, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

# PR Review Orchestrator

Runs parallel domain-specific reviewers and saves findings locally. Does **not** post to GitHub — run `/postReview` when ready.

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
