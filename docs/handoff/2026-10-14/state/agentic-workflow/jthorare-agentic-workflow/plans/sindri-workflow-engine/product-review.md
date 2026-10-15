# Product Review: Sindri workflow engine: recipes, blocks, validator and engine

_Reviewed by `/productReview` on 2026-10-10 | Mode: growth_

Scope note: the one-engine decision (A §17.28, §17.29, Q6/Q7) is settled and not reopened here. "Users" for growth
purposes are the owner (sole human operator and recipe approver), the recipe generator (§17.7), and the agents
whose work runs as instances. "Growth" means more of Sindri's and the skills' machines running as recipes, and the
owner relying on the engine instead of the TS `sindri/` and the standalone skill state. Threat model is honest
agent mistakes, not adversarial agents; no finding below depends on an adversarial agent.

## Verdict: ITERATE

One HIGH concern (#1) drives the verdict. After all seven phases, the engine has never done any useful work:
every Step runtime fails closed until `ILauncher` exists, and nothing in these phases builds `ILauncher`. So the
final signal (Phase 7) is an item that parks with `SND-WF-010`. The engine is sound, but its first value moment
falls outside this plan. MEDIUM concerns #2 (authoring recipes and evals has no tooling) and #3 (no proof that
schema v1 can express the recipes it exists for) are the main growth levers. They don't block approval, but they
are cheap to fix now and expensive to fix after recipes exist.

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Two levers are built into the product: (a) "a new workflow is a config change" (recipes plus a reusable block catalog), and (b) generated recipes, approved by the packet freeze (W9). Lever (a) is throttled by authoring friction: evals are required to approve, are written by hand as JSONL event logs, and nothing scaffolds a recipe or a block (#2). Lever (b) is outside the phases, which is correct. The doc doesn't check that schema v1 can express the known next recipes, bugfix first (#3) |
| Activation funnel | The funnel is write YAML → `recipe validate` (Phase 1, good: named rules with fixes) → `register` → `approve` (TTY, needs evals) → `workflow start` → a useful outcome. The biggest drop-off is the last step: no phase reaches an outcome other than `parked(needs-answer, SND-WF-010)` (#1). A second, smaller drop is at approve, where evals are required and can't be generated from a run (#2) |
| 10x usage | At 10x recipes and blocks, the weak point is block evolution. `BlockDescriptor.Version` exists, but recipe hashes and approvals don't pin block versions. A block that changes its outcomes, outputs or parameters silently invalidates approved recipes and in-flight instances whose hash is unchanged (#4). At 10x instances, the human's queue of parks has no list or aggregate view (#5). Store and commit design (W12–W16) supports the volume. SQLite with one writer under the lease is fine at this scale |
| Retention hooks | The natural cadence is per park and per escalation: each one is a question for the human, and the `next` text is the hook. The design produces these events well. But the only read surfaces are `workflow status <item>` and `sindri ledger --item`. There is no "what needs me" list and no notification on park in the phases, and the S1 banner ("3 items need you · 1 parked") needs exactly that (#5) |
| Viral coefficient | Mostly N/A: this is a single-operator toolkit, with no other users to expose. The inner equivalent is reuse: each block written once is used by every later recipe, and the catalog compounds. One spread surface is real: Q3 (the factory host can move between the laptop and the Linux box) and Q5 (ring-1 workplace repos). Recipe approvals live in one host's `ledger.db`, and the doc doesn't say whether they move with the host (#7) |

## Strengths

1. **Fail closed is consistent and specified per block kind (W5).** An unavailable gate refuses the transition. An unavailable Step parks the instance at once instead of waiting out a timeout. For an operator, this is the difference between "it's broken, here's why" and a silent stall, and it is the best retention property a harness can have.
2. **The validator is a real authoring aid, not only a guard.** Each failure prints one line: rule id, what failed, the path, and the fix (§7). It is pure Core on descriptors alone, so it runs in the unit lane. The every-path wiring check (W2) turns latent runtime refusals into errors at authoring time. Phase 1 alone has standalone value.
3. **Phasing is honest about S1 dependencies.** The S1-D1…D7 table names each missing port and the phase that needs it. That lets Phase 1–3 proceed while S1 is still in progress. Each phase has a concrete, observable signal.
4. **Operator-friendly time semantics.** The sleep shift (W14), a paused clock during takeover versus a fresh timeout after a park (W15), and explicit, audited `grant: n` (W7) fit a laptop-hosted, interrupt-driven single operator.
5. **Approval provenance comes from the path a recipe entered by, not from a field (W8–W11).** This is the right size under the stated threat model, and it keeps generated recipes cheap to approve, since the packet freeze is the approval.

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | HIGH | **There is no value moment inside the plan.** `step.produce`, `step.checks` and `step.verify` fail closed until `ILauncher` exists (W18, §10 "Honest first signal"). `ILauncher` is "planned" and has no phase. After Phase 7, every instance ends in `parked(needs-answer)`, so the owner has a running engine that can't finish anything. That leaves the TS `sindri/` and the standalone skill state as the only working path, with no date for when the engine overtakes them. | Add one Step runtime that needs no launcher, so that some recipe reaches a real terminal `outcome` within the phases. Candidates: a deterministic Step over an existing finished port (`aw-packets validate`, `aw-readiness check`, or `bugfix-state`'s `run-test` as an evidence provider), or a `step.desk` Step that parks with `needs-answer` *by design* and completes on the human's `command.answer`. Alternatively, name `ILauncher` as Phase 8 with its own signal, and state the date or condition when the engine replaces a TS path. |
| 2 | MEDIUM | **Authoring a recipe or block has no tooling, and eval files are hand-written.** `evals` are required to approve (§3.1). They are replayed event logs (JSONL), and the only way to get one is to write it by hand. A new block means C# code, a descriptor, `Add…()` registration and a `BlockContract` binding, with no scaffold. This throttles the "config change" lever more than any engine property does. | Add `sindri recipe eval --record <instance>`, which writes an instance's ledger slice as an eval case with its observed states and outcome (Phase 4, where the ledger read lands). Add a `/new-block` scaffold that mirrors `/new-slice`: descriptor, kind stub, registration and the contract-test binding, red first (Phase 2, alongside the real descriptors). Optionally add `sindri recipe new <id>`, which writes a minimal valid recipe and an empty eval. |
| 3 | MEDIUM | **Schema v1 is closed (W3), but the doc never shows it can express the recipes it exists for.** Only `workflow.item` and `workflow.step` are written. The bugfix worker (mode B, judge-once, the attempts loop), the direction check (leg timeouts, `maxExchanges`) and ship/stack (§17.28.6 steps 2–4) are the reason for one engine. If any of them needs something v1 lacks, a schema v2 and a migration land before the second real recipe. | In Phase 1, add **valid** fixtures (validate only, not run) for `workflow.bugfix` and the direction check, using fake descriptors that mirror `bugfix-state`'s gates. If one can't be written, record the gap in §3.9 now. This costs a few YAML files and fake descriptors. |
| 4 | MEDIUM | **Block evolution has no compatibility rule.** A recipe's hash and approval cover its text, not the descriptors it names. Changing `effect.claim.ref`'s declared outcomes, or a gate's rung or parameters, changes what an approved recipe means without changing its hash. Wiring (W2) and `RCP-WHEN-001` were checked against the old descriptor. A raised rung or new capability flag changes a recipe's capability footprint with no re-approval. This matters as soon as there are several recipes. | Record each named block's `(id, Version)` with the registration (or fold them into an "effective hash"). Re-run the validator at `StartInstance` against the current catalog, and refuse with `SND-RECIPE-001` on drift. A descriptor change that adds capability flags makes the recipe unapproved until it is re-approved. In-flight instances keep the descriptor versions they started with, or park with `spec-conflict`. |
| 5 | MEDIUM | **There is no "what needs me" surface.** Parks and escalations are the human's work queue and the main reason to come back, but the phases expose only per-item `status` and a ledger read. S1's startup banner and the UI ("3 items need you · 1 parked") need a cross-instance query. Nothing notifies the human when an instance parks. | Add `GetInstances` with filters (state, park reason, recipe) and `sindri workflow list [--parked\|--needs-me]` to Phase 4. Make `workflow.transition` into `parked` or `escalated` emit a notification command through the outbox in Phase 6, so the retention hook arrives instead of being polled. |
| 6 | LOW | **S1-D3's status is out of date.** The S1 branch already migrates `outbox_commands` into `ledger.db` (`LedgerDatabase` "events, lease, outbox_commands"), with Kernel's `SqliteOutbox` and `OutboxTables`. What is missing is the dispatcher under the lease, with the approval and allowlist re-check. | Change the row to "table and `IOutbox` exist; dispatcher with dispatch-time re-check missing", and size Phase 6 accordingly. It may shrink, or the dispatcher may move into Phase 6 itself. |
| 7 | LOW | **Recipe approvals are bound to one host.** `recipe_approvals` lives in the active host's `ledger.db`, while Q3 makes the factory host swappable and Q5 brings in workplace repos. The doc doesn't say whether moving hosts means re-registering and re-approving every recipe, and whether in-flight instances move too. | State the answer in §4. The simplest form is: approvals travel with the ledger, as the host lease does. Or say they're per host by design and `sindri recipe list` shows what isn't approved here. |
| 8 | LOW | **There is no measure of the engine's own adoption.** W6's outcome set makes per-recipe outcome metrics possible, but the plan defines no signal that the engine is working for the owner: instances finished, park-to-answer latency, refusals by rule. | Name two or three numbers that scorer or metrics (§17.2) will read from `workflow.*` events. Nothing needs building now; just name the event fields that let them be computed, which the transition payload mostly already carries. |

## Scope Suggestions

### Cut
- Nothing material. One thing could wait: `RCP-WHEN-002` (the shadowed-transition check) and the seeded random property runs in Phase 3 could move to a follow-up if Phase 3 runs long. Replaying the real recipes' evals covers the main property first.

### Keep
- **Phase 1 as a standalone deliverable.** A validator with named, fixable rules is useful on day one and de-risks the schema.
- **Fail-closed parking (W5) and the honest first signal.** Keep the behavior; add a recipe that ends in a real outcome next to it (#1).
- **`Commit` as the single port (W13) and the `fromVersion` idempotency key (W12).** These hold up at 10x and keep replay exact.
- **Approval by provenance (W9).** It is cheap for generated recipes and needs a desk action for hand-written ones, which is the right friction in the right place.

### Add
- A Step runtime that needs no launcher, or a `step.desk` human Step, so some recipe finishes inside the phases (#1).
- `recipe eval --record` and a `/new-block` scaffold (#2).
- Validate-only fixtures for `workflow.bugfix` and the direction check (#3).
- Pinned block versions, plus re-validation at start (#4).
- `workflow list --parked` and a notification on park (#5).

## Key Questions for the Team
1. Which phase, or which follow-up with a date or condition, delivers `ILauncher`? Until it lands, what is the first recipe that finishes with `outcome: resolved` on real work?
2. Can `workflow.bugfix`, as `bugfix-state` runs it today (mode B, judge-once, attempt caps, the `next` hints), be written in schema v1 without additions? If not, which v1 field is missing?
3. When a block's descriptor changes, what happens to approved recipes that name it, and to instances in flight on them?
4. Do recipe approvals move with the factory host (Q3), or are they per host?

## Recommended Next Action
Before `/specToProvenPR` takes the phases, amend the doc in three places. First, add a launcher-free Step runtime, or a `step.desk` human Step, plus one fixture recipe that finishes with a terminal outcome by Phase 4 (#1). Second, add `workflow.bugfix` and direction-check valid fixtures to Phase 1's file list (#3). Third, add `recipe eval --record` to Phase 4 and `workflow list --parked` to Phase 4 (#2, #5). Record the block-version rule (#4) as a §1 decision.

verdict: NEEDS_WORK
