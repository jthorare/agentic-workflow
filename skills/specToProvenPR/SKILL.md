---
name: specToProvenPR
description: Use when turning an approved spec or design doc into production-ready pull requests that are definitively proven to work in the running app. Use for staged multi-phase builds where each stage must be planned, implemented, tested by hand in the running app, evidenced, and only then driven to zero blocking review findings. Triggers include "take this spec to PRs", "ship this design", "prove it works then open the PR", staged epic delivery, and review-loop-until-clean. Also use when tempted to stop a review loop early, treat green tests as proof, or defer findings to a follow-up.
argument-hint: "[approved-spec-or-design-doc-path] (no argument = resume from stages.md)"
allowed-tools: Bash(git *), Bash(gh *), Bash(npm *), Bash(npx *), Bash(SHARED_DIR=*), Bash(source *), Bash(mkdir *), Bash(ls *), Bash(cat *), Bash(test *), Bash(date *), Agent, Read, Write, Edit, Glob, Grep, Skill, TodoWrite, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# specToProvenPR

Take an approved spec to production-ready PRs that are **definitively proven to work in the running app**, one shippable stage at a time.

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

## Core principle

**Order of operations: function first, review last.** Within a stage the sequence is implement → test it by hand in the running app → prove it with an evidence pack → gather `/ui-evidence` when the spec calls for it → *then* open the draft PR and run the `/review` loop. The review loop is the last quality pass over behavior that is already known to work; it is never how you find out whether it works. A stage that goes to `/review` without hand-tested, evidenced behavior goes back to step 4.

A stage is **DONE** only when all three gates are green at once:

1. **PROOF**: the spec's behavior was observed in the running app (not merely unit-tested), with an evidence pack captured per `_shared/evidence-pack.md` (`pack.json` verdict not FAIL) and an independent cross-check for every claim.
2. **REVIEW=0**: the most recent `/review` run returned **zero blocking findings** (see "Blocking or not" below), and every non-blocking finding from it is resolved in writing. Not "I fixed them, re-review is unnecessary".
3. **GREEN**: the full test suite and CI pass.

Then mark the PR ready and **STOP for human merge**. "Practically done" and "trivial delta" mean: not done while a blocking finding is open.

## When to use

- An **approved** spec (e.g. `planning/specs/<topic>/DESIGN.md`) that must ship as staged, proven, review-clean PRs — one stage per PR. Not approved → stop and approve it first.
- **NOT for**: a one-line fix with no observable app behavior, or pure docs. Use a normal commit.
- **Resume mode**: invoked with no argument, read `plans/<epic>/stages.md`, print a state synopsis (per-stage status, current step, open PR, last verdict), and continue from the first incomplete step.

## Stage steps

