# Handoff: PR #192 review loop (Sindri engine P4)

Brief: ~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-engine/unit-brief.md (lock rules: build once with
`AW_HEAVY_SLOTS=2 ~/.agentic-workflow/bin-r1b/heavy dotnet build ...`, then `dotnet test ... --no-build`; one fix agent at a
time; no `timeout` binary, bound commands with `perl -e 'alarm shift; exec @ARGV' <secs> ...`; never delete lock dirs).
Worktree ~/repos/agentic-workflow/.claude/worktrees/sindri-wf-p4, branch feat/sindri-wf-p4-interpreter.
Pushed head 3ed339b5 (main fe228345 merged; carries API P2 #190 at **ba457fdc**). Gate at 3ed339b5: build 0 warnings, format,
smoke, deps/rules/sync/env ok; lanes 5659/5660, the one failure is cross-lane (UI GoldenContractTests vs #190's status
golden views/health/lastRun; reported to coordinator, not P4's). Merge order: #192 merges after #190; then merge main
so the diff is P4's own (no new review run needed for a no-behaviour merge).

## Run table
| Run | Head | blocking | issue | suggestion | nit |
| 1 | c3ea98c7 | 3 | 17 | 10 | 7 |  (all owner P4; none on P2 / API P2)

State file ~/.agentic-workflow/jthorare-agentic-workflow/reviews/192.json holds every run-1 finding + resolution
(entries still marked "pending: interp group" are now FIXED: 1c9ad45c/466bfede/702f2700/... red, 505e52a7 fix; update them).

## Done since run 1 (on the branch)
- bridge dig-0/1/2/4/5: 93eda8ce red, e78797d3 fix
- cli cr-0/3/4/5/6/10/11: 1fad519b red, 96d4c95b, ee36c01c (sec-3 declined)
- nonce dig-3, sec-0: d4689968 red (bounded), 0f004f3c, 4c28de71
- interp arch-0..6, qa-0, dig-6: cherry-picked reds + 505e52a7 fix
- E-30 temporal_event_id, ledger migration 0005 (coordinator-assigned): e85d5836 red, 00a5c713 fix
- ScheduledResult (API E1): merge 41aa950f adapts signature; red + fix (bridge Quiet when it signalled nothing)
- docs: 03a5f7f1, bb3b171b; registry CLI test fakes Temporal 7450463c/fecdf01f; spec §4.2 item 1 cfd1f95d

## Left
1. (done) pushed and reported. 192.json interp entries now marked fixed.
2. qa-1 (issue): run evidence journeys via scripts/sindri-prove.sh (fresh AW_SINDRI_HOME=$TMPDIR/sindri-dev-p4,
   AW_SINDRI_PORT=0): answer (text + protected grant with typed challenge), command, cancel, events --follow (Ctrl-C exit 0),
   status/events on bare item, desk-timeout park (dev profile deskTimeout 1m), causation routing, E-30 signal 6 sqlite
   count check; update ~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-wf-p4/{evidence.md,manual-test.md,raw/}.
3. cr-8 (issue): refresh PR body (gh pr edit 192 --body-file; it embeds evidence.md) with head, carried merges, gate, and a
   "Review resolutions" section (from 192.json).
4. Owner items: spec §5.4 held-window wording (1-min lag, started-before rule); arch-8 §3.4/§3.8 rev 8.2 wording.
5. Review run 2 (reviewers model opus, in parallel; P4-only diff = HEAD vs merge-tree of origin/main + origin/feat/sindri-api-p2-projector;
   brief scratch/p4-review/reviewer-brief.md), fix (Sonnet, one at a time, scratch/p4-review/fix-brief.md), repeat to 0/0, cap 5.
6. Known flake: SpikeEvalTests "under ten seconds" at heavy load.

## Lead 2 (2026-10-14 ~18:40)
- Run 2 started on 3ed339b5: 5 reviewers (qa-expert, data-integrity-guardian, security-sentinel, architecture-strategist,
  code-reviewer), brief scratch/p4-review/run2/reviewer-brief.md; P4-only diff = HEAD vs merge-tree(fe228345, ba457fdc)
  = tree 81b518da (api-surface spec conflicts between main and #190; not P4's, excluded). since-run1.diff there too.
- qa-1 journeys running in a Sonnet agent in the PR worktree (fresh $TMPDIR/sindri-dev-p4).
- Note: #190 head on GitHub is now 189f2f2b (P4 carries ba457fdc); merge only when the orchestrator asks.
- Owner spec note committed+pushed 9dcab704 (§5.4 held events at-or-before + 1 min of first delivery; §3.4/§3.8 desk timeout
  always parks). Also aligned the §5.4 eventHold buffering sentence with code (non-fitting only). Mention both in PR body.
- Run 2 counts (deduped, classified): blocking 2 (arch-r2-1 parked report hang; sec-r2-1+dig-r2-4 Pending skips checks),
  issue 7, suggestion 9, nit 2. Findings in scratch/p4-review/run2/out/*.json. Fix group A (wf) running (Sonnet, worktree
  branch p4fix2-wf). Group B next: ApplyCommand Pending checks + sindri-workflow.md ~635 sentence; SchedulerHost _last
  ConcurrentDictionary; --follow EPIPE; temporal_event_id docs row 141/144 (spec §5.6/API P13 text = owner question).
  qa-r2-5 goes into the PR body refresh.
