# Sindri app — stages

Spec: `docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` §17.29 (app), §17.28 (engine), §17.17 Q6/Q7;
seams §17.20–§17.22, §17.27.1; design spec §5.2, §9.1, §10.3, §11.1; readiness §4a/§4b.
Handoff: `~/.agentic-workflow/digests/sindri-app-shell-handoff.md`.

## Contracts

- **Autonomy:** run each stage to ready, never merge (owner, in the handoff). The human merges; the next stage starts after.
- **Git topology:** base `origin/HEAD` (the default branch), push remote `origin` (jthorare/agentic-workflow, not a fork).
  Never push the base branch. Each stage has its own branch and worktree under `.claude/worktrees/`.
- **Builds and tests:** under `~/.agentic-workflow/bin-r1b/heavy`. GREEN = the full local merge gate (Actions are
  billing-blocked). Lane-budget and statusline flakes under load: re-run alone, record here.
- **Review loop cap (owner, 2026-10-11, raised from 3): at most 5 `/review` runs per PR**, the first one included. If a
  blocking finding or an issue is still open after run 5, stop, report the open findings verbatim, and ask the owner.
  This applies to every lane. The loop still stops at the first run with 0 blocking and 0 issue.
- **Owner decisions:**
  - 2026-10-11 — profile format: **YAML, typed through AwJson** (a reflection-free YAML parser turns YAML into a JSON
    tree; AwJson deserializes it into typed records). The same reader is meant for S2 recipes and R14 packets.

## Stages

