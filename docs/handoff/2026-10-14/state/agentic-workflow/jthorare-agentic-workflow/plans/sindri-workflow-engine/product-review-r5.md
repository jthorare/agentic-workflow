# Product Review: Sindri workflow engine (revision 5)

_Reviewed by `/productReview` on 2026-10-11 | Mode: growth (slim) | Pass 5 of 5 (final)_

Scope: (1) pass-4 product findings (product-review-r4.md #1–#8) checked against revision 5; (2) problems that
revision 5 introduces (item-keyed claim with owner, `SND-WF-012`, child pins and `child-stale`, batch drift resume,
the split resume actions). Resolved items and §13 written answers are not raised again unless they are wrong.

**Pass-4 status:**
- **#1 (MEDIUM, item-keyed claim lost its exclusion): resolved.** The ref records its owner, `created` only for the
  owner or once the owner's attempt has ended, and `--attempt next` is refused while an earlier attempt is live
  (§5.4, `SND-WF-012`). Both tests are in §10 (`Next_attempt_refused_while_earlier_is_live`,
  `Claim_owned_by_ended_attempt_reads_created`).
- **#2 (MEDIUM, child pins had no lifecycle): mostly resolved.** A child runs its parent's pinned hash, `child-stale`
  is listed by `--stale`, and creating a revoked child parks the parent (§4.1). The parent's recovery path is still
  mis-signposted (concern 1). The test I asked for (`Revoked_child_pin_marks_parent_stale`) is not in §10 (concern 2).
- **#3 (MEDIUM, drift recovery O(instances)): resolved in design.** The drift approval previews and resumes every
  instance parked on that drift, with one notice per recipe (§4.1, §3.8). It has no test, no owning unit and no
  signal (concern 2).
- **#4 (LOW, P5 address): resolved.** `T-8` and `T-2/dev-timeout/1`.
- **#5 (LOW, tests and P4 tag): resolved.** Every P4 step is tagged.
- **#6 (LOW, scaffold marker): resolved** (`"scaffold": true`, §9.2). The preview flags only the all-scaffold case
  (concern 4).
- **#7 (LOW, resume actions): resolved.** §5.8 splits drift (`rearm`, re-evaluating only a stored Step trigger) and
  `SND-WF-010` (`restart` / `rewait`).
- **#8 (LOW, stale status line): resolved.** The status line and §13 say pass 5 is the last.

## Verdict: SHIP

No CRITICAL or HIGH concern, so the rule gives SHIP. Every pass-4 product finding is fixed or fixed in design. What
remains is two MEDIUMs on the new child-pin and drift mechanisms: a parent parked on a revoked child is told to do
the one thing that parks it again (#1), and the batch drift resume, the path every routine block upgrade takes, has
no test, owning unit or signal (#2). Neither makes a phase signal fail; fix both inline before `/specToProvenPR`.

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Lever (a), "a new workflow is a config change", holds. Child versions now roll forward by re-approving the parent, which is clean. The revoked-child case still has a misleading way out (#1) |
| Activation funnel | start → first desk task (interrupting cue) → `complete` ×3 → `done` is proven in P4. The restart funnel (cancel → `--attempt next` → `created`) and its guard (`SND-WF-012`) are consistent and tested |
| 10x usage | One typed drift approval now resumes N instances with one notice per recipe, so a block `Version` bump costs O(recipes), not O(instances). But this path is unowned and unproven (#2) |
| Retention hooks | Every §3.8 park has a resume action that does something, and §11 measures park-to-answer latency. The revoked-child park is the one exception: its text sends the human round the loop (#1) |
| Viral coefficient | N/A as acquisition: a single-operator toolkit. Approvals travel with the host handover (written answer P7) |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | MEDIUM | **A parent parked on a revoked child is told to do the thing that parks it again.** §4.1 parks the parent with `spec-conflict` (revoked) when its pinned child hash is revoked. The §3.8 text for that park is "This recipe was revoked. Cancel, then start a new attempt from an approved version". But the parent recipe isn't revoked, and its approval still pins the revoked child. So `cancel` → `--attempt next` creates the child from the same revoked hash and parks again. The real fix is to re-approve the parent, which pins the latest approved child (§4.1), and nothing says so. If no approved child version exists, re-approval should say that instead | Give the child-revoked case its own row and text: "{child}@{hash}, pinned by {recipe}, was revoked. Approve a child version, re-approve {recipe} (`sindri recipe approve {recipe}`), then cancel and start `--attempt next`." Either let that parent re-approval resume the parked parents the way a drift approval does, or say they're cancel-only. Add `Revoked_child_pin_parks_parent_with_reapprove_fix` |
| 2 | MEDIUM | **The batch drift resume (H12) has no test, no owning unit and no signal.** It is the path every routine block `Version` bump takes. P2(c) owns "approve (including drift re-approval)", but instances, frames and resume actions only exist from P3/P4, and no P4 unit names the batch resume. §10's revoke/drift row covers parking (`Pin_drift_parks_on_next_commit`) but not the resume, the per-recipe notice, or `child-stale` | Assign it explicitly: P4(b) "drift approval resumes the listed instances (`reapproval:{seq}:{instance}`)". Add `Drift_approval_resumes_every_listed_instance`, `Drift_parks_send_one_notice_per_recipe` and `Approving_a_newer_child_marks_parent_child_stale` to §10. If a dev build can bump a dev block's version, add one owner step to P4 or P6 that proves it |
| 3 | LOW | **Child drift wording survives from revision 4.** §3.8's drift row ("A pinned block or child changed in this build") and `SND-RECIPE-006` ("A pinned block or child changed since approval") still treat a child change as drift. §4.1 now says a child change only marks the parent `child-stale` and never parks it. Implemented literally, approving `workflow.step` v2 would refuse every live `workflow.item` commit under RCP-CAP-003 | Drop "or child" from the §3.8 drift row and from `SND-RECIPE-006`. State in RCP-CAP-003 that child pins are checked only for revocation, when the child is created |
| 4 | LOW | **Two leftovers in the text.** (a) §12's `effect.claim.ref` row still says "a ref that already names this instance's effect identity counts as `created`". §5.4 now keys the identity by item, with the owner in the ref. (b) The approve preview flags `scaffold only` only when *every* eval is scaffolded, so one authored case plus two scaffolds looks fully authored | (a) Point the §12 row at §5.4's rule. (b) Show `evals: n authored, m scaffold` |
| 5 | LOW | **§13 cites a file that doesn't exist.** It points to `consolidated-review-r4.md`, but the plans directory has only `consolidated-review.md` to `-r3.md` | Write the pass-4 consolidated review, or cite the per-lens `-r4.md` files instead |

verdict: PASS
