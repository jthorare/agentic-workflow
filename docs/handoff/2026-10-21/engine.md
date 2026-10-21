# Engine lane handoff — 2026-10-21

Lane: `sindri-engine` (Sindri line recipe blocks, the engine side of goal 4: a line runs end to end with no desk answers).
Main at handoff: `ccddefc5b`. Every engine builder is stopped; all work is pushed. Nothing lives only on the old box.
Box-local detail (it doesn't travel): `~/.agentic-workflow/digests/sindri-engine-2026-10-21b.md`, briefs in `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-engine/`. The essentials are below.

## Rules that carry over

- Builders and reviewers run on Sonnet or Haiku, never Opus. Every brief carries `Goal:` and `Acceptance criteria:`.
- Merge, never rebase. Run the scoped `dotnet format --verify-no-changes --include <touched .cs>` before every push.
- Every heavy dotnet runs with `MSBUILDDISABLENODEREUSE=1`. Idle MSBuild nodes once ate 14 GB and starved the heavy-slot memory guard.
- `dotnet test ... -warnaserror` runs **zero** tests (MTP). Build with `-warnaserror`, then test with `--no-build`.
- Every merge goes through meta's batch. A PR goes to meta as `ready #N <sha>` after: a draft PR, a Sonnet review written to `scratch/<unit>/review.md`, blocking findings fixed, and the others answered in the body.
- The lane lead never asks the owner a question that blocks the session. Owner decisions go through meta.

## Branches and PRs

`wip/...` = `wip/handoff-2026-10-21/<branch>`. These refs hold ungated work, so the PR heads stay at their last gated sha.

| Unit | Branch | PR | Gated head (PR) | Pushed WIP head | State | What's left |
|---|---|---|---|---|---|---|
| **SO-E1** recipe displayName, version rule, manifest, IEmbeddedRecipes | feat/sindri-recipe-display-name | #532 (ready) | d8bc1911a | `wip/…` **f3beb4d41** (merge of main, unbuilt) | Meta's gate wants it next | **Version merge.** Main has #538 (bumps workflow.build/prove/ship) and #541 (bumps workflow.design/review-loop, adds step.lens.ask and step.review.apply-answer). Take the higher version, +1 where both sides changed it. New recipes from main need #532's displayName and plain-words fields. Re-pin StatePathOrderTests, ShippedDescriptorHashes, the recipe manifest, `eval --shipped` and `src/Sindri/aot-smoke/expected.txt`. Gate: unit lanes, Engine integration, aot-smoke, `recipe validate`, check-rules. Push to the PR branch, then `ready #532`. |
| **BS-2/2b** design and plan stations' nine Steps | feat/sindri-blocks-design-plan | #497 (ready) | a8638d330 (gate 9 green) | `wip/…` **c5dca0755** (main merged: union in ShippedBlocksTests, now 30 steps, plus the `step.review.` filter in ScopingBlockRuntimesTests) | **MERGED** into main as #555 (0cbf9a4; it contains a8638d330 and c5dca0755). #497 is superseded | Nothing. |
| **BS-3d** build red-check, checks, ship green on the block runtime (ICheckCommands) | feat/sindri-blocks-build-commands | #463 (draft) | 4e08fb452 | `wip/…` **e8473c7c7** (main merged; decision A started, unbuilt) | Fix round 1 (B1 one deadline, B2 refuse a dirty tree) is done and built green on 9aad93291 | **Decision A (lead, final):** adopt main's owner-approved `workflow.build.commands: string[]` (LN-WF9). red-check expects at least one line to fail, checks runs all, green expects all to pass. Per-command timeout = `workflow.steps.longMax` (LN-WF7). Line to argv: whitespace split with "double-quoted" groups, no shell; an unbalanced quote is a typed error at profile validate. Record the dropped per-command timeout and stage as a follow-up in the body. Then build, re-pin, gate, push, a narrow Sonnet re-check against the review, the body answers (green with no commands still greens; GitBranchChanges doesn't decode C-quoted paths), ready. |
| **BS-6b** prove.holdout-log | feat/sindri-blocks-holdout | none | — | **73fa5129e** | Red commit a8146b996 (19 step + 6 git tests fail at run time). Descriptor 9f40a7f7d reads fact:phasePacket (hash deb2e71a…→9511ad11…). Green code is committed but unbuilt | Build, green, docs line, gate, draft PR, review, ready. |
| **BS-13** proof feed + new deterministic `step.prove.collect` (option 2) | feat/sindri-proof-feed | none | — | **dc3731a9b** | C1 committed (step.prove.evidence reads fact:proof; hash 83e90ae9…→5f7170da…). C2 wiring (collect descriptor and registration, workflow.prove v2 with a collect state, evals incl. missing-item and stale-artifact) | Finish C2, C3 red, C4 green, gate, PR. Contract pins are hard-coded constants, not goldens. |
| **BS-8** ship.pack + ship.freeze | feat/sindri-blocks-ship-pack | none | — | **ea5043ade** (main merged on top of gate-green 45b85f1b0) | Gate green (Sindri unit 7789, integration 1652, Engine 441, Ui 3108, Arch 130) | Re-gate after the main merge, draft PR, review, ready. Body desk items: fact:review is assumed `{hash, dir}` with review.json `head` and `rounds[]`; fact:green is assumed to carry a sha. Base branch is `lineContext.stackParent` → `baseBranch` → `main`. Freeze compares recorded shas only, so effect.pr.open must re-check the head. step.ship.pack hash → 187d0835…. |
| **BS-2c** a redesign edits the previous design | feat/sindri-design-revise-edits (stacked on #497) | none | — | **5759907fc** | Gate green (Sindri unit 7358, Arch 128, Engine 155, Ui 2244, Sindri integration 1565, Engine integration 260 with 1 known flake) | #497 has landed (#555): merge main (0cbf9a4 or later). workflow.design → v3 (main's v2 + 1), workflow.line → v2. Re-pin, gate, PR, review, ready. The first pass passes the literal "First pass: no previous design." because recipe inputs can't be optional. |
| **CI-red autofix** (ci-fix-brief.md) | feat/sindri-ci-fix | none | — | **54ddcce1e** | Ports (IPullRequests, IWorkspaceChanges, ISecretScan, IAgentStepInstructions), the agent-launch seam (opt-in for step.pr.ci-fix only, marker `ci-fix-agent-workspace`), stubs, fakes and tests. Red build not yet clean (PublicAPI) | Red build clean → red commit → green, descriptors (step.pr.ci-log, step.pr.ci-fix, effect.pr.push, gates diff-scope/secret-scan/autofix-cap), babysitter v3, event pr.pushed, new profile key `workflow.pr.autofixCap`, docs, gate, PR. |
| **aot-smoke serve flake** (`'sindri serve' didn't answer within 10 s`, hit #540/#541/#549) | fix/sindri-smoke-serve-ready | none | — | **ccedd556a** (one test edit, red not confirmed) | Just started | Pick (a) raise the StartCommand readiness `Budget` (src/Sindri/Aw.Sindri.Host/Commands/StartCommand.cs:134) or (b) make the smoke poll status. Red, green, smoke twice, PR. Meta wants it right after #532. |

## Merge order and dependencies

1. **#532** (meta's gate is waiting on it).
2. ~~#497~~ merged as #555. BS-2c now builds on main.
3. **#463**, which unblocks BS-9.
4. BS-8, BS-6b, BS-13 and the CI fix in any order.
5. The smoke fix as soon as it's ready.

## Not started (engine)

- **BS-9 prove.before-after.** Needs #463 merged (ICheckCommands). It needs a brief.
- **Forge wiring proper** (about 11 h; brief `forge-wiring-brief.md` plus the owner's answers):
  - What's needed: #463, BS-8 and #538 (merged); contract item 3 (effect replies carry facts) is done in #533.
  - The scope:
    - new `effect.pr.open`, `step.pr.snapshot` and events pr.opened/pr.changed;
    - a pr-watch service;
    - ship and babysitter recipes v2, with descriptors;
    - a Sindri-owned `IPullRequests` port (reuse the CI fix's) and a Host adapter over IForge (the fan-out limit);
    - deps.yaml edges, which need review.
  - Owner answers: push from the line's own worktree; the remote is the checkout's origin, checked against forge.hosts; add the CI-red autofix loop (the CI fix unit above); review comments go to the desk.
- **AGAIN** (optional `again: <address>` on workflow start, for ui): low priority.
- **Open question:** does `workflow.plan` treat `dropped` as failed, while other recipes treat it as unresolved? Check it against the line spec.

## Cross-lane agreements (still binding)

- **ideas owns** BS-7 #535, BS-11, review.record, LN-PK lite, STEPRUN-EV, PB-R #545, and BF-S (the four bugfix Steps; the Sindri→BugFix cli edge is approved: an IBugFixState port and an AW_BUGFIX_BIN adapter, the edge in its own commit). **Ping ideas when IPullRequests lands**, so it can build step.docs.scope. BS-11 combine: children route noTarget → `skipped` {selected:false}, and combine fails only when every part is unselected.
- **api:**
  - StartedBy on InstanceStart (optional; no workflow branch reads it, so no hash move);
  - api serves steps[].needs and produces on the instance detail;
  - #528's EmbeddedShippedEffects re-points onto IEmbeddedRecipes, whichever lands second;
  - A7a asserts the embedded count;
  - E3 carries RedeemedPlan.HeldMs into recipe.approved.
- **ui:** "Run again" = a new workflow start. #532 serves the stage displayName, why and transitionLabels. Per-stage times come from api plus STEPRUN-EV.
- **E2/E3** (setup-once) come after E1. The scope is in f5-brief.

## Owner approve-all step (before the e2e proof)

Once the units above are merged, the owner runs `sindri recipe approve` on every moved recipe and descriptor in one pass, then re-approves the profile:

- every recipe, via #532's displayName (E1);
- workflow.scoping v3 and step.scope.freeze (#529, merged);
- workflow.build, prove and ship (#538, merged);
- #541's lens.log, review.log, review.fix, review-loop v3, design v2, lens.ask and review.apply-answer (merged);
- #463's descriptors;
- step.prove.holdout-log (BS-6b);
- step.prove.evidence, step.prove.collect and workflow.prove (BS-13);
- step.ship.pack (BS-8);
- step.design.intake, step.design.draft, workflow.design and workflow.line (BS-2c);
- the CI fix's new descriptors plus babysitter v3;
- the forge wiring's ship and babysitter v2;
- ideas' BS-11 evidence children, BS-12 and #545 PB-R (pr-babysitter, docs-sync, prove-recheck). PB-R also needs the owner to adopt its seed (PB-S1).

The profile re-approval is for the new key `workflow.pr.autofixCap` (CI fix). Tell meta when the line is one step away from the e2e proof.

## Next step for the new engine session

Start a Sonnet builder on #532's version merge from `wip/handoff-2026-10-21/feat/sindri-recipe-display-name` (f3beb4d41). Push it to the PR branch, then `ready #532`. In parallel:

- #463 decision A from its `wip/…` ref;
- the smoke fix.

Then resume BS-6b, BS-13, BS-8, the CI fix and BS-2c, each from the head in the table.
