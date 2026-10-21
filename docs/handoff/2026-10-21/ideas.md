# ideas lane handoff, 2026-10-21

Lane: ideas (session `sindri-ideas`). Running log `~/.agentic-workflow/digests/sindri-ideas-lane.md` and digests
`sindri-ideas-2026-10-2*.md` stay on the old box; this file replaces them. PRs are on `jthorare/agentic-workflow`
(remote `jthorare`).

**Owner goal 4 (line recipe blocks fully built out), split agreed with engine:** ideas owns BS-7, BS-10, BS-11, BS-12,
review.record, LN-PK lite, STEPRUN-EV, PB-R and BF-S. Engine owns everything else (#497, #532, #538, #463, BS-6b, BS-13,
BS-8, BS-2c, the CI-red autofix, BS-9, the forge wiring). The full LN-PK comes after LN-PK lite. SC-E is paused.

**Update (after handoff):** the owner merged PB-R through #557 and #545 (main f018e5d). PB-R is done; the step.docs.check re-pin now falls on BS-11.

**Merged since 2026-10-20:** #523, #526, #529, #530 (BS-10), #539 (flake), #541 (BS-12). main was at `ccddefc` at handoff.

**How the stop happened:** every builder hit the account usage limit around 03:59Z. Nothing ran from 04:00 to 14:40Z.
I resumed them at 14:40Z, then stopped them at about 14:50Z on the owner's handoff order and killed their build processes.
**No head below has finished its surface checks after its last main merge.** Re-run them before you send `ready`.

## Branches and PRs

Everything is pushed to `jthorare`: for every branch, the remote head equals the worktree head (checked at handoff).
Nothing is uncommitted. Three branches end in a `wip: handoff snapshot` commit (`--no-verify`, unbuilt).

| Unit | PR | Branch | Head | Pushed | main merged | State |
|---|---|---|---|---|---|---|
| BS-7 scoping Steps | #535 | `feat/line-bs7` | `dda494073` | yes | ccddefc | Done. Surface checks green on earlier heads (446c0bd16, 2bedebd5d); not re-run on dda494073. Meta dropped it from a train on conflicts; needs a fresh `ready` |
| PB-R babysitter recipes | #545 | `feat/pb-recipes-pbr` | `a99f8bf9d` | yes | #541 head + ccddefc | Done; was ready at 953946f60. Meta dropped it: conflicts with #541. Now has #541's head and ccddefc merged in; re-pin and checks not finished |
| BS-11 evidence + docs Steps | none yet | `feat/line-bs11` | `8030a42c7` | yes | ccddefc | 3 of 4 Steps done (evidence none/combine, docs.check, plus children noTarget → skipped). docs.scope NOT built (blocker below). Checks not finished on this head |
| STEPRUN-EV | none yet | `feat/sindri-steprun-events` | `9836982cb` | yes | ccddefc | Done: the step-run open row carries `actor`, `attempt`, `maxAttempts`. Surface checks green after the 9260072 merge (build, scoped format, Engine unit 166, Sindri unit 7758); the ccddefc merge touched none of its files. Can go straight to a PR |
| BF-S, 4 bugfix Steps | none yet | `feat/sindri-bugfix-steps` | `4b267b5cb` | yes | 9140177 only | `13203acea` = the Sindri→BugFix cli edge in deps.yaml (own commit; engine approved it). `4b267b5cb` = WIP: red contracts plus green code (IBugFixState port, AW_BUGFIX_BIN adapter, Steps), unbuilt |
| LN-PK lite | none yet | `feat/sindri-lnpk-lite` | `7e3335dc7` | yes | 9140177 via #541's head | `6af5f6595` built-in packs design-five / code-review. `7e3335dc7` = WIP (59 files): lens.round + review.round on the runtime, packs port, eval cases, recipe edits, unbuilt. Was rewriting contract files with `Expect.*` and fixing analyzer errors |
| review.record | none yet | `feat/sindri-review-record` | `eaa97a395` | yes | ccddefc | `71a32ee88` red (step.review.record records the review result packet and machine-freezes it). `eaa97a395` = WIP green (ReviewPack, ReviewRecordStep, inputs, phase packet), unbuilt |
| SC-E prompt eval | none | `feat/scoping-prompt-eval-sce` | `d68d2fe19` | yes | old | Paused. WIP red slice 1, doesn't build |

