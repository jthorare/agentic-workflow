---
name: syncDocs
description: Post-ship documentation updater — refreshes README, ARCHITECTURE.md, CHANGELOG, and AGENTS.md (the repo agent-instructions file) to reflect recent changes.
argument-hint: "[release-id] [--scope readme,architecture,changelog,agents] [--since <ref>] [--commit]"
allowed-tools: Bash(git *), Bash(gh *), Bash(ls *), Bash(grep *), Bash(mkdir *), Bash(SHARED_DIR=*), Bash(source *), Agent, Read, Write, Edit, Glob, Grep, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

# Sync Documentation

Refreshes project documentation to reflect recent changes. Updates only the sections that are stale — does **not** rewrite entire files.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

## Step 1: Parse Arguments

- **Release-id** (positional, optional) — canonical CD7 form `<ISO-date>-<short-sha>`. Passed by `/canary` on HEALTHY. The report in Step 5 is written **inside** `releases/<release-id>/` so the whole release stays in one folder.
- `--since <ref>` (optional) — the git ref the change range starts from. `/canary` passes the release's merge SHA so the docs sync covers exactly the shipped release, not an unrelated tag range.
- `--commit` (optional) — commit doc changes directly to the current branch. **Default (flag absent): branch + PR** (this toolkit ships a block-push-main hook; committing to main post-merge would be blocked anyway).
- `--scope` — accepted values (comma-separated, case-insensitive):

| Value | Target file |
|-------|-------------|
| `readme` | `README.md` |
| `architecture` | first file matching the glob `{,planning/,docs/}ARCHITECTURE.md` |
| `changelog` | `CHANGELOG.md` |
| `agents` (alias: `claude`) | `AGENTS.md` — the canonical repo-instructions file. If `AGENTS.md` is absent, fall back to `CLAUDE.md`; if `CLAUDE.md` is a symlink to `AGENTS.md` or only an `@AGENTS.md` import stub, edit `AGENTS.md` and never edit `CLAUDE.md` itself |

Default (no `--scope` provided): all four documents.

If a target file does not exist in the repo, skip it and note it in the report.

## Step 2: Gather Changes

Determine the range of recent changes:

```bash
# --since <ref> wins; else last tag; else last 20 commits
LAST_REF="{--since ref if provided}"
[ -n "$LAST_REF" ] || LAST_REF=$(git describe --tags --abbrev=0 2>/dev/null || echo "HEAD~20")

git log --oneline "$LAST_REF"..HEAD    # commit summaries
git diff "$LAST_REF"..HEAD --stat      # file-level change stats
git diff "$LAST_REF"..HEAD             # detailed diff for context
```

Store this change context — all agents in Step 3 will receive it.

## Step 3: Spawn Update Agents

For each document in scope, **Spawn a subagent** to update it. **Dispatch in parallel** — all applicable agents at once, per `_shared/parallel-dispatch.md`.

Each agent receives:
- The full contents of the target document (read it first)
- The commit log and diff stats from Step 2
- Specific instructions per document type (below)

**Common rules (append to every agent prompt):** make targeted in-place edits — do NOT rewrite the entire file; if nothing needs updating, return `NO_CHANGES` (subject to the Step 3.5 post-check).

### README Agent

> You are updating `README.md` to reflect recent code changes. You have the current file contents and a list of recent commits with their diffs.
>
> Rules:
> - Keep the existing document structure intact.
> - Update feature lists, badges, and setup instructions ONLY if the recent changes affect them.
> - Add new sections only if a major new feature was introduced.
> - Remove references to features/files that no longer exist.

### ARCHITECTURE Agent

> You are updating the resolved ARCHITECTURE.md (first match of `{,planning/,docs/}ARCHITECTURE.md`) to reflect recent code changes. You have the current file contents and a list of recent commits with their diffs.
>
> Rules:
> - Update the directory tree if files/directories were added, removed, or moved.
> - Update component descriptions if their responsibilities changed.
> - Update key rules or patterns if new ones were introduced or old ones changed.

### CHANGELOG Agent

> You are updating `CHANGELOG.md` to reflect recent changes. You have the current file contents and a list of recent commits.
>
> Rules:
> - Append a new entry at the top of the changelog (after the title/header).
> - Use today's date and the version from package.json, Cargo.toml, pyproject.toml, or similar if available. If no version file exists, use "Unreleased".
> - Categorize changes using Keep a Changelog format: Added, Changed, Fixed, Removed.
> - Derive entries from commit messages. Group related commits.
> - Do NOT modify existing entries.
> - If the changelog does not exist, create it with a standard header and the new entry.
> - `NO_CHANGES` is only valid when the range contains nothing meaningful (e.g., only CI/chore commits).

