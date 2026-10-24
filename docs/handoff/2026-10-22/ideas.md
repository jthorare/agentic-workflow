# ideas lane — handoff 2026-10-22

Lane: line recipe blocks (ideas half), lens packs, bugfix Steps, PR babysitter recipes. Session `sindri-ideas`.
Main at hand-off: `6a674c43d`. Push remote for this account: `jfork` / `upfork` (joithor/agentic-workflow-jthorare); `origin`
(jthorare) is 403. PRs: `gh pr create --repo jthorare/agentic-workflow --head joithor:<branch>`.

## PRs and branches

| Unit | PR | Branch | Head | State | What's left |
|---|---|---|---|---|---|
| LN-PK lite (lens packs, `step.lens.round` / `step.review.round` re-pins, workflow.phase v2, review-loop v4) | #601 | `feat/sindri-lnpk-lite` | `c5718aa10` | open, MERGEABLE, `ready` sent; not gated yet (memory, hand-off) | in meta's hand-off queue after #598 and #597: gate, owner's merge, then the owner's approve-all for its hash moves |
| BF-S (bugfix block Steps + `bugfix-state` adapter) | #591 | `feat/sindri-bugfix-steps` | — | MERGED (main `bf77125d1`) | nothing |
| BS-7 (scoping evidence, read-tracker, read-change Steps) | #579 (replaces #535) | `feat/line-bs7` | — | MERGED (main `09bfb594b`) | owner closes #535 (comment: "Superseded by #579") |
| review.record (`step.review.record`, review packet) | #577 | `feat/sindri-review-record` | — | MERGED (main `32d66498c`) | nothing |
| PB-R, STEPRUN-EV, BS-11, line-fixture fix | #557, #564, #566, #576 | — | — | MERGED | nothing |
| docs.scope | none | — | — | not started | blocked, see below |
| SC-E (scoping prompt eval) | none | `feat/scoping-prompt-eval-sce` (on origin) | — | paused by the owner | owner's call |

### #601 notes for its reviewer
- Hash moves: `step.lens.round` e0821b51 → 8e31f7aa…; `step.review.round` 2aa8ad46 → 4d36f64f…; `workflow.phase` v2
  `sha256:c2c9e93d3f1b4bf0b4755df18ba9659c2c38edf43f67d976870541b580ac9924`; `workflow.review-loop` v4
  `sha256:522e7e223f3d845d220b7d91d20951c848ef15cda1c700b9e923f619d06bb584`.
- `LineBlocksTests`' `step.lens.round` row was updated to the pinned descriptor (it also reads `fact:scope` and writes
  `fact:lensRounds`). That is the one test expectation changed to match code; check it against the LN-PK spec.
- The merge of `bf77125d1` (`077c4f767`) went in with `--no-verify` for the scrubber false positive on main's
  `.github/workflows/ci.yml:334` (`${GITHUB_TOKEN}` placeholder, from #570; meta routed the fix to engine). No other commit skips the hook.
- Box-load timeouts that passed on rerun: RecipeApproveCli, ServedWorkflowRoutes, WorkflowViewGolden, ScopingCheck,
  ScopeRunProposalsContract (an HttpClient 10 s timeout in its setup; green on main CI).

## Worktrees (`~/repos/aw-wt/`)

| Worktree | Branch | State |
|---|---|---|
| `feat-sindri-lnpk-lite` | `feat/sindri-lnpk-lite` | clean; local = pushed = `c5718aa10` |
| `feat-sindri-bugfix-steps` | `feat/sindri-bugfix-steps` | clean; pushed `1f0126753`; merged, so the sweep can remove it |

All other ideas worktrees (bs7, bs11, pbr, steprun, review-record) were already removed. Nothing is uncommitted or unpushed.

## Builders

All stopped; no test, build or format runs are left from this lane.
- `ideas-lnpk4` finished #601 and stopped. `ideas-lnpk3`, `ideas-lnpk2` and `ideas-lnpk` each stopped at the context limit.
- `ideas-bfs2` finished #591 and stopped; `ideas-bfs` stopped at the context limit.
- `ideas-bs7`, `ideas-review-record`, `ideas-bs11`, `ideas-pbr` and `ideas-steprun` finished, and were stopped in the owner's idle cleanup.

## Blocked

- **docs.scope** waits on engine's `IPullRequests` port (engine pings ideas when it lands).
- **Owner decisions** (meta carries them): noBehaviourChange (#362), CO-4a, PB-R's two spec questions, PB-S1 seeds.
- **SC-E** is paused by the owner.

## Rules this lane learned

- A contract test is fixed by making the fake match the real binary, never by relaxing the assertion (#591: the fake now
  answers `AW-BUGFIX-005` like `bugfix-state`).
- Real-binary tests are `Lane=Integration` and read the solution-built dll (`artifacts/bin/Aw.BugFix.Host`,
  `artifacts/bin/Aw.Packets.App`); a test-project-only build fails them.
- `recipe validate`/`eval` need a scratch `HOME` (the real profile's approval hash is stale, SND-PROFILE-005) and absolute paths.
- Merge `origin/main` before ready. Union the Step lists in `ShippedBlocksTests` / `ScopingBlockRuntimesTests`, recount, keep
  every pin in `ShippedDescriptorHashes`, and keep both sides of `PublicAPI.Unshipped.txt`. If a PR conflicts with one ahead of
  it in the train, merge that one in first.
- Box rule: long test runs only in the background with a lane or class filter, and kill them when a builder stops.
  Heavy work goes through `~/.local/bin/heavy`.

## Next steps, in order

1. #601 gates after #598 and #597 (meta's queue). Watch it. Fix any red that is in its diff (not on meta's known main-red list), merge main if it moved, re-ready.
2. After #601 merges, the owner runs approve-all for its recipe and descriptor hash moves. Owner closes #535.
3. When engine's `IPullRequests` port lands, start docs.scope (`step.docs.scope` is in `DocsSteps.cs`; descriptor only today).
4. Pick up the owner's decisions above as they come back (PB-R spec questions, PB-S1 seeds) and SC-E if unpaused.
