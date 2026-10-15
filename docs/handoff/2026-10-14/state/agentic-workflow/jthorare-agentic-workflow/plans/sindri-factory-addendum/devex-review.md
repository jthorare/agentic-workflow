# Plan Devex Review — sindri-factory-addendum (round 2)

**Plan:** docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md (revision 3, plus §17.20 and §17.21)
**Reviewed:** 2026-10-09
**Prior round:** round1/devex-review.md (4 HIGH, 10 MEDIUM, 3 LOW; verdict NEEDS_WORK)

## Summary
There is still no plan dir (`plan.md`, `engineering.md` or `TASKS.md`). As in round 1, the API surface was judged from §17.4–§17.21 and the rollout from §17.16. It was checked against `sindri/src/cli.ts`, `docs/sindri/README.md`, `docs/sindri/errors.md` and the readiness design (`2026-10-09-agent-readiness-design.md`). The following were treated as decided and not reviewed: the C# port, stacked epics, the swappable host, the ring order and the core tenet.

Revision 3 closes or mostly closes all four round-1 HIGHs:
- Packets now have `init`, `amend` and `validate --json`.
- Sandbox denials have their own `SND-SANDBOX` area, plus `session spec` and `why-denied`.
- Sindri mode has a detection signal (`SINDRI_MODE`) and a state-path shim.
- The skill lint has a spec.

The glossary, `schemaVersion`, and the lens command aliases close three round-1 MEDIUMs.

Two new HIGHs drive the verdict:
- **Micro-packets contradict invariant 13.** They take their write scope from triage, which is a model lens, and they freeze without a desk step. Invariant 13, the C1 resolution and §17.12 all say no model sets a capability field and that freezing is a desk action.
- **The C# port can't satisfy both documents.** §17.20 and §17.21 add `tenant`, `ring`, `actor`, `trace` and idempotency keys to every ledger record "during the C# port". The readiness cutover contract requires the C# ledger to keep the TS schemas byte-compatible under golden tests.

Most round-1 MEDIUMs that weren't about the new verbs are still open: unseeded error areas, the docs list, the `context-leak` record, unattended publishes, metric reclassification, and a worked example. The new P1 scheduler also has no "why is my Step waiting" view.

Time-to-first-success (first `sindri packet validate` passing on a real repo): about 30 minutes, down from about 75.

## Round-1 closure

| Round-1 finding | Status | Where |
|---|---|---|
| H1 No packet create or amend | Mostly closed. `packet diff` and the affected-task display are still missing (M1 below) | §17.19 l.700–701, §17.16 F0 |
| H2 Denial codes collide; no way to see why | Mostly closed. The command-denial code is ambiguous and there's no pre-freeze compile view (M2) | §17.5 l.259, §17.19 l.702–703 |
| H3 No compatibility contract for sindri mode | Partly closed. Who sets the env var, concurrent state, and regression tests are open (M3) | §17.19 l.704–705 |
| H4 Skill lint undefined | Partly closed. No budget number, no marker syntax, no command, no F step (M4) | §17.19 l.706–707 |
| M schemaVersion and JSON Schema | Closed | §17.4 l.203–205, §17.19 l.710 |
| M Lens command binding | Closed for binding. Selector semantics and the `check.type` enum are open; the §17.7 example is stale (L1) | §17.19 l.710–711 |
| M Effective rule and lens view | Partly closed (`rules explain`). No effective list, no `--step`, noun is inconsistent (M5) | §17.19 l.711 |
| M Unattended evidence `publish` | **Open** (M6) | §17.11 l.449 |
| M Worked example and quick start | **Open**, though softened by `init --from-scope` and F0.5 (M7) | — |
| M Handover preflight and rollback | Mostly closed (`--dry-run`, `--back`). Partial-failure codes are open (L2) | §17.19 l.658–659 |
| M New error areas unseeded | **Open**, and wider now (M8) | — |
| M `context-leak` record | **Open** (M9) | §17.1 l.99–100 |
| M Docs list | **Open** except the glossary (M10) | §17.19 l.712 |
| M Metric reclassification | **Open**, and wider: human-minutes has no source (M11) | §17.2, §17.19 l.715 |
| M Overloaded terms | Closed (glossary). One stale use remains (L3) | §17.19 l.712, §17.8 l.389 |
| L `/rule` and `attach` have no F step | **Open**, and wider (L4) | — |
| L Holdout location | Closed (sindri-private state, evaluator-only mount, canary) | §17.19 l.646 |
| L Adapter-authoring doc | **Open** (L5) | — |

