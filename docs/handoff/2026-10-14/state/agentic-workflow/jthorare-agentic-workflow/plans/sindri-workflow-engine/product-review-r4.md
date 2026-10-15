# Product Review: Sindri workflow engine (revision 4)

_Reviewed by `/productReview` on 2026-10-11 | Mode: growth (slim) | Pass 4 of at most 5_

Scope: (1) pass-3 product findings (product-review-r3.md #1–#6, consolidated G7/G4/G17) checked against revision 4;
(2) problems that revision 4 introduces (§5.8 resume actions, the new §3.8 rows, claim identity by item, drift
re-approval, and the owner/agent signal split). Resolved items and §13 written answers are not raised again unless
they are wrong.

**Pass-3 status:**
- **#1 (HIGH, drift and revoke dead end): resolved.** Revoke is cancel-only and says so consistently (§3.8, P6
  signal). Drift is fixed by re-approving the same hash with new pins (§4.1, W11, SND-RECIPE-006), then
  `answer --resume` (§3.8, §5.8). The claim identity is now the item (§5.4), so a new attempt counts its claim as
  `created`. The two tests I recommended aren't in §10. `Answer_<reason>_runs_<resume action>` covers the drift
  resume, but nothing covers the claim across attempts (concern 5).
- **#2 (MEDIUM, child wait at human pace): resolved.** `running` now has a 14d timeout, and rows aimed at a live
  frame never expire (§5.7).
- **#3 (MEDIUM, `step.checks` self-reported): resolved by written answer.** The worker-recipes design owns it, and
  outcomes are labeled `via: desk`. That's the deferral I offered, so I accept it.
- **#4 (LOW, scaffolded evals): partly resolved.** §13 says the approve preview marks `scaffold only`. But §4.2's
  preview list and §9.2's format don't define the marker, so nothing tells approve which case was scaffolded
  (concern 6).
- **#5 (LOW, activation cue): resolved.** An instance's first desk task interrupts once (§6.3).
- **#6 (LOW, P7 reused `T-5`): resolved.** P7 now uses `T-7`. Revision 4 makes the same kind of mistake again in
  P5 (concern 4).

## Verdict: SHIP