### AGENTS.md Agent

> You are updating `AGENTS.md` (or the resolved fallback from Step 1) to reflect recent code changes. You have the current file contents and a list of recent commits with their diffs.
>
> Rules:
> - Update the architecture tree if files/directories were added, removed, or moved.
> - Update the commands section if new scripts were added or existing ones changed.
> - Update patterns section if new patterns were introduced.
> - Update tech stack if dependencies changed significantly.

## Step 3.5: NO_CHANGES Post-Check

An agent may not return `NO_CHANGES` unconditionally — verify it. From the Step 2 diff stats, collect **new paths and new scripts** (added files/directories; new entries in `package.json` `scripts`, `Makefile` targets, etc.). For each doc whose agent returned `NO_CHANGES`, grep the target doc for those additions **where that doc type should mention them** (README: commands/features; ARCHITECTURE: directory tree; AGENTS.md: commands/structure; CHANGELOG: any non-chore commit). If a relevant addition is absent, the `NO_CHANGES` claim is a violation: **re-dispatch that agent once**, listing the specific missed paths/scripts. If it still returns `NO_CHANGES`, record the discrepancy in the report instead of silently accepting it.

## Step 4: Commit

After all agents complete, check if any files were actually modified:

```bash
git status --porcelain README.md CHANGELOG.md AGENTS.md CLAUDE.md {resolved ARCHITECTURE path}
```

If no docs were changed, note "No updates needed" and skip to Step 5.

If docs were changed — **default path (no `--commit`)**: create a branch and open a PR (post-merge you are on the default branch, and this toolkit's block-push-main hook would reject a direct push anyway):

```bash
git checkout -b docs-sync-{release-id or timestamp}
git add README.md CHANGELOG.md AGENTS.md CLAUDE.md {resolved ARCHITECTURE path} 2>/dev/null
git commit -m "docs: sync documentation with recent changes"
git push origin docs-sync-{release-id or timestamp}
gh pr create --title "docs: sync documentation" --body "Automated docs sync via /syncDocs for {release-id or ref range}."
```

**With `--commit`**: add + commit directly on the current branch (no push, no PR). Only add files that exist and were modified. Capture the commit SHA (or PR URL).

## Step 5: Report

Resolve the release dir and write the report **inside it** (one-folder-per-release invariant, CD7):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
RELEASE_DIR="$AW_DIR/releases/{release-id}"  # arg wins; else newest releases/<ISO-date>-<short-sha>/ dir; else mint from tip SHA
[ -n "{release-id}" ] || RELEASE_DIR=$(ls -1dt "$AW_DIR/releases/"*/ 2>/dev/null | head -1)
[ -n "$RELEASE_DIR" ] || RELEASE_DIR="$AW_DIR/releases/$(date +%F)-$(git rev-parse --short HEAD)"
mkdir -p "$RELEASE_DIR"
```

Write `$RELEASE_DIR/docs-sync.md`:

```markdown
# Documentation Sync

- **Date:** {ISO timestamp}
- **Release:** {release-id or "n/a (standalone)"}
- **Ref range:** {LAST_REF}..HEAD
- **Commit:** {SHA / PR URL / "no commit needed"}
- **NO_CHANGES post-check:** {clean / re-dispatched agents + outcome / recorded discrepancies}

## Documents Updated

| Document | Status | Changes |
|----------|--------|---------|
| README.md | {updated/skipped/not found} | {brief description or "—"} |
| {resolved ARCHITECTURE path} | {updated/skipped/not found} | {brief description or "—"} |
| CHANGELOG.md | {updated/created/skipped/not found} | {brief description or "—"} |
| {resolved AGENTS.md path} | {updated/skipped/not found} | {brief description or "—"} |
```

Print a summary to the user:

```
Docs synced.
  Updated: {list of updated docs}
  Skipped: {list of skipped/unchanged docs}
  Commit:  {SHA | PR URL | "no changes"}
  Report:  {RELEASE_DIR}/docs-sync.md
```

## Git mode

`AW_GIT_MODE` is `${AW_GIT_MODE:-full}`.

- `full`: do everything this skill describes.
- `readonly`: work in the current directory. Make no branch, worktree, commit, push, PR, PR comment or review, and change no remote ref. Do the file edits and local checks the skill describes, and print each git or gh command you would have run under a `Would run:` heading.

## Next steps

- `/weeklyRetro` — if end of week, run the retrospective

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/syncDocs-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) and print the same JSON as a fenced `aw-result` block before `## Next steps`.

Keys: `skill` (`syncDocs`), `artifacts` (absolute paths: the doc files it changed and `$AW_DIR/releases/<release-id>/docs-sync.md`), `summary` (one line),
plus `docs_changed` (list of doc paths changed).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