**Track todos** — one item per stage from stages.md. `superpowers:*` references below name skills from the superpowers pack: **Invoke skill** them when installed on the host; otherwise follow the named practice directly. Shared files and output dirs resolve via this block — re-source it at the top of every bash block (shell state does not persist between shell calls):

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
STAGE_DIR="$AW_DIR/proof/<stage-slug>" && mkdir -p "$STAGE_DIR"
```

**0. Isolate (once per epic).** Create a dedicated worktree (**REQUIRED:** superpowers:using-git-worktrees). All stage work happens there.

**0.5. Stage map + contracts (once per epic).** Decompose the spec into staged, independently shippable PR-sized units (match the spec's phases; one stage = one PR) and write `plans/<epic>/stages.md`: per stage — goal, observable signal, files touched, estimated size, status. Then record two contracts in stages.md:
- **Autonomy contract** — **Ask the user** once, up front: pause for approval after each stage, or run continuously to epic end?
- **Git topology contract** — detect the base branch (`gh repo view --json defaultBranchRef -q .defaultBranchRef.name`) and the push remote (fork vs origin) up front and state them. **Never push the base branch**; each stage gets its own branch off base.

**1. Plan the stage.** Expand this stage's stages.md entry into an implementation plan. **REQUIRED:** superpowers:writing-plans, scoped to this stage only.

**2. Verification plan — written BEFORE you implement.** Write `$STAGE_DIR/verification-plan.md` from `_shared/verification-plan-template.md`: app entry, ≥1 journey (≥3 interactive steps), **≥3 named lenses** from `_shared/verification-lenses.md`, and ≥1 independent cross-check per numeric/behavioral claim. **GATE:** the plan's `created` timestamp must predate the stage's first implementation commit (`git log --diff-filter=A --format=%cI -- <files> | tail -1`); a plan written after code is invalid — regenerate the stage. If you cannot state the observable signal, the stage is underspecified: fix that first.

**3. Implement.** **REQUIRED:** superpowers:test-driven-development, executed via superpowers:subagent-driven-development or superpowers:executing-plans. Start the app with the project's own run recipe (`/run` or the documented dev command) — never assume a Node app.

**4. Test it by hand in the running app.** Before any evidence pack, use the feature the way a user would, in the real app (start it with the project's run recipe, `/run` or the documented dev command). This is exploratory, not scripted: walk each observable signal from the stage entry in stages.md, then the unhappy paths (empty, invalid, boundary, back/refresh, permission-denied, the failure the spec worries about). Use the browser tools for web UI, the simulator for iOS, the real desktop app on seeded data for a desktop UI (start it as the repo's desktop evidence config does, and compare each screen with its mockup by eye as you go), the real CLI or API call for everything else. Write `$STAGE_DIR/manual-test.md`: per signal, what you did, what you expected, what you observed (a screenshot path or raw output for each), and every surprise. Anything that does not work how the spec wants it is fixed now, back in step 3, and this step re-run. The hand test is the only place you discover that a thing is wrong cheaply; do not carry a doubt forward to `/review`.

**5. Prove in the running app.** Green unit tests are NOT proof — they confirm your code matches your assumptions, not reality. This is the repeatable, recorded version of what step 4 found by hand.
- Invoke exactly — **Invoke skill `verify-app`** with args `--yes --journey <path-to-verification-plan.md> --lenses functional,error-state,accessibility[,visual,responsive]`. A single-screenshot pass is forbidden — the journey must execute.
- **Desktop UI:** a stage that changes a desktop UI (`_shared/platform-detection.md`) is proved by `verify-app` dispatching to `verify-desktop`, and `design-verify` (→ `design-verify-desktop`) is **mandatory**: the UI must match the mockups in the repo's screens manifest, and any unexpected mismatch is stage-blocking.
- **Baseline check:** if `$AW_DIR/design/screens.json` baselines cover any of this stage's screens, `/design-verify` is **mandatory**; a FAIL diff (>10%) is stage-blocking.
- **Evidence pack (per `_shared/evidence-pack.md`):** verify-* writes `verification/<run-id>/pack.json` + report. Write `$STAGE_DIR/evidence.md` — verdict line, journey table, mockup diff %, each cross-check as its recorded command **with raw output side-by-side**, a pointer to the `<run-id>` dir, and a link to `manual-test.md`.
- `pack.json` verdict FAIL, or proof not observed → fix and return to step 3. **REQUIRED:** superpowers:verification-before-completion.

**6. Gather UI evidence — when the spec calls for it.** Decide from the spec and the diff, and write the decision (with its reason) in `evidence.md`:
- **Run `/ui-evidence`** when the stage changes what a user sees or does in a web app (routes, pages, components, forms, flows) and the app runs on the local stack. Follow that skill: doctor the stack, plan with `qa-runner`, run headless, lint, check DB provenance. Add the run's `summary.json` and its screenshots to `$STAGE_DIR` and reference them from `evidence.md`. For a spec with design mockups or Figma frames, use its design-parity mode (`parity`) as well.
- **Desktop UI stages:** `/ui-evidence`'s desktop mode is the evidence (`desktop-run`, or the workflow engine's `workflow.evidence-desktop` recipe when present): add its `summary.json` and each screen's `side-by-side.png` to `$STAGE_DIR` and reference them from `evidence.md`. The pack is judged against the mockups; a FAIL goes back to step 3.
- **Skip it** (and say why in `evidence.md`) for backend-only, API, CLI, data or infra stages, docs, iOS stages (step 5's `verify-ios` journey and screenshots are the evidence), and anything that cannot run on the local stack. Never run it against dev or prod.
- `/ui-evidence`'s publish step posts to Linear and the PR, so it waits for step 7: gather and review the evidence files now, publish ask-first once the draft PR exists. A failed or broken step goes back to step 3.

**7. Open a draft PR.** Only a stage with `manual-test.md`, a non-FAIL pack and the step 6 decision recorded may open one. Push the stage branch to the recorded remote, then `gh pr create --draft` with the body per `_shared/pr-body.md` (`## Evidence` embeds the evidence.md text; `--attach-images` defaults on for user-facing stages). The review loop needs an open PR — draft first, ready last. If step 6 gathered UI evidence, offer `/ui-evidence`'s ask-first publish now: its screenshots go on the PR as a comment (`gh-publish`, then `gh pr comment`; seeded data only).