No CRITICAL or HIGH, so the rule gives SHIP. All pass-3 HIGHs that came from product are fixed. What's left is two
MEDIUMs from the new mechanisms. The item-keyed claim no longer stops two live attempts of the same item (#1). The
drift and revoke model covers block pins but not child pins, so changing `workflow.step` leaves `workflow.item` in
an undefined state with no `--stale` signal (#2). A third MEDIUM is the drift recovery cost at 10x (#3). The rest
are doc fixes. None of them would make a phase signal fail. Make the fixes inline. A fifth pass isn't needed for
product.

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Lever (a), "a new workflow is a config change", is intact, and drift no longer strands instances. One gap: when a child recipe changes, its parents get no stale or drift path (#2). Lever (b), generated recipes: the drift re-approval goes through `recipe approve`, but W9 approves generated recipes only through the freeze, so a drifted generated recipe has no stated re-approval path. That's out of P1–P7 scope (no generator yet), so it's noted here and not raised as a concern |
| Activation funnel | start → first desk task (now an interrupting cue) → `complete` ×3 → `done` works. The restart funnel is fixed: cancel → `--attempt next` → claim `created`. The new hole is the reverse case: `--attempt next` while the earlier attempt is still live also gets `created`, so the human now has two parallel desk task sets for one item (#1) |
| 10x usage | Every block `Version` bump parks every live instance of every recipe that pins the block. Each park interrupts (§6.3), and each one needs its own `answer --resume` after one re-approval per recipe. At 10x instances, that's N interrupts and N manual resumes for a routine toolkit upgrade (#3) |
| Retention hooks | Every §3.8 park now has an answer that does something (§5.8), and §11 measures park-to-answer latency. Two resume actions are vaguely worded: `reevaluate the trigger that parked it` for a startup drift wake, and for `SND-WF-010` on an `active` Step. For a Step the right action is `rerun` or `rearm`, not re-firing the entering transition (#7) |
| Viral coefficient | N/A as acquisition: this is a single-operator toolkit. Approvals travel with the host handover. A host on a newer build now parks with drift and recovers through re-approval, where before it hit a dead end |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | MEDIUM | **Keying the claim by item removes its exclusion.** `effect.claim.ref` counts a ref that names the item's identity as `created` (§5.4, §12). The ref name is `sindri/{user}/{item}`, so for the same user `claimExists` can only fire on a hand-made ref. Meanwhile §6.1 lets `start --attempt next` run while the earlier attempt is still live (parked, taken over, or in a desk task), and both attempts get `created`. Fixing the restart (pass-3 #1b) traded away the only thing the claim guarded | Refuse `--attempt next` (`SND-WF-003`, with the fix "cancel `<address>` first") while an earlier attempt of the same `(recipe, item)` isn't terminal. Or keep the item identity, but have the existence check read the attempt recorded in the ref and count it as `created` only when that attempt is terminal. Add the eval `claim-after-cancelled-attempt` and the test `Attempt_next_refused_while_an_attempt_is_live` |
| 2 | MEDIUM | **Child pins have no lifecycle.** W11 pins children's hashes. The §3.8 drift row says "A pinned block **or child** changed in this build", but `recipe.drifted` is written only when "the build's descriptor hashes differ" (§5.4), and children are registered recipes, not part of the build. `--stale` likewise compares pins "against the build". So: (a) approving `workflow.step` v2 neither flags `workflow.item` nor says whether new children run the pinned v1 or the latest approved version; (b) revoking `workflow.step` v1 strands `workflow.item`. Its live child parks revoked (cancel only, so the parent ends `dropped`), every new attempt pins a revoked child, and nothing tells the human that re-approving `workflow.item` (same hash, new child pin) is the fix | State that a child runs its parent's pinned hash. Extend `--stale` (and `recipe.drifted`) to "a child pin is revoked or is no longer the latest approved version of that id". Have the `revoke` preview list the parent recipes it strands, next to the instances it parks. Add `Revoked_child_pin_marks_parent_stale` |
| 3 | MEDIUM | **Drift recovery costs O(instances) at 10x.** A block `Version` bump, which the review rule requires for any behaviour change, parks every live instance of each affected recipe at startup. Each park sends an interrupting notice and needs its own `answer --resume` after the one typed re-approval. Revoke previews the instances it parks, but the drift re-approval doesn't offer to resume the ones it unblocks | Have the drift re-approval preview list the instances parked on that recipe's drift and resume them as part of the same typed approval (one decision, N `resume` triggers keyed `reapproval:{seq}:{instance}`). Collapse drift parks into one notice per recipe ("3 instances of workflow.step parked: gate.x changed; re-approve") |
| 4 | LOW | **P5's last signal step can target the wrong instance.** The second instance's item isn't named. If it is `T-2 --attempt next`, then the bare `T-2` in `answer T-2 --resume` resolves to the latest attempt, which is the taken-over one (§6.1), not the timed-out first one | Give the second instance its own item (`T-8`), or use `T-2/dev-timeout/1` in the answer step |
| 5 | LOW | **The pass-3 tests are missing, and one P4 signal step is unlabeled.** §10 has no test of the claim across attempts. P4 step 2 (`complete` ×3) has no **(owner)**/**(agent)** tag. It has to be owner, because `complete` is a `human` verb and the Prover is a `session` | Add the tests from concern 1. Tag P4 step 2 **(owner)** |
| 6 | LOW | **The "scaffold only" marker isn't specified.** §13 promises it, but `--scaffold` (§9.2) writes nothing that distinguishes a scaffolded case, and §4.2's approve preview doesn't list it | `--scaffold` writes `"scaffolded": true` on line 1, which an edit removes. The §4.2 preview shows `evals: n authored, m scaffold only` |
| 7 | LOW | **Two resume actions are underspecified.** `reevaluate the trigger that parked it` (§5.8) is unclear for a drift park that came from the startup `recipe.drifted` wake (re-evaluating the wake does nothing beyond the pop). It is also unclear for `SND-WF-010` on an `active` Step, where the parking trigger is the entering transition, so re-evaluating it would re-run its gates and effects | Split the row. For a park from a wake, or when the frame has an open run: `rearm` (keep the desk task). For a Step with no runtime: `rerun` (start the Step). Use `reevaluate` only when a withheld transition is stored |
| 8 | LOW | **§13 and the status line are stale.** They say "the owner capped the loop at three passes, so revision 4 isn't reviewed again", but this is pass 4 | Update the status line and the §13 preamble, and add the pass-4 ids when revision 5 lands |

verdict: PASS