| # | Stage | Goal | Observable signal | Size | Status |
|---|---|---|---|---|---|
| S1 | App shell | `sindri` CLI + control-plane process: host lease with fencing epoch, ledger (`ILedgerStore`, envelope, idempotency, outbox), YAML profile approved by hash, command layer + loopback API + event stream, scheduler with a heartbeat service, `doctor` | `sindri` prints banner + summary; `sindri status` from a 2nd terminal; a 2nd `sindri serve` refused `SND-LEASE-001`; stop/kill → restart recovers with epoch+1; `doctor` green and red | L (~3.5k C# lines incl. tests) | **ready for review — PR #163** at `2a31206a` (human merge) |
| S1b | UI shell (lane B, parallel) | `Aw.Sindri.Ui` + `.Desktop` (Avalonia, `SindriUi` node in `deps.yaml`, `http` edge kind), launched by bare `sindri`, shows status from the API | bare `sindri` opens the window; it shows the same status the CLI does; process down → the window says so | M | planned (split out of S1: S1 alone is L) |
| S2 | Workflow engine (lane C, parallel, stacked) | §17.28: recipe schema (YAML via the S1 reader) + validator, block registry, instance rows, fenced idempotent transitions, timers; item lifecycle + Step contract as the first recipes | replayed event log reproduces the instance; stale epoch refused | L | planned |
| S3 | Context retrieval (lane D, parallel) | §17.29.2: source adapters + query planner over the reachable sources (code index, graphify, Linear, notes, ConnectAI via MCP) | `sindri context query` returns cited results | L | planned |
| S4+ | Blocks and workflows | judge #156 and evidence #158 as blocks; index, scorer, scoping, evolve rebuilt as blocks; workflows down the factory line (intake first); MCP server, `sindri open` | per stage | — | planned |

## Parallel execution (owner, 2026-10-11)

The epic runs as an **agent team**. This session is the **orchestrator**: it doesn't write stage code. It keeps every
lane busy, moves work between lanes, merges the lanes' results into the stage file, and holds the human gates. Each
worker agent runs `/specToProvenPR` for **one** stage or sub-stage in its own worktree and branch, from plan to ready
PR, and never merges.

### Lanes and what each waits on

| Lane | Work | Starts when | Base branch |
|---|---|---|---|
| A · S1 finish | gate → draft PR → `/review` loop → re-check → ready | now (in progress) | `feat/sindri-s1-app-shell` |
| B · S1b UI shell | Avalonia `Aw.Sindri.Ui` + `.Desktop`, `SindriUi` node, `http` edge kind in `deps.yaml` and `check-deps.mjs` | **now**: it only needs S1's HTTP contract (`docs/csharp/sindri.md` routes and the response records), which is frozen for S1 | stacked on `feat/sindri-s1-app-shell`; rebased after S1 merges |
| C · S2 engine | recipe schema + validator, block registry, instance rows, fenced idempotent transitions, timers | **now**, stacked: it builds on S1's `ILedgerStore`, `LeaseGrant`, `EventFactory` and the YAML reader | stacked on `feat/sindri-s1-app-shell` |
| D · S3 context retrieval | source adapters + query planner + evals over the reachable sources | **now**: its adapters and planner are Core/Infrastructure work with no S2 dependency; only its CLI/API mapping waits for S1 to merge | off `origin/HEAD`, rebased onto S1 once it merges |
| E · S4 blocks | judge (#156) and evidence (#158, merged) as blocks | when C's block registry contract is in its draft PR | stacked on C |
| F · S4+ workflows | intake first, then the factory line | when C merges | off `origin/HEAD` |

Within a stage, independent tasks fan out too: for example, in S2 the recipe validator, the block registry and the
instance store are separate tasks with separate test suites. A worker may run them as its own subagents (one task per
subagent, then a reviewer), so long as only one PR comes out per stage.

### Rules for the team

- **One stage = one worktree = one branch = one PR**, under `.claude/worktrees/sindri-<stage>`. Stacked PRs into their
  parent branch (addendum Q1); the orchestrator rebases a stack when its parent merges.
- **Heavy lock:** the box allows **2** heavy jobs at once (`~/.agentic-workflow/bin-r1b/heavy`, `AW_HEAVY_SLOTS`). Builds
  and tests always go through it, so at most two lanes build at once and the others plan, write, or review in the
  meantime. The orchestrator sequences full merge gates: one at a time, because they take both slots.
- **Shared one-line files** (`AgenticWorkflow.slnx`, `tests/Aw.Architecture.Tests/Packages.cs` and its csproj,
  `deps.yaml`, `AGENTS.md`, `Directory.Packages.props`, lock files, `scripts/ci/changed-areas.sh`): each worker edits
  them only in its own branch. The orchestrator checks every pair of open branches with `git merge-tree --write-tree`
  after each push and resolves conflicts with merge commits (as S1 did with #158/#161).
- **Contracts between lanes** are frozen docs, not chat: S1's `docs/csharp/sindri.md` for B and C, C's recipe and block
  docs for E. A lane that needs one of them changed asks the orchestrator, which changes it in the owning lane's PR.
- **Each worker follows `/specToProvenPR` in full:** a verification plan before code, a hand test, evidence, a draft PR,
  the `/review` loop to zero blocking, then ready. Each writes its log section in this file (its clock and C# lines).
- **Gates stay human:** workers never merge. The orchestrator marks PRs ready only when all three gates are green and
  reports merge order: S1 → S1b, then S2 → S3, then the blocks.
- **Always busy:** when a lane blocks (waiting on review, a parent merge or a heavy slot), the orchestrator gives its
  agent the next unblocked task from the table, or starts the next lane. Lanes talk through `SendMessage` with the
  orchestrator, never with each other directly.
- **Threat model and review severity** are the same for every lane: guard honest mistakes; fix issues and real-harm
  findings, and resolve suggestions and nits in writing.

### Kickoff (once S1's draft PR is open)

1. Spawn B, C and D as team agents (`isolation: worktree`), each with its row above, the handoff digest, this file and
   the instruction "run `/specToProvenPR` for stage <X>; first deliverables are the verification plan and the
   implementation plan; ask the orchestrator, not the owner".
2. The orchestrator keeps lane A (S1's review loop) moving itself, and checks merge-tree across branches after each push.
3. E starts when C's draft PR has the block registry; F starts when C merges.

## S1 log

- Clock: verification plan 2026-10-11T04:18:55Z → ready 2026-10-11T17:21:53Z.
- C# lines (src / tests, all of src/Sindri vs tests/Aw.Sindri.*): 4396 / 4692.
- Flakes re-run alone: Redaction unit-lane budget (load), Evidence lock-signal and page-lint tests (box lock under full slots), agent-token, statusline (heavy-wrapper env); all pass alone, none in this PR's code. Five review runs, then the owner's "fix, verify, mark ready".

## S2 log

- Clock: verification plan created 2026-10-11T06:16:36Z → draft PR #167 opened 2026-10-11 ~08:30Z → ready: _tbd_.
- C# lines vs S1 base (src / tests): +6291 / +2219 at the draft (181 / 46 files).
- Hand test: run 1 in the shared `scratchpad/hand` was void (another lane used that home too); run 2 in
  `scratchpad/s2-laneC/home2`, all 14 journeys pass. It found two display bugs (show's signals while held, the paused
  timer text), both fixed red first.
- Flakes: none in the final gate. Before the 0c3a2f1 merge, S1's `SchedulerHostTests` spun at 100% CPU (S1 fixed it),
  and `FileApprovalsFailureTests.A_save_into_a_read_only_directory_is_SND_PROFILE_005` failed once (S1-owned, fixed in 0c3a2f1).

## Lane notes

- **Lane B (S1b), 2026-10-11:** the UI is built and green locally (32 UI tests, 102 architecture tests), with NativeAOT
  `sindri-ui` at 19 MB and zero IL warnings. It added the `http` edge kind, tier 4 and `routes` in deps, and pinned
  Avalonia 12.1.3. Avalonia.Headless.XUnit 12.1.3 fails under xUnit v3 4.0.1 (MissingMethodException), so the headless
  tests drive `HeadlessUnitTestSession` from plain facts. The hand test waits on S1's `NeedsApproval` push. B saw the S1
  `SchedulerHostTests` lost-lease flake once under load (it is on S1's fix list, item 7/22).
- **Lane B gate, 2026-10-11:** green except one S1-owned item: Aw.Sindri.Tests' integration lane hung in Release under
  lane-budgets (658 s, killed). It's intermittent (the orchestrator's Release run finished) and is assigned to S1's fix
  worker (find the test, fix it, bound every waiting test). S1b's hand-test pixels are headless Skia renders of the shipped
  app against the live control plane: the terminal has no Screen Recording permission, so `screencapture` fails. An OS
  window screenshot needs the owner to grant Screen Recording.
- **Lane C (S2), 2026-10-11:** first agent stopped at its context guard. WIP `b21d39a` (local) builds clean: recipes
  via the S1 YAML reader, a validator for every §17.28.5 check, a block registry, instances as replays, one-transaction
  fenced commits through a new `IWorkflowStore.Commit` (S1's `ILedgerStore` left as is), universal states, timers through
  the scheduler, loops, budgets, item lifecycle + Step contract recipes, 14 slices, routes and the CLI. It typed
  `LedgerEntry.Item` as `ItemId?` and added migration 4. Deviation: code came before tests (stated in its PR). Re-dispatched
  from `digests/sindri-s2-engine-handoff.md`.
- **S1 gate at `0c3a2f1` (2026-10-11): GREEN.** Full local merge gate, every step: format, build -warnaserror, unit,
  integration, Release build, lane budgets, verify-packages, ratchet, sync-rules, check-deps/env/rules, no-any,
  no-v8-ignore, setup dry-run, TS typecheck+test ×5, bash suites, aw-live, AOT smoke. Log: scratchpad/orch/gate5/gate.txt.
  Review run 1: 42 findings (36 fixed, 6 written). Run 2: 1 issue (approval key A,B,A,B) and 20 suggestions/nits, all
  being fixed. Next: run 3.
- **Scratchpad collision, 2026-10-11:** every lane shares one scratchpad directory. Lane D overwrote the orchestrator's
  `gate.sh`, and lanes C and B collided on `scratchpad/hand`: lane C stopped a serve that lane B had started on lane
  C's home. Rule added to the lane brief: each lane uses `scratchpad/<stage>-lane<X>/` only. Lanes B and C redo their
  hand tests in fresh homes; the orchestrator uses `scratchpad/orch/`.

## S3 log (lane D)

- Branch `feat/sindri-s3-context`, worktree `.claude/worktrees/sindri-s3-context`, draft PR #166 (base `feat/sindri-s1-app-shell`).
- Clock: verification plan written 2026-10-11T06:15:01Z (`proof/sindri-s3-context/verification-plan.md`) → ready 2026-10-11T09:35Z (about 3 h 20 min).
- C# lines (src / tests): +1651 / +1621 against the S1 merge base.
- Review runs (cap 5): run 1: 0 blocking, 11 issues. Run 2: 0 blocking, 2 issues. Run 3: 0 blocking, 2 issues. Run 4: 0 blocking, 0 issues, so the loop stopped. Every issue was fixed; suggestions and nits were resolved in writing on the PR.
- Evidence: journey replayed on the final code, 19/19 PASS (`verification/20261011-093205-sindri-s3-context`).
- Flakes re-run alone: the `Aw.Evidence.Tests` lock-signal test (exit 75 instead of 130 under load, twice; 85/85 alone), `aw-state-dir-isolation.test.sh` (passed alone), and the Redaction unit lane budget (13.4 s under load, 5.6 s alone). An early Sindri test run hung on S1's SchedulerHostTests spin (fixed in S1 `0c3a2f1`, merged).
- Merge-tree (after S1 0c3a2f1): clean against S1; conflicts with S2 in `Core/PublicAPI.Unshipped.txt`, `Features/PublicAPI.Unshipped.txt` and `SindriErrors.cs` (both append lines and errors to `All`), and earlier with S1b in `deps.yaml` (the Sindri node note). The orchestrator resolves these as union merges.
- **Lane C (S2), 2026-10-11:** draft PR **#167** (base `feat/sindri-s1-app-shell`, S1 `0c3a2f1` merged in). Block
  registry contract in the PR body and `docs/csharp/sindri-engine.md` § Blocks. All 14 hand-test journeys re-run in a fresh
  home (`scratchpad/s2-laneC/home2`). Local gate green (unit 1757, integration 406). Review loop running.
- **Lane E (S4a) started:** judge and evidence as blocks, stacked on S2 (`feat/sindri-s4-blocks`). Judge #156 is still
  open elsewhere: lane E binds judge only through its CLI contract, and reports the real binding blocked if that isn't
  enough.

## S1b log (lane B)

- Clock: verification plan written 2026-10-11T06:14:06Z (`proof/sindri-s1b-ui-shell/verification-plan.md`); first commit
  06:17:17Z; draft PR #168 (base `feat/sindri-s1-app-shell`) ~08:35Z → ready: _tbd_.
- C# lines (src / tests): 743 C# + 174 axaml / 930.
- Findings: Avalonia.Headless.XUnit 12.1.3 fails discovery under xUnit v3 4.0.1 (MissingMethodException), so the tests use
  HeadlessUnitTestSession from plain facts. `screencapture` is blocked (no Screen Recording for the terminal), so the
  frames are headless renders of the shipped app against the live control plane (approved by the orchestrator). The
  hand test found that a stopping `serve` keeps SSE open for about 30 s (S1 round-2 item 12); the UI side is fixed in 214b880e.
- Flakes: S1 SchedulerHostTests lost-lease (failed once under load, passed alone) and the Release integration hang;
  both S1-owned, both fixed in S1 review-1.
- Owner question: the bare-`sindri` launch of sindri-ui isn't declared as a `Sindri -> SindriUi` cli edge (it would form a
  cycle with the http edge; check-deps doesn't scan C# for cli edges).
- **Lane E design (approved):** additive `IEvidenceProvider : IBlock` + `CollectEvidence` slice on its branch (S2 unchanged; optional `IBlockRegistry.Provider` offered to lane C); judge gates bound to #156's CLI contract via a `cli` edge (read-only build of #156 head for real checks, contract tests on the fake); all four evidence blocks out of process to `ui-evidence` (Playwright, no NativeAOT).
- **Lane C (S2) review run 1 (2026-10-11):** 26 findings: 3 blocking (a child id collides across parent attempts; a recipe-changed instance can't be cancelled and its timer blocks the queue; the correlation-id doc claim is false and cancel cascades one level only), 9 issues, 14 suggestions or nits. Second lane-C agent stopped at its context guard; handoff-2 → third agent dispatched. GateContext gains Recipe/Attempt/To/On, and gates run in rung order (lane E told).
- **S1 gate at `c23529b2`: GREEN** (35/35). Review run 3: 1 issue (CLI reports body-less non-2xx as not running) + suggestions; round-3 fixes dispatched (digests/sindri-s1-review3-fixes.md). Next: run 4 of ≤5.
- **S2 contract pushed (b29c2672):** GateContext(Instance, Recipe, Item, Attempt, State, To, On, Facts, Evidence, Sha); evidence check before gates, gates cheapest rung first with stop at first failure; instance keys wf:<id>; child ids from (parent, recipe, parentVersion); root correlation; effect kinds allowlisted at registration. Lane E told to merge.

## S4a log (lane E)

- Branch `feat/sindri-s4-blocks`, worktree `.claude/worktrees/sindri-s4-blocks`, base `feat/sindri-s2-engine` (S2 `b29c2672` merged in).
- Clock: verification plan written 2026-10-11T08:29:00Z (`proof/sindri-s4-blocks/verification-plan.md`) → ready: _tbd_.
- Judge bound through #156's CLI contract; real-binary checks against a read-only export of #156 head `fabba31e`.
- Contract additions (additive, on this branch): `IEvidenceProvider`, `EvidenceRequest`, `ProvidedEvidence`, `EvidenceProviders` (stands in for an `IBlockRegistry.Provider` S2 didn't add); `CollectEvidence` slice. The deps scan now reads `*.cs` (so the Sindri → Judge/Evidence cli edges are seen).
- C# lines (src / tests, all of src/Sindri vs tests/Aw.Sindri.*): 4396 / 4692. Review runs: _tbd_.
- **S1 review run 4:** 1 blocking (doctor and the summary bypass the one approval rule; the doc claim is false), 1 issue (approve/stop exit-code docs), plus suggestions; round 4 dispatched. Run 5 is the last allowed. gate7 at e8ee0ce1: the Evidence CLI lock-signal tests exited 75 (box lock timeout with every heavy slot in use) and Redaction hit 10.5 s against its 10 s budget, both load and environment; re-run alone at the final gate.
- **2026-10-11 status after S1's review cap:**
  - **S1 (#163):** review runs 1–5 used. Run 5 left one root cause (an unreadable ledger or approval folded into "needs
    approval" on status and the summary). The owner chose to fix, verify and mark ready without a 6th run. Fixed in
    `094ca788` and `2a31206a` (253 Sindri tests), AOT smoke ok, every journey re-checked on the final binary
    (manual-test.md "Re-check"). Final gate running.
  - **S3 (#166, lane D): ready for review**, at review run 4 with 0 blocking and 0 issue; base S1 at `ae8b6da3`. Owner
    question: spec PR #162 plans a separate `Aw.Context` package; lane D recommends keeping S3 in `src/Sindri` and
    extracting it once #162 is approved.
  - **Lanes B (S1b, #168), C (S2, #167), E (S4a): stopped by the account's weekly usage limit** (resets Oct 16, 5 am PT).
    - B: review runs 1–3 done with 0 blocking, mid run 4; its gate is green except S1's import order (now fixed).
    - C: mid round-1 fixes on #167 (the contract changes are pushed, the remaining items are in handoff-2); it was
      re-running its gate on S1 `c23529b2`.
    - E: run 3 recorded; it was about to start run 4 and gate 5; it had green tool blocks and the CollectEvidence slice.
    - Each resumes from its PR and handoff once the limit resets.