## What's left, per unit

- **#535 BS-7:** re-run surface checks on the head, then send meta `ready #535 <sha>`. It moves no recipe hashes.
- **#545 PB-R:** NOTE: the owner opened #557 from the same branch (`feat/pb-recipes-pbr`). Don't push #545 further unless the owner says so; the steps below apply to whichever PR the owner keeps. Finish the re-pin after the #541 merge, then run surface checks (build, Sindri and Ui unit lanes, recipe
  validate and eval on all shipped recipes, scoped format). Push, then send `ready #545 <sha>` with the final hashes.
  Hashes at 953946f60 (they may move with the #541 merge):
  - `workflow.pr-babysitter` sha256:77bc5d78990c0b35e0fea00b52701e559a51b3fd0d70f9d2fb53d105390db434
  - `workflow.docs-sync` sha256:047436e3c164de8093cae1c00d3a55263334e242e0fd071cd4d9c771177eaee1
  - `workflow.prove-recheck` (new) sha256:3ce21eeef47ef3602f3ade8bcd9564fc2042e1cf8a63bbfdeac2b4b9dcc8000f
  - `step.docs.scope` and `step.docs.check` re-pinned (new params). `step.docs.open-pr` removed.
  - The PR body lists 8 spec deviations, says `f5004697d` is mislabeled, and says PB-S1 (the owner adopting the seeds)
    blocks `recipe approve`.
- **BS-11:** checks, then open the PR. List docs.scope under "still at the desk", and give the 3 child recipe hashes that
  move (noTarget → skipped). Send ready to meta, and the PR number and hashes to engine.
- **STEPRUN-EV:** checks, open the PR, and send sindri-api the field names (`actor`, `attempt`, `maxAttempts`) for #469.
- **BF-S:** build the WIP, fix it to green, merge main, run checks (include `node scripts/check-deps.mjs` and
  `scripts/check-env-example.sh`; AW_BUGFIX_BIN is in .env.example), then open the PR. Its body must say the
  Sindri→BugFix cli edge is the reviewed change engine approved.
- **LN-PK lite:** finish the contract rewrite (`Expect.*`) and the analyzer fixes, build, merge main (it has #541 through
  its base), run recipe validate and eval (the review-loop evals and recipes changed), then open the PR with moved hashes.
- **review.record:** build the WIP green against red `71a32ee88`, run checks, then open the PR.
- **SC-E:** stays paused until the owner says.

## Cross-PR coordination

- **step.docs.check re-pin, #545 vs BS-11 (#545 has merged, so BS-11 merges main and re-pins):** both change the `step.docs.check` descriptor. Whichever merges second merges
  main and re-pins `step.docs.check` (and any recipe that pins it) against the other. Meta knows and flags it on the merge.
- **#532 E1 (engine) recipe versions:** E1 bumps every recipe version. Whoever merges second keeps the higher version, plus
  one if both changed the recipe. This applies to #545, BS-11, LN-PK lite and review.record.
- **LN-PK lite and review.record** were branched off #541's head (52bf1f2a5). #541 has merged, so after a main merge their
  PRs diff against main only.

## Blockers

- **docs.scope (BS-11):** waits on engine's `IPullRequests` port (the forge unit: read side, no new deps edge). Engine pings
  ideas when it lands; then build docs.scope in a follow-up.
- **PB-R recipe approve:** blocked on PB-S1, the owner adopting the seeds.
- **Owner decisions still to raise:**
  1. What `noBehaviourChange` means (#362).
  2. CO-4a.
  3. PB-R's two spec questions: the docs-owner-check expectation contradicts the recipe, and the spec's "gate refusal parks
     needs-answer" isn't what the engine does.

## Worktrees on the old box (nothing in them that isn't pushed)

`~/repos/aw-ideas-{bs7,bs11,pbr,steprun,bfs,lnpk,sce}`, `~/repos/aw-wt/engine-bs12` (review.record; it was #541's worktree).
Merged and `dotnet clean`ed but not removed: `aw-ideas-{w23,lnk,bs10}`, `aw-wt/engine-flake2`. Removal goes through meta's
`aw-meta worktree-sweep`. `aw-ideas-pbr` is cleaned (the rebuild after the #541 merge refilled it).

## Rules that bit tonight

- Builders: Sonnet only, `MSBUILDDISABLENODEREUSE=1` through `~/.agentic-workflow/bin-r1b/heavy`, surface checks only
  (meta runs the full gate). Run `dotnet clean` once the PR is with meta. Kill only exact PIDs: a builder's
  `pkill -f never` around 02:50Z hit other lanes' jobs.
- Builders often say "waiting on X" when nothing runs. Check with pgrep or `/proc/<pid>/cwd` on the worktree.
- On a usage refusal: stop the builders, log it, tell meta (the limit is shared across models).

## Next step

On the new box: fetch `jthorare`, give each unit above to a Sonnet builder in its own worktree, in this order: #535 and
#545 first (ready right after checks), then STEPRUN-EV, BS-11, review.record, BF-S, LN-PK lite.

## Appendix: builder briefs (they lived only on the old box)

### BS7-BRIEF.md

````markdown
# BS-7 brief (untracked)

Goal: three scoping Steps run on the block runtime: step.scope.evidence, step.scope.read-tracker, step.scope.read-change.

On main (5d0b430): all three have descriptors and desk-helper code only for evidence (DeskBlocks.Evidence); none has a block runtime
(not in AddScopingSlices, not in the sorted runtime lists). read-tracker and read-change have no desk code, no port, no adapter.

Acceptance:
1. evidence: ScopeEvidenceStep (IBlockStep via ScopingCodeStep), writes the packet files into the run area; contract + hash; registered; sorted lists.
2. read-tracker: ITrackerSource port in Core, FileTrackerSource (fixture export shape) in Infrastructure, InMemoryTrackerSource fake, TrackerSourceContract run by both; step brief
   (R1 = the record, children/comments capped 50/20, scrubbed), anchors via the text shape; empty when no text; no source -> SND-SCOPING-005.
3. read-change: IChangeSource port in Core, GitChangeSource in Infrastructure, InMemoryChangeSource fake, ChangeSourceContract run by both; brief from PR body or first commit
   message, commits, changed files, diff capped 2 MiB, denied-path hunks dropped, diff and messages scrubbed; empty when no diff.
4. TDD: red commit then green; descriptor/recipe hashes unchanged unless reported; gate per lane-builder-rules.

````

### BS11-BRIEF.md

````markdown
# BS-11 brief (untracked)
Goal: step.evidence.none, step.evidence.combine, step.docs.check, step.docs.scope on the block runtime.
Found on main: descriptors only (EvidenceSteps, DocsSteps, kind block); no runtime for any of the four.
- evidence.none (DT spec 5/3.3): reads fact:built; 'done' with fact:evidenceSummary {kind:none, built, text}.
- evidence.combine (DT spec 5.3): reads evidenceUi/Api/Db; 'done' with evidenceSummary {kind:combined, ui, api, db}; a missing part fails the Step.
- docs.check (PB spec 7.2, API17): reads docsScope {base, paths[], candidates[{path,designNamed}]} + workspace path. Rules: DOCS-PATH-001 changed path outside globs/designNamed, DOCS-LINK-001 relative link does not resolve, DOCS-MD-001 unbalanced code fence. pass / fail (findings). Needs a changed-paths port (IWorktreeChanges, Core; git in Infrastructure).
- docs.scope: needs the PR's head, state and changed paths (a forge read) and a docs worktree; Sindri has no forge edge (deps.yaml) and no step.pr.snapshot. Left at the desk unless a port exists; report it.

````

### review-record-brief.md

````markdown
# review.record: `step.review.record` on the block runtime (ideas builder brief)

**Asked by:** engine, 2026-10-21. It is the last review-station Step BS-12 left at the desk, and it was not blocked.

## Where you work
- **Branch point:** if BS-12 (`feat/sindri-blocks-review-loop`) has merged, start from main. If it hasn't, branch from
  BS-12's head and say so in the report.
- **Read first:**
  - `lane-builder-rules.md`, which is binding.
  - `~/.agentic-workflow/digests/engine-bs12.md` and `engine-bs4.md` for the review fact shapes.
  - The line spec's review station table and §3.2, which lists the review packet's files.
  - `docs/csharp/packets.md` for the `review` kind (LN-PKT1).
  - The IPackets port in `src/Sindri/Aw.Sindri.Core/Packets`.
  - The model to copy: `ProveEvidenceStep` and `ProofPack` in `Features/Prove`.
- **Context-guard hook:** ignore it. It shows the lead's token count.

## Goal
1. **A ReviewPack file builder, modelled on ProofPack.** It writes the review packet's files into the run area:
   `packet.json` (whose `links` name every file and `index.json`), `index.json` (whose `parents` name the phase packet's
   hash), and `review.json`. `review.json` is shaped by `schemas/` `review-packet.schema.json`, or the review kind's
   schema in aw-packets, whichever is the registered one.
2. **`step.review.record` on the block runtime.** It assembles the packet from `fact:review` and `fact:lensRounds`, runs
   aw-packets validate with `--kind review`, then hash and freeze with `--by machine`, all over IPackets. It writes the
   packet fact `{ hash, dir }` in the shape the ship station reads. Put it in its own slice, registered with
   `AddBlockRuntime`, with a `BlockContract` and an entry in the sorted runtime lists.
3. **Hashes.** If the descriptor must change (Reads or Writes), give the change its own commit and list old → new.
   Otherwise no hash moves. Recipe hashes move only if the recipe changes.

## Acceptance
- **Clean loop:** a clean review loop records a review packet that `aw-packets validate --kind review` passes. It is
  frozen with `by: machine`, and its hash is in the fact.
- **Invalid packet:** a packet that fails validate fails the Step clearly (the PKT code reaches the reason) and freezes
  nothing.
- **Gate:**
  - `build -warnaserror`
  - scoped format
  - Architecture
  - the Sindri and Engine unit lanes
  - the Sindri integration lane, with aw-packets built
  - the Engine integration lane
  - `recipe validate` and `recipe eval` on every shipped recipe
  - Ui.Tests, if a recipe or catalog pin moved

## Report
Report the head sha (pushed), the packet files and an example of the fact, any moved hashes (old → new), the gate
totals (raw Total: lines) and any deviations.

````

### lnpk-lite-brief.md

````markdown
# LN-PK lite: built-in lens packs, and lens.round and review.round on the block runtime (ideas builder brief)

**Asked by:** engine, 2026-10-21. LN-PK lite is the smallest pack resolver that lets `step.lens.round` and
`step.review.round` leave the desk. The full LN-PK (repo and machine packs, pins, `sindri pack check`, the style pack
`plain`, LN-WF5 bundles, LN-UI5, LN-API8) is a later unit.

## Where you work
- **Start point:** BS-12 (PR for `feat/sindri-blocks-review-loop`). It changes the review-loop and design recipes and
  the lens and review descriptors.
  - If BS-12 has merged: work from main.
  - If it hasn't: branch from BS-12's head and say so in the report. The PR then stacks on BS-12's.
- **Read first:**
  - `lane-builder-rules.md` (binding).
  - The line spec `docs/superpowers/specs/2026-10-15-sindri-spec-to-proven-pr-line-design.md`: §5.2 (the lens pack),
    §5.3 (checking a pack), the LN-PK row in §13, and the design and review station tables (lens rows).
  - `~/.agentic-workflow/digests/engine-bs4.md`: the fact shapes for lensQueue, lensRounds and the review fact.
  - `docs/csharp/sindri-workflow.md`, "Lens loop facts".
  - The LensLoops slice: `src/Sindri/Aw.Sindri.Features/LensLoops`.
  - The descriptors: `Features/Blocks/Step/*Steps.cs`.
- **IGNORE the context-guard hook:** it shows the lead's tokens.

## Goal
1. **The built-in packs live in the toolkit:** `config/packs/lenses/design-five/` and `config/packs/lenses/code-review/`,
   each a `pack.json` (§5.2 shape: `kind: "lenses"`, `id`, `version`, `applies`, `lenses[]` with `id`, `title`,
   `prompt`, `when`) and a `lenses/<id>.md` per lens.
   - `design-five`, `applies: design`: product, architecture, design, developer experience, security. The /autoplan
     lenses cover these, so take the prompt text from `skills/productReview`, `skills/archReview`,
     `skills/planDesignReview`, `skills/planDevexReview` and `skills/cso` (plan mode), cut to a lens prompt: what to
     look for and what each severity means.
   - `code-review`, `applies: review`: the domains `/review` spawns today (see `skills/review/SKILL.md`).
   - At most 10 lenses per pack. `when` is `always`, or `{ paths: [...] }` for review lenses that only fit some paths.
   - App-agnostic: no work-app names, URLs or stacks.
2. **Resolver (Core port, Infrastructure adapter):** resolves BUILT-IN packs only.
   - **Selection:** the profile keys `workflow.packs.lenses.design` and `workflow.packs.lenses.review` choose the pack id,
     defaulting to `design-five` and `code-review`. Validate the keys, show them in the approval diff, and add rows to
     `docs/csharp/sindri-profile.md` (`check-profile-doc` must pass).
   - **What it returns:** `{ id, hash, lenses[] }`, with the hash from the pack's files (aw-packets hash, or the same
     canonical hash the build pins).
   - **Codes:** an unknown pack id or a pack over 10 lenses is a clear `Result` error. Do NOT register or run
     SND-PACK-001/-002: they stay not-run, as today. There are no repo packs, no machine packs and no pins.
   - **Tests:** a contract test base, run by both the adapter and a fake.
3. **`step.lens.round` and `step.review.round` on the block runtime** (LensLoops slice, `AddBlockRuntime`,
   `BlockContract`, the sorted runtime lists).
   - Each round queues the pack's lenses into `fact:lensQueue`.
   - **Selection:** `when` selects the lenses. For design it uses the scope's surface kinds; for review, the changed paths
     (`IRunDiff.ChangedPaths` from BS-11, if it's on main; otherwise the branch diff port that exists).
   - A lens not selected is recorded `not-applicable` with its reason, never silently skipped.
   - The rounds carry and update `fact:lensRounds`, following BS-4's shapes.
4. **Fix round 1:** `fact:lensRounds` must exist before the first round. Seed it empty (`[]`) at the station's intake,
   or produce it on the transition into round, so that round 1 holds it. Pick whichever needs fewer recipe changes and
   says plainly in the recipe what happens.
5. **Recipes:** descriptor and recipe hashes WILL move. That is expected; owner-approved through F5's approve-all.
   - Put each change in its own commit, and list every old → new hash.
   - Bump each recipe version. E1 (#532) also bumps every recipe version, so whichever merges second keeps the higher
     version, plus one if both changed.
   - Update the eval cases, and keep every replay fixture on main green with no change.

## Not in scope
Repo and machine packs, pins (`workflow.packs.pins`), `sindri pack check`, the style pack and `plain`, LN-WF5 bundle
copying into the Step directory, LN-UI5, LN-API8.

## Acceptance
- **Built-in resolution:** design resolves to `design-five`, and review to `code-review`.
- **Profile override:** a profile naming another built-in id resolves that pack, and an unknown id fails clearly.
- **`when` selection:** a review lens whose `when.paths` doesn't match the changed paths is `not-applicable` with its
  reason, and a matching one is queued.
- **Round 1:** it runs with `fact:lensRounds` present. A served review loop goes clean in 2 rounds with no human, from
  start to `clean` (an eval case).
- **Gate:**
  - build -warnaserror;
  - scoped format;
  - Architecture;
  - the Sindri, Engine and Ui unit lanes;
  - the Sindri and Engine integration lanes;
  - check-rules, check-deps and check-profile-doc;
  - `recipe validate` and `recipe eval` on EVERY shipped recipe.
- **Ui.Tests:** run them, because the catalog may pin these recipes.

## Report
Head sha (pushed), the commits, the pack contents (lens ids per pack), the resolver's port shape, the moved hashes
(old → new), the round-1 fix chosen, the gate totals (raw Total: lines) and any deviations.

````

### bugfix-steps-brief.md

````markdown
# BF-S: the four bugfix block Steps on the block runtime (ideas builder brief)

**Asked by:** engine, 2026-10-21. These four Steps are independent of the line's other units:
`step.bugfix.start-attempt`, `step.bugfix.record-candidate`, `step.bugfix.recheck` and `step.bugfix.judge`.

## Where you work
- **Worktree and branch:** a new worktree from main, branch `feat/sindri-bugfix-steps`.
- **Read first:**
  - `lane-builder-rules.md`, which is binding.
  - The descriptors in `src/Sindri/Aw.Sindri.Features/Blocks/Step/BugfixSteps.cs`. On main they are descriptors only, with no runtime; confirm this against `ShippedBlocksTests`' runtime list.
  - The decision-tree spec, `docs/superpowers/specs/2026-10-15-sindri-decision-tree-recipes-design.md`, §4 and §6. It covers the bugfix blocks, their outcomes and facts, and the attempt cap and split.
  - The BugFix tool: `src/BugFix` and `docs/csharp/bugfix.md`. It is the C# bugfix-state helper that /bugFixOrchestrator uses, and it is the model for attempts, candidates, rechecks and the resolution check.
  - The test fixture recipe `tests/Aw.Sindri.Testing/Workflow/Recipes/valid/workflow.bugfix.yaml` and `DecisionTreeBlocksTests`.
  - An existing block runtime slice as the shape: `Features/LensLoops`, or the scoping Steps BS-7 added.
- **Context-guard hook:** ignore it. It shows the lead's tokens.

## Goal
Put each of the four Steps on the block runtime, each in its own slice. Register each with `AddBlockRuntime`, give it a `BlockContract`, and add it to the sorted runtime lists. Use the descriptors' outcomes and facts as they are:
- **start-attempt** reads `fact:decision`. It reports `single` or `split` with `fact:attempt`, or `capReached` when the attempt cap is used up. Take the cap and the split rule from the DT spec, and from BugFix where it already decides them.
- **record-candidate** reads `fact:diff` and reports `recorded` with `fact:candidate`. Choose the shape the recheck and judge need: the candidate's head, its diff summary and its attempt.
- **recheck** reads `fact:check` and `fact:candidate`, re-runs the reproduction check against the candidate, and reports `passed` or `failed` with `fact:recheck`. It runs the check through an existing port, such as the check commands (ICheckCommands) or the run diff. Do not shell out ad hoc.
- **judge** reads `fact:check`, `fact:candidate` and `fact:evidenceSummary`. It reports `resolved`, `partial`, `unresolved` or `needsHuman` with `fact:verdict`.
  - **The deterministic part:** a failed recheck or missing evidence can never be `resolved`.
  - **The model part:** if the resolution check needs a model (judge's resolution-check), check whether Sindri already has a port for it, such as an IPromptModel-style port or a Judge adapter.
  - **If a new deps.yaml edge is needed:** for example Sindri→Judge. STOP and report it; don't add the edge.

## Rules
- **Red first:** commit the failing tests, then the green implementation.
- **Hashes:** no descriptor change is expected. If one is needed, put it in its own commit, list old → new, re-pin ShippedDescriptorHashes in its own commit, and stop to report before going further.
- **Checks:** builders run surface checks, and meta runs the full gate. Yours are:
  - build -warnaserror
  - the Sindri unit lane
  - the Engine unit lane
  - the Sindri integration tests for your classes only
  - recipe validate and eval on any recipe you touch
  - the ShippedDescriptorHashes test
  - scoped format
  - check-deps

## Report
Report:
- the head sha, pushed
- the fact shapes you chose, with an example of each
- how recheck runs the check
- how judge decides, including whether it uses a model
- the raw Total lines
- deviations

````