## Findings by severity

### CRITICAL
None.

### HIGH
- **API surface ergonomics / consistency:** Micro-packets contradict the capability rules the rest of the plan relies on. §17.19 says an S-or-smaller tracker item gets an auto-generated packet with "write scope from triage", "frozen without a desk step". The conflicts are:
  - The triager is a model-run standing workflow (§17.9 l.402: Product lens plus the matching lens).
  - Write scope is a capability field that "no model output may set" (§17.19 l.628–631; invariant 13, l.93–96).
  - Freezing is a desk action (invariant 13, l.95–96; §17.12 l.485 and l.495–496; §17.19 l.684).

  §17.19 says it wins over earlier sections, but both rules are inside §17.19. An implementer can't tell whether the auto-freeze path is allowed, or what decides a micro-packet's write scope. This is also the main on-ramp for small work, so it is the path developers will hit first.
  - Evidence: plan l.708–709, "write scope from triage … frozen without a desk step"; l.630–631, "no model output may set them"; l.93–96, "Freezing and re-freezing are desk actions"; l.485, "Never … packet freezes".
  - Fix: Pick one rule and write it down. Recommended: the micro-packet's write scope is computed deterministically. It comes from the item's linked paths, symbols or route via the code index, or from an explicit `writeScope` the human typed on the ticket, and it is capped by a profile `microPacket.maxWriteScope` (for example ≤ N files, never a protected module). Triage may only *propose* it, and a proposal becomes a one-tap swipe item: "freeze micro-packet X, scope Y". Then amend invariant 13 to make the exception explicit: "a micro-packet whose scope is computed by code within the profile cap may freeze without the desk". Add a `SND-PACKET` code for "micro-packet scope exceeds cap → desk".

- **Backwards compatibility / migration:** The ledger and event seams that §17.20 and §17.21 schedule "during the C# port" break the port's own cutover contract. The readiness design freezes "the ledger and SQLite schemas" as golden tests run against TS and C# alike, and says the ledger and state files "keep their schemas and versions" so that each build reads what the other wrote (readiness l.95–96, l.104–105). The addendum schedules these for the same port:
  - `actor`, `tenant`, `ring` and trace fields "on every record";
  - an idempotency key on "every ledger event";
  - a transactional outbox and event envelope;
  - a tenant and ring key on "every row … from day one".

  A porting agent can't make goldens pass and add these columns in the same packet. Nothing says whether the goldens get re-recorded, whether TS writes defaults, or what the backfill is for existing rows.
  - Evidence: plan l.742–748, "Every row carries a tenant and ring key from day one"; l.779–781, "During the C# port (cheap seams): the event envelope and transactional outbox … `actor`/`tenant`/`ring`/trace fields on every record"; readiness l.95–96 and l.104–105.
  - Fix: Add a "seam migration" rule to §17.20. The cutover stays byte-compatible. Then the seams land as a **separate, versioned ledger migration** (ledger schema vN+1) after the package's cutover, as its own packet. Spell out:
    - new columns nullable or defaulted (`tenant='default'`, `ring` from the profile, `actor='system:legacy'`, idempotency key derived from `(kind, item, attempt, sha)` for old rows);
    - a backfill and its dry-run;
    - a golden re-record step with a reviewed diff;
    - what a TS reader does with vN+1 (refuse with `SND-LEDGER-0xx`, since TS is deleted at cutover).

    Alternatively, state that the TS ledger gains these columns first, so the goldens include them before the port starts.

