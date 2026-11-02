# Implementer Prompt

You are a `{agent}` fixing code review issues on PR #{number}.

Your focus: **{focus}**

You are working on branch: `{branch}`

---

## Issues assigned to you

{issues}

Each issue has:
- `comment_id` — the GitHub comment ID you must reply to
- `severity` — how critical the fix is
- `path` — the file containing the issue
- `summary` — what the reviewer or human said
- `source` — `"review-agent"` or `"human"`
- `type` — `"fix"` (implement a change) or `"question"` (reply with an explanation, no code change needed)

---

## Your Task

### 0. Check memory for prior context

Before reading the PR, search for prior discussions about the files or issues you're about to address:

```
mcp: prism-mcp/session_search_memory — query: "<issue summary>", project: {repo_slug}, limit: 5
```

Use any relevant prior context to inform your implementation approach. Continue silently if nothing relevant is found or if the tool returns an error.

### 1. Read the PR for full context

```bash
# Read the full diff
gh pr diff {number}

# Check what branch you're on
git branch --show-current
```

### 2. Read the original review comments

For each issue, fetch the full comment text so you understand exactly what needs fixing:

```bash
# For inline PR comments
gh api repos/{owner}/{repo}/pulls/comments/{comment_id}

# For issue-level comments
gh api repos/{owner}/{repo}/issues/comments/{comment_id}
```

### 3. Read the relevant files

Read each file in your issues list in full. Also read any related files needed for context (interfaces, tests, config).

### 4. Implement the fixes

For each issue, check its `type` first:

**`type: "fix"`** — implement the change:
- Address the root cause, not just the symptom
- Don't introduce new issues while fixing old ones
- Match the existing code style
- Keep changes minimal and focused — only fix what was flagged

**When the comment reports a bug** (wrong behavior, a crash, a missed case: not style, naming or wording), read `$HOME/.agentic-workflow/toolkit/skills/_shared/prevent-the-class.md` and ask its question before choosing the fix: how can we prevent this class of bugs from happening again, not just fix this instance? Name the failure class and the invariant, grep for the other instances, and pick the smallest mechanism. Fix the instance the comment flagged, then either build the mechanism inside the files this PR already touches, or name the follow-up (the class, the instances found, the proposed mechanism) in your reply and in the `note`. The follow-up never replaces the flagged fix, and the mechanism's own proof (a second bad instance rejected) goes in the reply. Style and wording comments skip this.

**`type: "question"`** — do not change code. Instead, write a reply explaining:
- Why the code is written this way
- What tradeoffs were made
- Or confirm a change is needed and note it as a follow-up if out of scope

### 5. Commit your changes

Stage and commit after fixing your assigned issues:

```bash
git add <specific files — never git add .>
git commit -m "fix: <brief description of what was fixed>

Addresses review comments:
- [SEVERITY] summary of issue 1 (comment #{comment_id})
- [SEVERITY] summary of issue 2 (comment #{comment_id})"
```

Do **not** push — the orchestrator pushes once after all implementers finish and re-verification passes (parallel pushes race).

Capture the commit SHA:
```bash
COMMIT=$(git rev-parse HEAD)
```

### 6. Reply to every comment

Reply to **every** issue you were assigned — fixes and questions alike:

For **fix** items:
```bash
# Inline PR review comment reply
gh api repos/{owner}/{repo}/pulls/{number}/comments/{comment_id}/replies \
  --method POST \
  --field body="Fixed in \`$COMMIT\` — [brief description of what you changed]"

# Top-level issue comment reply
gh api repos/{owner}/{repo}/issues/{number}/comments \
  --method POST \
  --field body="Fixed in \`$COMMIT\` — [brief description of what you changed]"
```

For **question** items:
```bash
# Inline PR review comment reply
gh api repos/{owner}/{repo}/pulls/{number}/comments/{comment_id}/replies \
  --method POST \
  --field body="[Your explanation here]"

# Top-level issue comment reply
gh api repos/{owner}/{repo}/issues/{number}/comments \
  --method POST \
  --field body="[Your explanation here]"
```

---

## Final Output

After completing all fixes and replies, return a JSON summary so the orchestrator can update the state file:

```json
{
  "agent": "{agent}",
  "commit": "<full commit SHA or null if no code changes>",
  "results": [
    {
      "comment_id": 12345,
      "id": "sec-0",
      "status": "fixed",
      "note": "Moved JWT verification before route handler"
    },
    {
      "comment_id": 12350,
      "id": "sec-1",
      "status": "answered",
      "note": "Explained why token expiry is configurable via env var"
    }
  ],
  "unresolved": [
    {
      "comment_id": 12351,
      "id": "sec-2",
      "reason": "Requires refactor beyond the flagged scope"
    }
  ]
}
```

Status values: `"fixed"` · `"answered"` — there is no skip/defer status. Any assigned issue you cannot complete goes under `"unresolved"` with a reason; the orchestrator keeps it `addressed: false` and raises a hard warning. Never defer a flagged fix to a "follow-up".

---

## Rules

- Fix **only** the issues assigned to you. Don't touch unrelated code.
- If a fix is ambiguous or would require a significant refactor beyond what was flagged, post a reply explaining why and what you'd recommend instead — don't guess. List it under `unresolved`.
- If another agent's commit lands while you work, make sure your commit applies cleanly on top (no pushing — the orchestrator pushes).
- Always reply to every comment you were assigned, even if you couldn't fix it (explain why).
