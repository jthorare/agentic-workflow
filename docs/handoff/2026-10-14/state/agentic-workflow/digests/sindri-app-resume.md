# Sindri app — resume after the usage reset (Oct 16, 5 am PT)

Orchestrator handoff. Read `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-app/stages.md` first (contracts,
lanes, the team rules, each lane's log), then this.

## State on 2026-10-11 (end of the orchestrator session)

| Stage | PR | State | Base |
|---|---|---|---|
| S1 app shell | #163 | **ready for review**, `2a31206a`, waiting for the owner's merge | origin/HEAD |
| S3 context retrieval | #166 | **ready for review** (lane D finished; run 4 had 0 blocking and 0 issue) | `feat/sindri-s1-app-shell` (S1 merged in up to `ae8b6da3`) |
| S1b UI shell | #168 | stopped by the usage limit during review run 4 of 5 (runs 1–3 had 0 blocking) | S1 |
| S2 workflow engine | #167 | stopped mid round-1 fixes: the contract changes are pushed (`b29c2672`); the rest is in `digests/sindri-s2-engine-handoff-2.md` | S1 |
| S4a judge+evidence blocks | (none yet) | stopped while recording run 3 and starting run 4 and gate 5 | S2 |

## To resume

1. **S1:** if the owner merged #163, retarget the stacked PRs (#166, #167, #168) to the default branch (`gh pr edit
   <n> --base <default>`), and have each lane merge origin into its branch.
2. **S3 (#166):** it was built against S1 at `ae8b6da3`. Merge S1's final head (`2a31206a`), re-run its gate, and
   check that nothing regressed. The owner question is open: keep S3 in `src/Sindri` and extract `Aw.Context` once spec
   PR #162 is approved (lane D's recommendation).
3. **S1b (lane B):** dispatch from #168's state. Merge S1 `2a31206a` (approval rule changes: status now errors (exit 2)
   on an unreadable ledger or approval; `NeedsApproval`; new codes SND-API-004/005, SND-CLI-003, SND-PROFILE-006).
   Finish review run 4 (and 5 if needed), redo the hand test in `scratchpad/s1b-laneB/`, then ready. The screenshot is a
   headless Skia render unless the owner grants Screen Recording.
4. **S2 (lane C):** dispatch from `digests/sindri-s2-engine-handoff-2.md`. Merge S1 `2a31206a` (SindriErrors.All will
   conflict again; keep both sides). Post run 1, finish the blocking fixes and issues, then runs 2+ (cap 5).
5. **S4a (lane E):** its worktree is `.claude/worktrees/sindri-s4-blocks`. Re-dispatch with its lane brief and tell it
   to merge S2 `b29c2672`+ and run review runs 4–5.
6. Every lane: its own scratchpad subfolder, the heavy wrapper, review cap 5, never merge.

## Known flakes (re-run alone, record them)
The Redaction unit-lane budget under load (5.6 s quiet, 10–15 s loaded). Evidence's CLI lock-signal tests time out
(exit 75) when every heavy slot is taken. `statusline.test.sh` fails under the heavy wrapper and passes run directly.
`agent-token` fails under load.
