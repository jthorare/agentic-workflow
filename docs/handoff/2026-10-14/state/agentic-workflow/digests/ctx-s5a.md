# Handoff: S-5A (Sindri context: Onyx adapter, secret pointers, doctor context:onyx)

**Goal:** stage S-5A of the Sindri context lane, run with /specToProvenPR steps 1–9 under the lead's brief.
Spec: `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` on origin/main (Phase 5; the "Parallel
delivery" brief). Codes SND-CONTEXT-050–069 (shared with S-5B). Port 7498, home `<scratch>/home-s5a`.

**Done (Phase A prep):** everything is in `~/.agentic-workflow/jthorare-agentic-workflow/proof/ctx-s5a/`.
- `onyx-api-research.md`: Onyx CE pinned at v4.9.0 (commit f1738ed5). The fake's `/api/query/document-search`
  doesn't exist in it.
- `plan.md`: the tasks, plus the lead's build order at the end.
- `verification-plan.md`: created 2026-10-12T15:27Z, before any code.
- `onyx.md.draft` and `owner-check.md`.

**Waiting on:**
1. The owner's decision on route A (`/api/admin/search`, recommended) or B (`/api/search`), and on the three spec
   edits. The lead relays them; S-5A never edits the spec.
2. The lead's S-1A Core-contract sha and worktree `.claude/worktrees/ctx-s5a` (branch `feat/ctx-s5a`, draft PR with
   `--base feat/ctx-s1a`).

**Next (route-independent, in order):** secret pointers in Host `Composition`; the fake fixes that hold under both
options; the doctor `context:onyx` shell; the setup-doc sections that don't depend on the route. Hold the search,
keys and aliases code and all evidence until the owner answers.

**Tokens used:** about 0.21M of 3M.

## Update 2026-10-12 (later)
- The owner chose route A (`POST /api/admin/search`) with all three mitigations: an allowlist of the adapter's
  requests (enforced and tested), doctor shows the key's role, and the key has its own service account. Doctor can
  read the role from `GET /api/me` (`account_type`, `effective_permissions` contains "admin";
  `backend/onyx/server/manage/users.py:1081`, `manage/models.py:165`).
- Approved spec edits, made in their own commit: §5.1 alias source; Onyx `Versions` returns Unavailable; §12 both
  items closed; the §8.8 deviation (admin key) with its reason and mitigations; the pin Onyx CE v4.9.0 f1738ed5. Run
  the denylist check over the spec diff.
- Builds run as `AW_HEAVY_SLOTS=3 ~/.agentic-workflow/bin-r1b/heavy …`. S-5A ships as one PR even at size L.
- Done in the worktree: secret pointers (4aea28c0, 3c247798, abeedb2b). A second implementer is running tasks 2 and
  3 (the fake's v4.9.0 fixes for auth, chunk-info, version and metadata; the Core `OnyxSettings` rules and
  SND-CONTEXT-055; the version probe).
- Next: an implementer for the fake's admin/search and document-size-info routes, `OnyxSource` with keys and
  aliases, the allowlist, the doctor role check and the contract tests; then the spec edit, `docs/sindri/onyx.md`,
  the hand test and evidence, the draft PR, the review loop and step 9.

## Update: draft PR #195 open (base feat/ctx-s1a), pushed at 1545aa8b
- The Onyx adapter is done (466621b3..5a11cbc0). Spec commits: 4a06a8c1 (Onyx decision) and 1545aa8b (brief item 7,
  parked stages). origin/main 60ef0cbe is merged.
- An implementer is merging S-1A 1f0fde7c, then 8098ea1e (`ContextRepoName`, the new contracts, FixedSourceCatalog's
  empty scope), and wiring Onyx into Sindri: the profile, Composition's ContextEnvironment, the catalog, doctor
  `context:onyx`, the sources status row, and the S-5A sections of context.md and sindri-context.md.