### MEDIUM
- **M1 · API surface ergonomics:** Packet amendment has a verb but no shape. Nothing gives `packet amend`'s arguments, shows which tasks an amendment re-plans (§17.4 l.228–229), or shows the hash chain between freezes. `packet init --from-scope <map>` doesn't say what it fills in and what it leaves as TODO stubs. R1a ships a C# packet schema with only `validate` and `freeze` (readiness l.596), so ports built as packets before F0 have no `init` at all.
  - Evidence: plan l.700–701 (verbs named, no synopsis); l.551 F0; readiness l.469 and l.596.
  - Fix: Give synopses:
    - `packet amend <id> --task <ids> | --from <scope-expansion-item>` prints the affected tasks (the task and its dependents) and the new hash.
    - `packet log <id>` prints the freeze and amend hash chain.
    - `packet diff <hashA> <hashB>`.

    State that `init` copies the scope map's surfaces, read seeds and open questions, and leaves write scopes and acceptance examples as `TODO` that `validate` flags with a specific code. Either move `init` into R1a or say ports start from a checked-in example packet.

- **M2 · Error messages & observability:** Denial handling is still split, and you can't see it before freezing.
  - §17.5's sandbox table still says commands are enforced by "The gate (`SND-GATE-1xx`)" (l.253), and the next paragraph says a denied action returns `SND-SANDBOX-1xx` (l.259). A denied command could get either code.
  - `sindri why-denied <id>` doesn't say where `<id>` comes from (a denial id in the Step log? the ledger?).
  - `session spec <step>` hangs a step argument off the `session` noun.
  - There is no compile dry-run. A packet author sees a Step's compiled `write`, `commands` and `mcp` only after freezing, so every scope mismatch costs a desk amendment.
  - Evidence: plan l.253 vs l.259; l.702–703. Absent: searched "compile", "dry-run" in §17.4 and §17.5.
  - Fix:
    - Make `SND-SANDBOX-1xx` the only code a session sees for policy denials. Keep `SND-GATE` for gate infrastructure faults, and fix the l.253 cell.
    - Print the denial id in the refusal text (`SND-SANDBOX-104 [deny:01J…] … run: sindri why-denied 01J…`).
    - Use one noun: `sindri step spec|why-denied|context <step>`.
    - Add `sindri packet compile --dry-run [--task T] [--json]`, which prints each Step's session spec before freeze.

- **M3 · Backwards compatibility:** The sindri-mode contract has three gaps.
  1. Who sets `SINDRI_MODE=1`. It has to come from the launcher or the root-owned managed settings. A repo `.envrc` or an agent's `export` mustn't be able to set it or clear it (`SINDRI_MODE=0` would turn parks back into asks).
  2. The shim keeps state "readable from the same paths". §17.7 keys state by worktree, and P1 runs a packet's tasks in parallel. Two Steps on the same PR or bug would then share one `reviews/<pr>.json`.
  3. Nothing requires a regression test that interactive mode is unchanged. There is also still no `skills/_shared/sindri-mode.md`.
  - Evidence: plan l.704–705; l.366–370; l.729 (P1 parallel dispatch).
  - Fix: Write `skills/_shared/sindri-mode.md`, covering:
    - the variable is set only by the launcher, and the gate ignores a session-set value;
    - the shim *reads* legacy paths, and in sindri mode *writes* `<legacy-path>.d/<worktree-id>.json`, with `/postReview` and `/addressReview` merging per worktree;
    - the asks-become-parks mapping.

    Add a per-skill test that the interactive transcript is unchanged when `SINDRI_MODE` is unset.

