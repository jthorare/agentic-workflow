---
name: postReview
description: Publish a completed /review to GitHub as batched PR comments. Reads from ~/.agentic-workflow/<repo-slug>/reviews/<pr>.json and posts one review per agent (minimizing API calls). Use after /review has written a local state file and you are ready to publish.
argument-hint: [pr-number]
allowed-tools: Bash(gh *), Bash(git *), Bash(ls *), Bash(SHARED_DIR=*), Read, Write, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

# Post Review to GitHub

Reads the local review state file and publishes all findings to GitHub in batched API calls — one review submission per agent.

## Step 1: Resolve the PR number

**If an argument was provided**, use it.

**If no argument**, detect from current branch:
```bash
gh pr list --head $(git branch --show-current) --json number,title,url
```

If multiple PRs found, **Ask the user** to pick one.

## Step 2: Load State File

Read `~/.agentic-workflow/<repo-slug>/reviews/{number}.json`.

If the file does not exist:
> "No local review found for PR #{number}. Run `/review` first."

If `posted: true`, **Ask the user** to confirm before continuing:
> "PR #{number} was already posted at {posted_at}. Post again anyway? (yes/no)"

## Step 3: Post Each Reviewer's Findings

For each entry in `reviewers`, post **one batched GitHub review** containing all that agent's inline comments plus a top-level summary body. This is a single API call per reviewer.

```bash
gh api repos/{owner}/{repo}/pulls/{number}/reviews --method POST --input - <<'EOF'
{
  "commit_id": "{commit_sha}",
  "body": "{review_body — JSON-escaped}",
  "event": "COMMENT",
  "comments": [
    { "path": "src/auth.ts", "position": 42, "body": "**[blocking] …**" }
  ]
}
EOF
```

`gh api --field "comments[]=…"` cannot serialize object arrays — always send the full JSON body via `--input -` heredoc. `{review_body}` is the reviewer's human-readable summary (format below); `comments` is built from all `type: "inline"` issues for this reviewer.

### Review body format

```markdown
## {agent} Review
**Focus:** {focus}

{summary}

### Findings

| Severity | File | Issue |
|----------|------|-------|
| blocking | `src/auth.ts` | JWT not verified before use |
| issue | `src/api.ts` | SQL injection risk |

{top_level_issue_bodies}

### Evidence
{evidence}

<!-- review-data
{
  "agent": "{agent}",
  "focus": "{focus}",
  "issues": [ ... full issues array from state file ... ]
}
-->
```

`{top_level_issue_bodies}` — append the full `body` text of any `type: "top-level"` issues directly into the review body.

`{evidence}` — the newest `proof/<stage-slug>/evidence.md` (CD6) when present; **omit the `### Evidence` section entirely when absent**. Locate it with:

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
ls -1t "$AW_DIR"/proof/*/evidence.md 2>/dev/null | head -1
```

### Inline comment format

Each `type: "inline"` issue maps to:
```json
{
  "path": "src/auth.ts",
  "position": 42,
  "body": "**[blocking] JWT not verified**\n\nFull comment text..."
}
```

### Capture posted comment IDs

The POST response contains only the review `id` — per-comment IDs are **not** in it. Capture `REVIEW_ID` (`… | jq '.id'`), then post-fetch the PR's review comments and match:

```bash
gh api repos/{owner}/{repo}/pulls/{number}/comments \
  --jq '[.[] | select(.pull_request_review_id == {REVIEW_ID}) | {id, path, position}]'
```

Backfill each issue's `posted_comment_id` by matching on `path` + `position` (the issue's `diff_position`). Issues left unmatched keep `null` — list them in Step 5.

## Step 4: Update State File

After all reviews are posted, update `~/.agentic-workflow/<repo-slug>/reviews/{number}.json`:
- Set `"posted": true`
- Set `"posted_at"` to current ISO timestamp
- Fill in `posted_comment_id` for each issue

Read the state file, apply all mutations in memory, then write the complete updated JSON back in one operation (partial in-place edits of JSON are fragile).

## Step 5: Report

```
Posted to PR #{number}: "{title}"

  • security-sentinel — 3 comments (2 inline, 1 top-level)
  • kieran-typescript-reviewer — 2 comments (2 inline)
  • performance-oracle — 1 comment (1 top-level)

Total: 6 comments · 2 API calls
Unmatched posted_comment_id: none

View: {pr_url}
```

## Next steps

- `/addressReview` — start implementing the fixes from the published review
- `/shipRelease` — if the review found nothing blocking, ship the release
