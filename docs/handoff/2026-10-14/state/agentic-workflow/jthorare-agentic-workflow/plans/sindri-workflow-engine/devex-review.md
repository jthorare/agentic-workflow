# Plan Devex Review — sindri-workflow-engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md`
**Reviewed:** 2026-10-10

Cross-checked against `.agents/rules/csharp.md`, `.agents/rules/csharp-testing.md`, the C# slice recipe and `/new-slice`
contract (`AGENTS.md`, `docs/csharp/scaffolds/new-slice.md`, `scripts/scaffold/new-slice.mjs`), and the S1 app shell
as it stands in `.claude/worktrees/sindri-s1-app-shell` (read only: `docs/csharp/sindri.md`,
`src/Sindri/Aw.Sindri.Host/Commands/CommandTable.cs`, `SindriErrors.cs`, `SqliteLedger.cs`, `YamlToJson.cs`).

## Summary

The plan is a single design spec. It has no separate `plan.md`, `engineering.md` or `TASKS.md`, so the §Phases section
was reviewed as the task breakdown. The core model is strong for recipe authors: a closed schema, named `RCP-*` rules
each with a fixture, an output line that includes the fix, and `next` hints. The developer-facing gaps cluster in four
places:

1. **Phase sequencing doesn't line up.** The Phase 1 and Phase 2 signals depend on descriptors and eval files that only
   land in later phases.
2. **The CLI's process model is unstated.** The plan never says which verbs run in-process and which need a running,
   lease-holding `sindri serve`. Background services also run only when the approved profile names them.
3. **Block authoring is underspecified.** The plan doesn't say where blocks live, how the descriptor is declared, or
   how the CLI's catalog is populated. The fixture blocks the signals need don't exist in the real binary.
4. **Two instructions are wrong for this repo.** New error codes are sent to `docs/sindri/errors.md`, which is
   generated from TS and checked by a TS test. The S1 dependencies have no owning PR.

Time-to-first-success: ~15 minutes to validate a recipe, ~40 minutes to see a first instance move in the ledger.

## Findings by severity

### CRITICAL

None.

### HIGH