- **M4 · Ramp-up / skill authoring:** The lint spec names its checks but not their parameters. It has:
  - no size budget value (lines or tokens);
  - no syntax for marking a sindri-mode path in skill prose, which the "no interactive asks on sindri-mode paths" check needs;
  - no command (`sindri skill lint`?);
  - no merge-gate entry;
  - no F step (sindri mode lands in F1, but skill-change items are F6).

  `scenarios/*.md` doesn't say whether it is per skill (`skills/<name>/scenarios/`) or shared, or how it relates to `sindri evolve check` suites.
  - Evidence: plan l.706–707; l.520–522; l.553 (F1), l.558 (F6).
  - Fix: Put the lint in F1 with the concrete budget (for example ≤ 400 lines in SKILL.md, references split out), a `<!-- sindri-mode -->…<!-- /sindri-mode -->` region marker, `skills/<name>/scenarios/*.md` with a fenced `expected:` block, and `sindri skill lint [<name>] [--json]`. Add it to AGENTS.md's merge gate.

- **M5 · API surface ergonomics:** Rule inspection is narrower than the problem.
  - `sindri rules explain <id>` (plural) sits next to `/rule` and the singular nouns the CLI uses today (`profile`, `repo`, `index`).
  - "Why a rule was selected" needs a Step or session spec to match selectors against, but no `--step` is given.
  - There's still no effective list across the four layers, and no `lens list|show`.
  - Evidence: plan l.711; l.287–291; `docs/sindri/README.md` l.10 (`profile … explain <key>`).
  - Fix: Add `sindri rule list [--effective --repo R] [--json]` and `sindri rule explain <id> --step <step>`, which prints layer provenance, the selector match trace and the rank against the budget. Add `sindri lens list|show <id>`. Use singular nouns throughout.

- **M6 · API surface ergonomics:** It's still undefined what `EvidenceProvider.publish` does unattended. The contract says "ask-first rules stay per provider". Sindri mode has no asks, and §17.12 says publishes are never phone decisions. A provider author can't tell whether to park, skip, or emit a desk item.
  - Evidence: plan l.449; l.368; l.485 ("Never … publishes").
  - Fix: In sindri mode, `publish` returns `PendingPublish` and enqueues a desk-queue item, or the ship Step publishes under the ship contract's shared rules (§17.11 l.438). Write this in the provider contract and in `ship-contract.md`.

- **M7 · Setup / install friction:** There's no checked-in example packet, rule or lens, and no "Quick start: factory" block. `init --from-scope` and F0.5 lower the cost, but an author still learns the required fields from the C# records or the generated JSON Schema.
  - Time-to-first-success: about 30 minutes. That assumes sindri is installed and the profile approved, the R1a C# packet binary is present, and `sindri scope` takes about 8 minutes on a small brief. `packet init --from-scope` stubs write scopes and acceptance examples. The author fills 1 workstream and 2 tasks and runs 2–3 `validate --json` iterations. With an example packet and a quick start, it would be about 10 minutes.
  - Evidence: absent. Searched "example", "fixture", "quick start", "sample".
  - Fix: Ship `docs/sindri/examples/packet-ring0/`. A real F0.5 micro-packet works well, because F0.5 has to produce one anyway. Add `examples/rules/` and `examples/lenses/`. Add the quick-start block in F0's "Turn it on" task.

- **M8 · Error messages & observability:** Only `SND-SANDBOX` is seeded. The new surfaces need areas and codes in `errors.ts` before `docs/sindri/errors.md` can be generated, and that registry test fails on undocumented codes. Revision 3 adds several refusals "with a code" but doesn't name them:
  - a stale item version or reused nonce;
  - a revoked device;
  - a lease that can't be reached;
  - a handover that fails verification;
  - a query without a tenant.

  The areas needed are PACKET, RULE, LENS, WORKFLOW, EVIDENCE, HOST/LEASE, DEVICE, QUOTA, SCHED and TENANT.
  - Evidence: plan l.674 ("refused with a code"); l.654–655; l.658; l.766 ("A query without a tenant fails"). Searched `SND-`: only `SND-SANDBOX`, `SND-SCOPE` and `SND-GATE` appear.
  - Fix: Add an error-areas row to §17.16 per F step, with one `SND-PACKET` code per `packet validate` check (l.218–225). Seed DEVICE and LEASE in F4 and F7.