**8. Review loop to zero blocking (cap 5).** `/review` → `/postReview` → `/addressReview` on the blocking findings → re-run `/review`. Repeat while a blocking finding remains. Review with every severity visible (`--all`) so you classify each finding yourself rather than trust a filter or a label; resolve the non-blocking ones in writing (below). After 5 iterations with a blocking finding still open: stop, report the oscillating findings verbatim, ask the user. The loop reviews code that already works; if a finding exposes that the behavior itself is wrong (not a style or robustness issue), stop the loop, return to step 3, and redo steps 4–6 before resuming it.

**9. Re-check, gates → ready → STOP.** The review loop edits code after the proof was captured. If its fixes changed non-test source, re-run the stage's step 5 journey once against the final code (same verification plan, no new plan) and update `evidence.md`; a FAIL returns to step 3 and the loop starts over. Then confirm all of: tests + CI green; latest `/review` = zero blocking findings and the rest resolved in writing; `test -s "$STAGE_DIR/evidence.md"` and `test -s "$STAGE_DIR/manual-test.md"`; pack verdict not FAIL on the final code. Then `gh pr ready` and **STOP for human merge approval — never self-merge**. Emit the next-stage synopsis from stages.md, update stage status, and save the session handoff; after the human merges, start the next stage at step 1 (honoring the autonomy contract).

## The review loop to zero

Zero blocking findings is a hard gate, not a target. The only way to *know* you are there is that **`/review` itself reported no blocking finding on its most recent run** after your fixes. Self-certifying ("I fixed the three it found, so it must be clean") does not count: fixes can introduce new findings, and reviewers see the delta you cannot.

- The loop starts only after steps 4–6 are done. `/review` is not a way to find out whether the feature works, and a clean review never substitutes for a hand test or an evidence pack.
- After fixing a blocking finding you **must** re-run `/review`. No exceptions for "trivial" deltas.
- Every blocking finding is **fixed** on this PR, not deferred. A blocking finding you believe is wrong is still resolved explicitly: reply on the thread with the technical reason, mark it resolved in `~/.agentic-workflow/<repo-slug>/reviews/<pr>.json`, then re-run `/review`.
- A blocking finding that is a bug is fixed prevention-first: ask the question in `$HOME/.agentic-workflow/toolkit/skills/_shared/prevent-the-class.md` (class, invariant, other instances, smallest mechanism) before choosing the fix, and carry its handoff block into the fix's reply.
- Every non-blocking finding is resolved too, but in writing: reply on the thread (accepted as a follow-up, or why not), mark it resolved in the state file. Fold in a fix only when it is a trivial edit (a one-line doc or name fix). Non-blocking findings alone never trigger another review run.
- The loop ends ONLY when a `/review` run returns zero blocking findings — or the cap of 5 triggers report-and-ask.