- **API surface ergonomics / Phases:** the phase signals can't pass in the phase that claims them.
  - Phase 1's signal says `recipes/sindri/workflow.step.yaml` "exits 0 and prints its hash". But the real descriptors
    for `step.produce`, `step.checks` and `step.verify` arrive only in Phase 2 ("the real descriptors for the first
    recipes' blocks"), so Phase 1 would report `RCP-BLOCK-001`. The recipe file itself isn't in Phase 1's file list
    either.
  - Phase 2's signal approves `workflow.step.yaml`, but §3.1 makes `evals` required "to approve", and the eval files
    land in Phase 3.
  - Evidence: plan lines 468–470 ("`recipes/sindri/workflow.step.yaml` exits 0 and prints its hash"), 482 ("the real
    descriptors for the first recipes' blocks"), 99 ("`evals` | list of paths | to approve"), 485–487 (Phase 2 signal),
    495 (evals in Phase 3).
  - Fix: move the descriptors (data only) for the first recipes' blocks and the two recipe files into Phase 1, and
    move the eval files and the `evals`-required-to-approve check ahead of Phase 2's signal. Or make Phase 2's signal
    approve a fixture recipe with an inline eval.

- **API surface ergonomics (recipe authors):** `when: { outcome }` after a `wait` on an **event** or a **resource**
  has no declared outcome set, so `workflow.item` can't pass its own `RCP-WHEN-001`, or the rule has a hole.
  - §3.5 and `RCP-WHEN-001` accept only outcomes declared by "the Step's descriptor (or a child recipe's terminal
    states)". But `triaged` waits on `item.routed` with outcomes `startable` / `notStartable`, and `claimed` relies on
    the effect descriptor declaring the outcome of a *different* event.
  - The validator is also said to run "on descriptors alone", yet `RCP-CHILD-001` needs the child recipe (a registry,
    which doesn't exist until Phase 2), `RCP-EFFECT-001` needs the profile's allowlist, and `RCP-CAP-001/002` need
    the base recipe.
  - Evidence: plan lines 155, 328, 334, 407–411, 442–443; W1 line 20 ("The validator runs on descriptors alone").
  - Fix: add an event-kind catalog (`EventDescriptor(kind, outcomes)`, registered like blocks), and make
    `RCP-WHEN-001` check `waitFor.event` outcomes against it. Define one `ValidationContext(BlockCatalog,
    EventCatalog, IRecipeResolver, EffectAllowlist, BaseSnapshot?)` record as the validator's only input, and say
    which parts are empty in Phase 1. For example, child recipes could resolve from sibling files given on the command
    line.

- **Setup / install friction:** the plan never says which new verbs are API clients and which run in-process, or that
  the engine's background services need profile entries.
  - S1's contract separates in-process verbs (`doctor`, `profile validate`) from API clients that need the control
    plane running (`profile approve`, because "the approval is a ledger event fenced by the lease"). By that rule,
    `recipe approve/revoke/register` and every `workflow …` verb must have a running `sindri serve`. `recipe validate`
    and `recipe eval` should work while it is down. The plan says neither.
  - S1 runs only "the services the **approved** profile names". `workflow.timers` (Phase 5) and the router, slot and
    inbox services (Phase 7) therefore do nothing until someone adds them under `services:` and re-approves the
    profile. That is a silent failure of the Phase 5 signal.
  - Evidence: absent — searched the plan for "serve", "in-process", "API client", "running", "services:", "profile
    names". Lines 251, 522 and 554 only register the services. S1 `docs/csharp/sindri.md` "Which verbs are API
    clients" and "Scheduler".
  - Fix: add a verb table in the S1 doc's shape (command · in-process or API client · exit codes). Also do one of:
    make the engine services always-on and record that decision, or list the profile `services:` entries each phase
    needs (`workflow.timers: { every: 15s }`) and put them in the phase signals.

- **API surface ergonomics (CLI/API):** instance addressing and start inputs are undefined.
  - The verbs address instances by item (`workflow status T-3`, `cancel T-1 --version <stale>`). But an instance id
    comes from `(recipe, item, attempt, parent)`, and a child shares its parent's item, so `T-3` names at least two
    instances. Nothing says which one `--version` refers to.
  - `--spec spec.json` is ad hoc: it doesn't say how a file becomes a fact, which §5.1 requires to be a "registered
    artifact" with `{ref, sha256, headSha}`. It also doesn't say how `attempt` is chosen, or what a second `start` of
    the same item returns.
  - Evidence: plan lines 216, 223, 513–516, 559–562.
  - Fix: specify that an item resolves to its latest attempt's root instance, and add `--instance <id>` for children.
    Use the generic `--input <name>=@<file>` (it registers the file as an artifact and records its sha). Make `start`
    idempotent (it returns the existing instance and version), with `--new-attempt` to start another. Name the flag
    `--expect-version` and make it optional (default: the current version).

- **Ramp-up cost (block authors):** a block author can't write a block from this doc.
  - W1 says each block has a "static, data-only `BlockDescriptor`" and that `Add…()` calls `catalog.Register` and
    `runtime.Register(id, factory)`. It doesn't say:
    - how the descriptor is declared (a static abstract member on the kind interface? a separate `…Descriptor`
      class?);
    - where block classes live (`Aw.Sindri.Features/Blocks/<area>/`?). Judge and Evidence blocks can't live in their
      own packages, because Sindri is tier 3 and project edges point down;
    - what the factory lambda may resolve under `ARCH-DI-001`;
    - how the in-process `recipe validate` gets the catalog without the block runtimes.
  - The fixture blocks (`gate.fixture.*`, `step.fixture.scripted`, `effect.fixture.echo`) live in `Aw.Sindri.Testing`,
    which production code may never reference. Yet the Phase 5 and 6 signals run fixture recipes in the real binary.
  - Evidence: plan lines 20, 72–74, 380–381, 528–530, 544; csharp.md "Test support" (production never references
    `*.Testing`).
  - Fix: add a "Writing a block" subsection: file layout, descriptor declaration, one worked block with its
    `BlockContract` binding, and the registration lines. State how fixtures reach the real binary: wait-only fixture
    recipes that need no blocks, or a `Debug`-only fixture-blocks registration behind an env var. Reword "once for
    every recipe that uses it" (line 73–74) into a concrete rule.

- **Backwards compatibility / migration:** block descriptors can change under approved recipes and running instances.
  - Approval is keyed by the recipe's `(id, version, hash)`. The hash covers only recipe text, yet a block's `Version`,
    `Capabilities`, `Outcomes` or `Parameters` can change in a later build.
  - An approved recipe could then gain a capability without re-approval, against invariant 13. A running instance's
    frozen recipe could also name an outcome or parameter that no longer exists.
  - Evidence: plan lines 53 (`int Version` with no stated use), 193–194, 218 ("frozen at start"); absent — searched
    "descriptor version", "block version", "re-validate".
  - Fix: record the used blocks' `(id, version, capabilities)` in the approval row. Re-validate at instance start and
    at every transition. A mismatch refuses start (`SND-RECIPE-00x`) or parks a running instance with
    `spec-conflict`. Say that raising a block's `Version` is required whenever its descriptor changes, and have
    `BlockContract` check it.

- **Documentation completeness:** the error codes are sent to a generated TS file.
  - §6a puts the new codes in "`SindriErrors`, `docs/sindri/errors.md`". On the S1 branch, `docs/sindri/errors.md`
    says "Generated from `sindri/src/errors.ts` … Do not edit by hand", and `sindri/tests/errors.test.ts` fails when it
    differs, which breaks merge gate item 2.
  - The C# catalogue lives in the Errors table of `docs/csharp/sindri.md`.
  - `docs/sindri/recipes.md` (Phase 1) would also put a C# contract into the TS sindri's docs folder.
  - Evidence: plan lines 304, 465.
  - Fix: document the codes in `docs/csharp/sindri.md` (Errors). Put the recipe, block and workflow contract in
    `docs/csharp/sindri-workflow.md`, or a section of `docs/csharp/sindri.md`.

- **Ramp-up cost (implementers via /specToProvenPR):** no PR owns the S1 dependencies.
  - S1-D2, D3, D5, D6 and D7 are "Missing" or "To confirm" and are "needed by" Phases 2, 4 and 6. The plan doesn't
    say whether they go into the S1 PR, an S1 follow-up, or the phase PR that needs them.
  - D2 changes S1's private `SqliteLedger.InsertOnce` and its transaction ownership. D3 adds a dispatcher. Either one
    edits S1 code from inside a stacked phase.
  - S1-D3 is also partly wrong: `outbox_commands` already exists (S1 migration 3, "empty in S1"); only the dispatcher
    is missing.
  - Evidence: plan lines 286–294; S1 `docs/csharp/sindri.md` Ledger ("`outbox_commands` is Kernel.Data's outbox table
    (migration 3)").
  - Fix: add an "Owner PR" column, with files, to the §6.2 table. Correct D3 to "dispatcher missing". Resolve D7
    (actor) before Phase 4 starts.

### MEDIUM

- **API surface ergonomics:** the exit codes don't follow S1's scheme.
  - S1 uses `profile validate` 0 = valid and approved, 1 = valid but unapproved, 2 = invalid, and 2 for usage errors.
  - `recipe validate` exits 1 on any failure and reports `unapproved` "as information".
  - Evidence: plan lines 322, 329; S1 `docs/csharp/sindri.md` command table.
  - Fix: use S1's codes for every `recipe` and `workflow` verb (0 ok, 1 valid but not approved or a refused
    transition, 2 invalid, a usage error or a tool error), and list them per verb.

- **Error messages & observability:** the error catalogue has no fix text and is missing cases.
  - S1 errors print `(fix: …)`, but §6a has only a Meaning column.
  - `SND-WF-010` doesn't say that it names the block id and kind.
  - `SND-WF-003` covers both "wrong trigger" and "timeout during takeover".
  - No code covers an answer to a non-parked instance, `grant` on a park that isn't `rounds` or `no-progress`, a
    takeover of a terminal instance, or an unknown `--input`.
  - Evidence: plan lines 304–317; S1 doc: "Errors go to stderr as `SND-<AREA>-NNN message`, with `(fix: …)`".
  - Fix: add a Fix column. Name the block or the trigger in the message. Add the missing codes, or map each case to an
    existing code explicitly.

- **Error messages & observability:** the engine has no telemetry plan.
  - csharp.md requires `[LoggerMessage]` and `Aw.*` `ActivitySource`/`Meter` names. The plan defines ledger events
    only: no spans per `Commit` or transition, no metrics, no log events for refusals or dispatch rechecks.
  - The `workflow status` output (which columns, whether it shows `next`, deadlines, the last refusal's gate results)
    is defined only by the example strings in the signals.
  - Evidence: absent — searched "telemetry", "ActivitySource", "Meter", "LoggerMessage", "span", "metric".
  - Fix: add a short Telemetry subsection: `Aw.Sindri.Workflow` source and meter, a span per `Commit`, and counters for
    transitions, refusals, parks by reason and outbox dispatches (with small fixed tag sets). Define the `workflow
    status` text and `--json` fields, including the last `workflow.refused` gate results and the rendered `next`.

- **Error messages & observability (recipe authors):** validator diagnostics lack source positions.
  - The output is `… at <path>` (e.g. `states.orphan`). No YAML line and column, although YamlDotNet's representation
    model has start marks that `YamlToJson` currently drops.
  - `RCP-SCHEMA-001` promises per-field shape errors. A typed `AwJson` deserialize with
    `JsonUnmappedMemberHandling.Disallow` stops at the first error.
  - W19 generates the JSON Schema from the records. The short and long forms (`step: id` or `{use, with}`, a gate as
    an id or an object) need custom converters, which an exporter won't see, so the editor schema would be wrong.
  - Evidence: plan lines 81–82, 118, 142–144, 322, 326, 38.
  - Fix: carry `line:col` from the YAML node into each `RCP-*` line. Say whether the schema check is a structural pass
    over the parsed tree that collects every error, or a typed deserialize that stops at the first. Say how the
    polymorphic forms appear in the generated schema (a `oneOf` written by hand, plus a test).

- **Documentation completeness (recipe authors):** the schema text has errors and gaps.
  - §3.3's field table omits `kind`, which every example uses.
  - The child-input form reads `{ fact: fact:stepSpec }`, with the prefix twice.
  - The eval path shapes disagree: `evals/workflow.step/*.jsonl`, a glob in a field typed "list of paths", against
    `recipes/sindri/evals/*.jsonl`.
  - The eval JSONL record format (trigger, expected state, outcome) is never given.
  - Evidence: plan lines 116–124, 401, 99, 365, 414, 439, 495.
  - Fix: add `kind` to the table. Allow `{ fact: stepSpec }`, or say the key is written in full. Pick one eval layout
    and say whether globs are allowed. Add the eval record schema with a three-line example.

- **Ramp-up cost:** the plan's verbs and slices don't fit `/new-slice` or S1's command table.
  - `/new-slice --command` accepts only `^[a-z][a-z0-9-]{1,40}$`, so it can't express `recipe validate` or `workflow
    start`.
  - S1's `CommandTable.Dispatch` is one hand-edited switch with about 10 arms and no flag parser (only `--json`).
    Adding about 15 verbs with `--item`, `--input`, `--expect-version` and `--grant` would cross `CA1502` (≤ 10) and
    `AW1005` (≤ 300 lines).
  - `ApplyTrigger` plausibly needs `IWorkflowStore`, `IRecipeStore`, `IRecipeApprovals`, the block runtime map,
    `ILease` and `TimeProvider`. That is more than `/new-slice`'s 5 (`NS-DEPS`) and hits the `AW1008` split signal.
  - Evidence: plan lines 280, 282, 509–510; `scripts/scaffold/new-slice.mjs` line 69; S1 `CommandTable.cs`.
  - Fix: add per-group dispatchers (`RecipeVerbs`, `WorkflowVerbs`) and a small flag parser in Phase 1. Either
    extend `/new-slice` to multi-word commands or say that the CLI command is written by hand. Split `ApplyTrigger`
    (one slice per command group) or introduce one `IWorkflowRuntime` port that bundles the store and the catalog
    lookups.

- **Documentation completeness:** only Phase 1 writes documentation.
  - No later phase updates the docs for its verbs, events, error codes, the eval format, the block-author guide or the
    S1 doc's verb, file and error tables. `AGENTS.md` Commands doesn't gain the new checks.
  - Evidence: plan lines 465 (the only doc file in any phase), 474–562.
  - Fix: add a doc line to each phase's Files: the verb rows, error rows and events it introduces in the C# Sindri
    doc. Add the block-author guide in Phase 2, with the real descriptors.

- **Backwards compatibility / migration:** the recipe version rules on register are unstated.
  - Is a `register` with the same `id` and `version` but a new hash refused? Is a lower version refused? Does
    approving `v2` leave `v1` approved, so new starts could pick either? Which version does `workflow start
    workflow.step` pick?
  - Evidence: plan line 90 ("Rises with each registered change to this `id`"), lines 196–200.
  - Fix: state the rules. For example: same version with a new hash → `SND-RECIPE-00x`. `start` uses the highest
    approved version unless given `id@hash`. `list` shows every version with its approval state.

- **Backwards compatibility / migration:** migrations across a 7-PR stack are unordered.
  - Phases 2, 4, 5 and 7 each add `ledger.db` migrations while S1 is still open and may add its own.
  - Numbered migrations in stacked PRs collide when anything below them is rebased.
  - A dev state directory built by an earlier phase meets `SND-LEDGER-001` ("schema doesn't match this build") with no
    stated upgrade.
  - Evidence: plan lines 281, 451–453; S1 Errors `SND-LEDGER-001`.
  - Fix: reserve migration numbers per phase in the plan. State that a newer build migrates a dev ledger forward, or
    that dev state directories are thrown away between phases.

- **Setup / install friction:** the dev loop is unwritten.
  - Every signal invokes `sindri …`, but nothing installs the C# binary before the switch.
  - The C# S1 has no `profile init`, so a developer writes `<home>/profile/profile.yaml` by hand.
  - The "dev state directory" in Phase 5 is `AW_SINDRI_HOME`, which the plan doesn't name.
  - The typed confirmation is S1's "first 6 characters of the hash"; the plan says only "typed confirmation".
  - Evidence: plan lines 468, 485, 513, 528 ("registered in a dev state directory").
  - Fix: add a "Dev loop" box:
    - `export AW_SINDRI_HOME=$(mktemp -d)`;
    - a minimal `profile.yaml`;
    - `alias sindri='dotnet run --project src/Sindri/Aw.Sindri.Host --'`;
    - `sindri profile approve <hash>` then `sindri &`;
    - `sindri recipe register|approve`;
    - `sindri workflow start`.

- **API surface ergonomics (recipe authors):** `timeout.then: retry` on a `wait` state doesn't redo the work.
  - Effects belong to transitions, so re-entering `claimed` after a timeout waits again but never re-sends
    `effect.claim.ref`.
  - Evidence: plan lines 132, 398, 409.
  - Fix: say that `retry` re-arms the timer only, and offer a re-entry transition when the work must re-run. Or define
    that `retry` re-applies the entering transition's effects with a new idempotency key, and add a validator warning
    for `retry` on a wait state whose entering transitions have no effects.

### LOW

- **API surface ergonomics (block authors):** `BlockDescriptor` invites inconsistent descriptors.
  - It is a 10-argument positional record, and `ThresholdApplies` can be derived from `Rung` (true for 2 and 3).
  - Evidence: plan lines 50–60.
  - Fix: drop `ThresholdApplies` (compute it). Use `required` init members so call sites name each field. Have
    `BlockContract` assert the kind-specific fields (`Rung` only on gates, `Effects` on effects).

- **API surface ergonomics:** the verb namespaces are inconsistent.
  - `sindri takeover|handback <item>` are top-level while every other instance verb is `sindri workflow …`.
  - `sindri outbox list` (Phase 6) and `sindri ledger` (S1-D6) appear in signals but in no phase's Files.
  - Evidence: plan lines 525, 545, 513.
  - Fix: use `sindri workflow takeover|handback`, and assign `outbox list` and `ledger` to a phase or an S1 PR.

- **Ramp-up cost:** the stacked-worktree mechanics are implicit.
  - W17 says "its own worktree off that branch", but not how to rebase the stack when S1 changes, or that the existing
    S1 worktree must not be used.
  - Evidence: plan lines 36, 451–452.
  - Fix: add the two commands (create the worktree off `feat/sindri-s1-app-shell`, rebase the stack in order) and the
    rule that the S1 worktree is read-only.

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | HIGH | 7 |
| Setup / install (time-to-first-success: ~15 min to validate, ~40 min to a first instance) | HIGH | 2 |
| Error messages & observability | MEDIUM | 3 |
| Docs completeness | HIGH | 3 |
| Backwards compatibility | HIGH | 3 |
| Ramp-up cost (tribal-knowledge items: 14) | HIGH | 4 |

**Time-to-first-success assumptions:**
- Validate a recipe in ~15 min: a new worktree off the S1 branch, a locked `dotnet restore`, a solution build of about
  3–5 min, then `dotnet run --project src/Sindri/Aw.Sindri.Host -- recipe validate <file>`. This assumes the Phase 1
  descriptor gap is fixed.
- A first instance in ~40 min: also hand-writing and approving a profile, adding `services:` entries, starting
  `sindri serve`, writing an eval file so the recipe can be approved, approving at a TTY, then `workflow start` and
  reading the ledger.

**Tribal-knowledge list** (facts a new developer must be told, none of them written in the plan):
1. Which verbs need a running `sindri serve` holding the lease, and which run in-process.
2. Background services run only when the approved profile's `services:` names them, so `workflow.timers` and the
   Phase 7 stubs need profile entries and a re-approval.
3. `sindri` isn't installed before the switch. Use `dotnet run --project src/Sindri/Aw.Sindri.Host --` or the AOT smoke
   publish.
4. `AW_SINDRI_HOME` isolates a dev state directory. The C# S1 has no `profile init`.
5. The typed confirmation is the first 6 characters of the hash (S1 convention).
6. `docs/sindri/errors.md` is generated from TS and test-checked. C# codes go in `docs/csharp/sindri.md`.
7. `YamlToJson` is `internal` to `Aw.Sindri.Infrastructure` (under `Profile/`), so the recipe reader must live in
   Infrastructure or move it.
8. `/new-slice --command` can't express two-word verbs, and `CommandTable` is a hand-edited switch with no flag parser.
9. Where block classes live, and why Judge and Evidence blocks can't live in their own packages (tier direction).
10. Where the outcomes of `waitFor: event` are declared.
11. The eval JSONL record format.
12. Which PR implements each of S1-D2, D3, D5, D6 and D7. `outbox_commands` already exists.
13. Fixture blocks live in `Aw.Sindri.Testing`, which production may not reference, so how they reach the real binary
    for the Phase 5 and 6 signals.
14. Which instance an item argument names when an item has a child or several attempts.

## Verdict

The plan has eight HIGH findings and no CRITICAL one. The main ones:
- Phase 1 and Phase 2 signals depend on later phases' descriptors and eval files.
- Event-wait outcomes are undeclared, which breaks `RCP-WHEN-001` on `workflow.item`.
- The CLI process model and the profile-gated services are unstated.
- Instance addressing and start inputs are undefined.
- Block authoring is incomplete.
- Block descriptors can change under approved recipes.
- Error codes are sent to a generated TS file.
- No PR owns the S1 dependencies.

All eight can be fixed in the doc without reopening W1–W19.

verdict: NEEDS_WORK