- After that: commit docs/sindri/onyx.md from proof/ctx-s5a/onyx.md.draft. Check what the implementer's code 052
  means. Build $SINDRI with src/Sindri/aot-smoke/run.sh. Hand test on port 7498 in home-s5a. Write evidence.md and
  manual-test.md. Run the merge gate, first `npm ci --prefix judge`, then `npm ci --prefix scorer`. Then the review
  loop, step 9 and the denylist check, and report "ready #195".

## Update: the hand test FAILED (5 bugs); fixes are running
- Evidence (FAIL) and the merge-gate transcript are in proof/ctx-s5a: evidence.md, manual-test.md, gates-transcript.txt
  and hand-run/.
- An implementer is merging de15b379 (CheckSettings(section, repos), SourceRecord.Blocked, ContextEngineContract)
  and fixing: aot-smoke expected.txt; aliases reaching citations; the docs on version drift (doctor reports it,
  queries continue); doctor resolving the token pointer through ISecretPointers for the exact SND-SECRET reason;
  timestamps in text output; refusing a symlinked file: pointer.
- After that: rerun the hand test (all journeys plus the alias check), rewrite evidence.md as PASS, push, and update
  the PR body. Then the review loop (findings and replies go in evidence.md's "Review replies" section, never as PR
  comments, owner 2026-10-12), step 9, and report "ready #195".
- Open question to the lead: whether to keep the rule that blocks session reads of intent sources until
  context.repoTeams exists.

## Update: run-2 fixes pushed (head 6a4c262f); hand test run 3 underway
- Hand test run 2 failed on a permalink scrub leak and a missing 051 cause in the degraded message. Both are fixed in
  19e8f7c6..567e37ba. Also merged: S-1A 07ab478c and origin/feat/ctx-s1a 2cc2d8d7 (the X61 seam). The doctor checks
  moved to ServiceChecks. The Onyx contract tests derive from ContextSourceContract; a ChecksVersions virtual was added
  to S-1A's base, which S-1A should pick up. The spec sentence on the X61 seam is aae660c3.
- Run 3 rewrites evidence.md and manual-test.md and appends a "Rerun on 6a4c262f" section to gates-transcript.txt.
- owner-check step c now uses jq `[.cites[] | .aliases[]?] | length`.
- After that: the review loop (`/review` on #195, findings and replies in evidence.md, no PR comments), step 9,
  then mark the PR ready and run the lead's ignored-file cleanup in the worktree.

## Update 2026-10-14: review loop run 1
- origin/main d4de828b (S-3a) is merged (30ce941e, fix e878c509) and pushed; #195 targets main and was MERGEABLE.
- /review run 1: 0 blocking, 10 issue, 11 suggestion, 7 nit, in reviews/195.json. The replies to the non-fixed
  findings are already in evidence.md's "Review replies" section.
- An implementer is fixing the issues: the red commit 403f5dfe is in; the rest is uncommitted in the worktree.
- After that: review run 2 (the same review subagent pattern, run 2), the PR body summary of the review replies, step
  9 (the journey re-check if non-test source changed, the gates, the denylist over the title, body, commits,
  evidence.md, owner-check.md and onyx.md), `gh pr ready 195`, the ignored-file cleanup in the worktree, and report
  "ready #195". #195 merges after #207 (S-2W).
- Open with the lead: the repoTeams session rule; S-1A taking in ChecksVersions; the owner's spec decision on the
  §8.8 and §12 wording (first indexing time, search-only key).

## Update: main moves; denylist usage changed
- The merge implementer has started merging main: first 3e3fde3b (S-2W), now origin/main 6d8ab66c (S-2W, S-1M #204,
  #208). It must prove the extra-pattern scrub covers Onyx fields and the MCP tools work with Onyx; then push and
  check the PR is MERGEABLE.
- Review run 2: 4 issues (the key read happens before the deadline; the walkthrough's profile-file step; hidden-doc
  docs) plus cr-2, cr-4 and qa-0 folded in. The replies are already in evidence.md. Fix them after the merge, then
  review run 3.
- The denylist check now runs only through the AW_DENYLIST environment variable, with no list argument; never write
  the list's path into any file.