### Blocking or not

Classify each finding by what it does, not by its label; reviewers mislabel in both directions.

- **Blocking:** every `issue`, and any finding whatever its label whose substance is data loss or destructive behaviour, wrong or broken behaviour on a path users take, a security or privacy hole, a failing check, or a false claim in a doc people rely on (AGENTS.md, rules, planning docs, CHANGELOG).
- **Non-blocking:** other suggestions and nits: style, naming, wording, extra tests, robustness on a path no one takes (a hypothetical config, a case that ends in at most a warning), follow-up ideas.
- Unsure: treat it as blocking.

### Rationalizations and red flags (all FALSE — each means: return to the loop)

| Excuse / red flag | Reality |
|-------------------|---------|
| "Re-running review on a trivial delta is theater" / about to skip the re-run after a blocking fix | The delta can add findings; only a clean `/review` run proves zero blocking. Re-run. |
| "It's labeled a suggestion, so it's non-blocking" | Classify by substance: a "suggestion" that deletes data or breaks a real path is blocking. |
| "Only nits are left, I'll just ignore them" | Non-blocking still gets a written resolution on the thread and in the state file. |
| "Tests are green, so it works" / "done" on unit-test evidence alone | Tests confirm assumptions, not reality. Prove it in the app and capture the pack. |
| "I made a deliberate call to stop" / "it's late, the user is waiting" | The stop condition is `/review` returning zero blocking findings (or the cap-5 ask), not your judgment or the clock. |
| "The reviewer is wrong, so I can ignore it" | Resolve in writing on the thread + state file, then re-run. Never silently ignore. |
| "Open the PR and let `/review` tell me if it works" / skipping the hand test because the unit tests pass | Function comes first. Do steps 4–6, then review (step 8). |
| "UI evidence is extra, the pack is enough" / running `/ui-evidence` for a backend-only stage | Decide from the spec (step 6) and record the reason either way. |
| Writing the verification plan after implementing / marking ready with FAIL or missing evidence | The plan predates code (step 2 gate); non-FAIL evidence is a ready gate (step 9). |

## Composition (per stage)

| Stage | Use |
|-------|-----|
| Isolate / Plan / Implement | superpowers:using-git-worktrees · writing-plans · test-driven-development |
| Test by hand | the running app via the browser tools / simulator / CLI → `manual-test.md` |
| Prove | verify-app (+ design-verify when screens.json baselines match, always for a desktop UI) + superpowers:verification-before-completion |
| UI evidence | `/ui-evidence` (+ its `parity` mode for design-backed specs) when the stage changes the web UI, its desktop mode when it changes a desktop UI; skip with a recorded reason otherwise |
| Review loop (last) | draft PR → `/review` → `/postReview` → `/addressReview` on blocking findings, looped to zero blocking |
| Ship | re-check → gates → `gh pr ready`; or `/shipRelease --no-deploy` for an already-proven, review-clean stage |

## Common mistakes

- **Bundling phases into one PR.** Each stage is its own proven, review-clean PR.
- **Reviewing before proving.** Running the `/review` loop on a stage nobody has used by hand or evidenced. Hand test, pack and UI evidence (steps 4–6) come first; the loop is last.
- **Opening the PR ready, or after the review loop.** `/review` needs an open PR: draft at step 7, ready only at step 9.
- **Leaving the proof stale.** A review-loop fix that changes source after the evidence was captured needs the step 9 re-check.
- **Self-merging.** The harness stops at the human merge gate.

## Next steps

- `/landAndDeploy` — after a human approves and merges the stage PR, poll for merge then deploy and smoke it (never self-merge; this skill stops at the human gate)
- `/shipRelease` — the single-command gate+PR path for a stage that is already proven and review-clean
- `/weeklyRetro` — once all stages of the epic have shipped, capture what shipped and what slipped