- **M9 · Error messages & observability:** "Anything loaded that the plan didn't name is a defect with a metric" still has no record or command. A developer can't find which skill, MCP server or memory leaked into a Step, or from which settings layer.
  - Evidence: plan l.99–100; l.385–387.
  - Fix: Add a `context-leak` ledger event (`{step, lens, source, name, tokens, settingsLayer}`), a `doctor` line per lens, and `sindri step context <step>`, which compares first-call sections with their budget.

- **M10 · Documentation completeness:** Apart from `glossary.md` and `ship-contract.md`, the addendum names no docs to write.
  - None of these has a `docs/sindri/*.md` target: packets, rules, lenses, workflows, standing-workflow tiers, sandbox denials, sindri mode, host handover and lease, device enrollment, the scheduler, or workers.
  - The README command table isn't updated for any new verb.
  - Evidence: absent. Searched "docs/sindri", "README". Only l.712 and l.434 name docs.
  - Fix: Add a docs column to the §17.16 table:
    - F0: `packet.md`, `rules.md`, `lenses.md` (generated from schemas, like `profile.md`)
    - F1: `sindri-mode.md`
    - F2: `factory.md` (compile, sandbox, `why-denied`, scheduler)
    - F4: `review-anywhere.md` (device enroll and revoke, decision states)
    - F7: the `host-setup.md` handover and lease section
    - P2: `workers.md`

    Update the README table in each "Turn it on" task.

- **M11 · Backwards compatibility / observability:** The metric changes still don't say how existing ledger and scorer rows are reclassified into build and desk turns. The new weekly **human-minutes** metric pauses auto-started work when it crosses a ceiling (l.715–716), but nothing says how minutes are measured or what the pause does to running Steps:
  - Are desk minutes wall time, transcript turns × an estimate, or active-pane time?
  - Are phone minutes per-decision dwell time?
  - Does a pause stop running Steps?

  An enforcing control with an undefined input is hard to debug when it fires.
  - Evidence: plan l.105–120; l.714–718.
  - Fix: Define the `turnClass` field and its legacy default (`unclassified`, excluded from baselines). Define the human-minutes source per surface (scorer session spans for the desk, signed-decision timestamps for the phone, takeover spans) and add a `sindri budget status` line. Running Steps finish; only new auto-starts pause.

- **M12 · Error messages & observability (new, §17.20):** The P1 scheduler adds WIP limits per packet and ring, priorities, conflict-aware serialization (write scopes or `deps.yaml` neighbours), resource classes and a computed session cap. Nothing shows why a Step is queued. "Why isn't my task running?" becomes the most common factory question, and the answer can come from six sources. P1 "joins F2", but the F2 row in §17.16 doesn't list it, and no config keys are named.
  - Evidence: plan l.729; l.750; l.554 (F2 row lacks the scheduler).
  - Fix: Add `sindri queue [--packet P] [--json]`. It lists each waiting Step with its blocking reason (`wip-limit:packet`, `conflict:<step>`, `resource:browser`, `quota:<account>`, `human-minutes`) and its position. Add `sindri queue why <step>`. Name the profile keys (`scheduler.wip.perPacket`, `scheduler.wip.perRing`, `resources.<class>.slots`). Add P1 to the F2 row.

### LOW
- **L1 · API surface ergonomics:** The §17.7 lens example still shows raw command strings (`"<repo migrate cmd> --dry-run"`, l.321), which §17.19 replaced with aliases. Selector semantics are still unstated: AND across keys or OR within lists, the glob dialect, and what `"*"` means. The `check.type` enum lists only `eslint`.
  - Evidence: plan l.321, l.281–282, l.710–711.
  - Fix: Update the example to `commands: [{alias: migrate, args: [--dry-run]}]`. Put the selector semantics and a closed `check.type` enum in `rules.md`.
