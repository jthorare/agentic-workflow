# Product Review: Sindri workflow engine (revision 3)

_Reviewed by `/productReview` on 2026-10-10 | Mode: growth (slim) | Pass 3 of 3 (final)_

Scope: the pass-2 product findings (product-review-r2.md #1–#9, consolidated E11 and E22) checked against revision 3,
plus new problems that revision 3 introduces. §17.28 and §17.29 are settled. The threat model covers honest mistakes.
§13 written answers are not re-raised unless they are wrong or contradict the spec.

**Pass-2 status:**
- **Resolved:**
  - #1, desk ergonomics: W20's desk floor (`workflow.deskTimeout`, 2d, always `park`, never `retry`), split
    notification classes (§6.3), `--needs-me` split into Decisions and Tasks, and the `inputs` line in §6.4.
  - #3, fail-closed: W5 `deskCapable`, with the test `Step_without_runtime_and_not_desk_capable_parks`.
  - #5, eval bootstrap: `--scaffold`.
  - #6, bugfix mode B: the §13 answer that mode B waits for v2 is accepted.
  - #7: the P7 integrator files the `/new-block` issue.
  - #8: a test catalog and the `step.bugfix.*` grammar.
  - #9: §11 adds park-to-answer latency and time to outcome.
- **Partly resolved:** #2. `ILauncher` now has an owner (the next design), but `step.checks` is still self-reported.
  See concern 3.
- **Not resolved, and now contradictory:** #4, pin-drift recovery. See concern 1.

## Verdict: ITERATE

One HIGH (#1) sets the verdict. When a block changes, or a recipe is revoked, the documented way back doesn't work.
§3.8 lets a `spec-conflict` instance only cancel. Its text says to "approve a new version and start a new attempt",
which contradicts SND-RECIPE-006's fix ("re-approve") and the §13 answer. For `workflow.item`, a new attempt can't
succeed either: attempt 1's claim ref is still there, so attempt 2 gets `claimExists` and ends `dropped: unresolved`.
Every block `Version` bump, which the review rule requires for any behaviour change, would leave every in-flight item
with no way forward. The two MEDIUMs are a smaller version of pass-2's desk-timeout problem one level up (#2) and the
unchanged "the engine does no work" gap (#3). The LOWs are small doc edits. There is no CRITICAL.

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Lever (a), "a new workflow is a config change", is now complete: `--scaffold` bootstraps evals, pins exist and `--stale` shows drift. One weakness: a scaffolded happy-path case passes by construction, so the approval eval gate checks nothing for a new recipe (#4). Lever (b), generated recipes, is unchanged and sound (`RCP-CAP-005..007`) |
| Activation funnel | serve → register → `--scaffold` → approve → `start` → desk task → `complete` ×3 → `done: resolved` works for `workflow.step`. For `workflow.item`, the first desk task shows up asynchronously after route, slot and claim. Its notice goes only into the daily digest, so the person who just ran `start` gets no cue (#5). The bigger drop-off is restart after a failure: cancel, revoke or drift, then a new attempt, ends in `dropped` (#1) |
| 10x usage | Notifications and `--needs-me` now grow with the number of decisions, not Steps (fixed). At 10x recipes and builds, drift is the scaling problem. A shared block's `Version` bump strands all in-flight instances of every recipe that names it, and the only way out is cancel (#1) |
| Retention hooks | The decision hook works. Interrupting notices go only to parks, escalations and idle takeovers, and §11 measures park-to-answer latency. The desk floor stops the human's own pace from parking a Step, but the parent's `running` child wait still has a 2d `park` timeout, so `workflow.item` parks while its child is doing normal desk work (#2) |
| Viral coefficient | N/A as acquisition: this is a single-operator toolkit. Approvals travel with the host handover (§13 P7). A host on a different build hits the drift dead end on arrival (#1) |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | HIGH | **Drift and revoke recovery is a dead end, and the doc contradicts itself.** (a) §3.8 gives a `spec-conflict` park (revoke or drift) only `cancel`, with "Approve a new version and start a new attempt". SND-RECIPE-006's fix says "Re-approve (`recipe list --stale`)", and the §13 answer suggests re-approval recovers the instance. When only the build changed, the recipe's hash is unchanged. "Approve a new version" would mean raising `version` on identical content, and the doc doesn't say whether re-approving the same `(id, version, hash)` with new pins is allowed. (b) A new attempt of `workflow.item` can't succeed. `effect.claim.ref` counts a ref as `created` only when it names *this instance's* effect identity, so attempt 2 finds attempt 1's `sindri/{user}/{item}` and takes `claimExists → dropped: unresolved`. Cancel doesn't release the ref (§3.8 and §5.7 are silent). So every cancelled, revoked or drifted item is stuck until someone deletes the ref by hand. Because behaviour changes bump `Version` by rule (§2, §13 S2-L-c), this happens on ordinary toolkit upgrades | Choose one model and state it in §3.8, §8 and §13. **Preferred:** for drift, re-approving the same hash with the current pins is allowed (a typed challenge that shows the descriptor diff). The drift park then accepts `answer` once the recipe is approved again, and the frame resumes. Revoke stays cancel-only. Also define the claim across attempts: either `cancel` (and the terminal `dropped`) releases the claim ref as an outbox effect, or the claim identity is `(item, recipe)` with the attempt recorded in the ref, so a new attempt of the same item counts as `created`. Add the eval `claim-after-cancelled-attempt` and the test `Drift_park_resumes_after_reapproval` |
| 2 | MEDIUM | **The parent's child wait times out at the human's pace.** `workflow.item.running` has `timeout: { after: 2d, then: park }`. Under W20 its child's three desk Steps can each take up to 2d, more with revisions. So the parent parks with `timeout` while the child is in normal desk work. That is the pass-2 #1 problem one level up, in the only real top-level recipe. It also leaves a gap: if the child finishes while the parent is parked, §5.7 holds the row for `eventHold` (24h) and then expires it, unless the "parent busy" row (pending with no limit) applies. The doc doesn't say which | Make a `child` wait's timeout count only while the child isn't in a desk task, or apply the desk floor to waits on a child whose current Step runs on the desk. Or give `running` a timeout above the child's worst case and say why. State in §5.7 that a child's terminal row is never expired while the parent's frame waits on that child, including while it is parked or taken over |
| 3 | MEDIUM | **The engine still does no work in any phase, and a deterministic Step is still self-reported** (pass-2 #2, partly resolved). §13 now names `ILauncher`'s owner. But `step.checks` ("Run the Step's deterministic checks") is still the desk, so `passed` is whatever the human types. A deterministic check needs no launcher: running a profile-declared command (`bugfix-state run-test`, `aw-packets validate`, `aw-readiness check`) and mapping its exit code fits the effect and dispatcher machinery P6 already builds. `via: desk` labels the outcome but doesn't verify it | Add a small non-desk runtime for `step.checks` in P6 or P7 (size S–M). It runs a profile-allowlisted command and reports `passed` or `failed` with `fact:findings` from its output. If you defer it, say so in §13 and name the phase or design that owns it. That gives the phases at least one transition the engine itself decides |
| 4 | LOW | **A scaffolded eval meets the approval gate trivially.** `--scaffold` writes the happy path (first matching transitions, first declared outcome), which passes by construction. So approve (`SND-RECIPE-008`) passes for any recipe once it has been scaffolded | Mark scaffolded cases (`"scaffolded": true` until edited), and have the approve preview show "evals: 1 scaffolded, 0 authored". Or require at least one case that reaches a park or a non-first outcome before approval |
| 5 | LOW | **No cue at activation.** Desk-task notices go only into the daily digest. After `workflow start workflow.item`, the child's first desk task appears seconds later, after route, slot and claim. The human sees `triaged` and nothing else | Make the first desk task of an instance the human just started an immediate (non-interrupting) notice, or let `start` take `--follow` until the first open task or decision. Have the S1 banner count Tasks |
| 6 | LOW | **P7's signal reuses `T-5` from P6.** If the prover keeps P6's state directory, the bare `answer T-5 --grant 1` has two roots (`dev-notify` and `item`) and gets `SND-WF-006` | Use a fresh item id (`T-7`) or the full address `T-5/item/1` in P7's signal, or say each phase's prover starts from a fresh `AW_STATE_DIR` |

verdict: NEEDS_WORK