- **L2 · Setup / install friction:** Handover has `--dry-run` and `--back`, but partial failure isn't defined: the target becomes unreachable mid-sync, a checkpoint never reaches a safe point, or the signature fails on arrival. Nothing gives the timeout, the source state after an abort, or the codes.
  - Evidence: plan l.508–510, l.658–659.
  - Fix: On any failure, the source keeps the lease with its epoch unchanged and prints the code. Add `--timeout` and `sindri host status`.
- **L3 · Ramp-up:** §17.8 still calls the opt-in desk setup a "`lean` profile" (l.389), which is the overload the glossary was meant to remove.
  - Fix: Call it "lean session mode".
- **L4 · API surface ergonomics:** Several verbs have no F step or synopsis: `/rule`, `sindri attach`, `sindri device enroll|revoke`, `sindri scout`, and the P2 worker registration, which has no `sindri worker join` or setup steps.
  - Evidence: plan l.199, l.488, l.671–672, l.579, l.730. Absent from §17.16.
  - Fix: Place each verb in an F or P step with a one-line synopsis. Give P2 a `worker join <coordinator>` flow with its prerequisites (tailnet, container runtime, version-skew check).
- **L5 · Documentation completeness:** F5 has no adapter-authoring doc for the component interfaces and the `Source → Connector` shim.
  - Fix: Point F5 at `docs/sindri/adapters.md`.
- **L6 · Documentation completeness:** The sections are out of order: §17.17 comes after §17.21, and there's no §17.17 entry where readers expect it. That slows lookups by number.
  - Fix: Move §17.17 Decisions back between §17.16 and §17.18, or renumber.

## Per-dimension summary
| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | HIGH | 6 (micro-packet H, M1, M5, M6, L1, L4) |
| Setup / install (time-to-first-success: ~30 min) | MEDIUM | 2 (M7, L2) |
| Error messages & observability | MEDIUM | 4 (M2, M8, M9, M12) |
| Docs completeness | MEDIUM | 3 (M10, L5, L6) |
| Backwards compatibility | HIGH | 3 (seam-vs-golden H, M3, M11) |
| Ramp-up cost (tribal-knowledge items: 12) | MEDIUM | 2 (M4, L3) |

**Tribal-knowledge list** (facts a new contributor or agent would still have to be told):
1. Where a packet directory lives (in the repo, under `$AW_STATE_DIR`, or in the profile repo). This is still unstated.
2. Whether a micro-packet may freeze without the desk, and what decides its write scope.
3. Which code a denied command returns (`SND-GATE-1xx` or `SND-SANDBOX-1xx`), and where the denial id for `why-denied` comes from.
4. Who sets `SINDRI_MODE`, and whether a session can override it.
5. How the state-path shim behaves when two worktrees work on the same PR or bug.
6. The on-disk paths of the four rule layers and their precedence.
7. The per-repo test-path convention used to widen `write` scope.
8. The change-class taxonomy (`schema`, `migration`, `query`, …) and the task-kind enum used by selectors and lens triggers.
9. The token, turn and wall-time budgets per size class and lens.
10. How to rebuild the per-(repo, toolchain) session image and where its managed settings live.
11. Whether the C# port adds `tenant`, `ring`, `actor` and idempotency fields before or after golden cutover, and what old rows get.
12. How human-minutes are measured, and which scheduler limit is holding a queued Step.

## Verdict
Revision 3 closes or mostly closes all four round-1 HIGHs. Two new HIGHs remain: micro-packets take write scope from a model and freeze without the desk, against invariant 13 and §17.12, and the §17.20/§17.21 ledger seams scheduled "during the C# port" conflict with the readiness cutover contract's frozen, byte-compatible ledger schemas.
verdict: NEEDS_WORK
