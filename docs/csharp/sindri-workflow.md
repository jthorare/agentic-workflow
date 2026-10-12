# Sindri workflow engine (C#) — recipes and blocks

A **recipe** is a YAML state machine that Sindri's workflow engine runs for an item; a **block** is the C# code a
recipe names (a gate, a Step, an effect, an evidence provider or a Step input). This page is the reference for writing
both. The design and its reasons are in the engine spec,
`docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (§2 blocks, §3 the schema, §9 the validator); where
this page summarizes, the spec wins. The app shell (the `sindri` binary, its home, the error catalogue) is in
`docs/csharp/sindri.md`.

Phase 1 (this page's scope) reads, normalizes, hashes and validates recipes in-process. Registering, approving and
running them come in later phases (registry P2, engine and evals P3, store and desk Steps P4).

## `sindri recipe validate`

```
sindri recipe validate <file> [--json]
```

Local: it runs in the calling process with this build's block catalog, and never starts or contacts `sindri serve`. It
reads the file and its child recipes, and writes nothing.

- **Plain output:** one line per finding, then `hash sha256:<64 hex>` when the recipe bound (it parsed and its fields
  have the right types). The hash is printed in full, not as §3.1's 12-character display, because it is the value a
  `hash:` field takes. Errors come first, as `RCP-…: <what> at <file>:<line>:<col> (<path>); <fix>`. Information and
  skipped findings follow, with `information: ` or `skipped: ` in front of the same line, so a line that starts with
  `RCP-` is always an error. Output is ASCII as long as the recipe's own names are.
- **`--json`:** one JSON object on one line: `passed`, `hash` (null when the recipe didn't bind), `findings` (in the
  plain order; each has `code`, `severity` (`error`, `information` or `skipped`), `message`, `file`, `line`, `column`,
  `path` and `fix`) and `normalized`. `normalized` is the normalized long form without `hash`; SHA-256 over its RFC 8785
  canonical JSON is `hash`.
- **Child recipes** resolve from the recipe file's directory (`<child id>.yaml`), so `recipes/sindri/workflow.item.yaml`
  finds `workflow.step.yaml`. Once the registry exists (P2), a served validation resolves them from the registered recipes.
- **Hand-written only:** `recipe validate` passes no base recipe, so it checks every recipe as hand-written: a `base`
  field is `RCP-CAP-004`, and the generated-recipe rules (`RCP-CAP-001`, `-002`, `-005`, `-006`) don't run. Those will run
  when Sindri's generator checks a generated recipe against its base (the packet freeze, W9); no command runs them in this build.
- **The profile:** there is no effect allowlist in the profile yet (`workflow.effects`, P6), so `RCP-EFFECT-001` is
  reported as `skipped`.

| Exit | When |
|---|---|
| 0 | No finding is an error (information and skipped findings don't count) |
| 1 | One or more errors |
| 2 | A usage error (no file: `SND-CLI-003`; an unknown `recipe` verb: `SND-CLI-001`), or a tool error: the file doesn't exist, is a directory or can't be read (`SND-RECIPE-010 <path> doesn't exist`, `… is a directory, not a recipe file` or `… can't be read: <reason>`, each with its fix), or a bug inside `sindri` (`SND-API-005 recipe validate failed inside sindri: …`) |

Errors go to stderr as `CODE message (fix: …)`, or `CODE message (usage: …)` for a usage error, like every `sindri` verb.

`sindri recipe eval <file>` (run a recipe's eval cases, spec §9.2) arrives with P3. Until then it exits 2 with
`SND-CLI-001 'recipe eval' isn't in this build yet`.

```
$ sindri recipe validate tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/RCP-REACH-001.yaml
RCP-REACH-001: state 'orphan' is unreachable from 'produce' at RCP-REACH-001.yaml:14:3 (states.orphan); add a transition into it or remove it
hash sha256:a8c2b5e8be5a2d5877545ffbb7d8a3f6d82cd2b01254f2825e5b323d62e99f3c
$ echo $?
1
```

The golden files under `tests/Aw.Sindri.Tests/Workflow/Cli/Golden/` pin this output. After an intended change, re-pin
them with `AW_GOLDEN_UPDATE=1 dotnet test --project tests/Aw.Sindri.Tests --filter-namespace Aw.Sindri.Tests.Workflow.Cli`
(the run rewrites them and fails), review the diff, then run again without the variable.

## Recipe reference (schema 1)

JSON Schemas: `schemas/recipes/recipe.schema.json` is the long form, generated from the C# records by
`RecipeSchemaTests`. `schemas/recipes/recipe.editor.schema.json` also accepts the short forms; point an editor at it with
`# yaml-language-server: $schema=<path>/recipe.editor.schema.json`.

### Reading

- **Refused YAML** (`RCP-SCHEMA-003`): duplicate keys, anchors, aliases, merge keys, explicit tags, a file over 256 KiB,
  or nesting deeper than 32.
- **Scalars** follow the YAML 1.2 core schema, so `on` stays a string.
- **All schema errors** are reported with their line and column, not just the first. When the reader's only findings
  are unknown fields, the schema rules still run on the rest of the recipe, so a misspelt field doesn't hide a missing
  `next` or an undeclared state. After any other reader finding (a wrong type, a missing or empty value, refused YAML)
  they wait, since the recipe without that node would make them report on something the author didn't write. A recipe
  with any reader finding has no hash, and the other rule groups wait until it is fixed.
- **Short forms become long forms** before typed binding, which refuses unknown fields (`RCP-SCHEMA-001`). A short form
  and its long form bind to the same record and hash the same. `tests/Aw.Sindri.Testing/Workflow/Recipes/valid/` has
  `short-forms.yaml` and the same recipe written out as `long-forms.yaml`.

| Short form | Long form |
|---|---|
| `step: step.produce` | `step: { use: step.produce, with: {}, inputs: [] }` |
| `inputs: [fact:item]` | `inputs: [{ key: fact:item, label: null, type: null }]` |
| `from: produce` | `from: [produce]` |
| `gates: [gate.budget.available]` | `gates: [{ use: gate.budget.available, threshold: null, with: {} }]` |
| `effects: [effect.notify.desk]` | `effects: [{ use: effect.notify.desk, with: {} }]` |
| `with: { strict: true }` | `with: { strict: { literal: true } }` |
| `tokens: 200k`, `2M` | `tokens: 200000`, `2000000` |
| `loops: { r: { cap: 3 } }` | `loops: { r: { cap: 3, noProgress: [findings, emptyDiff, sameFailure] } }` |

**Hash:** `sha256:` over the RFC 8785 (JCS) canonical JSON of the normalized long form without `hash`. A `hash` written in
the file is checked (`RCP-HASH-001`). Listings show the first 12 hex digits. The long form writes every absent optional
value as `null` and every absent list or map as empty (owner decision, 2026-10-11), so adding an optional field to the format re-hashes every recipe, and each
needs re-approval.

### Top level

| Field | Required | Notes |
|---|---|---|
| `schema` | yes | `1` |
| `id` | yes | `workflow.<name>` |
| `version` | yes | An integer. Same id and version with another hash, or a version not above the highest registered, is refused once the registry exists (`RCP-VERSION-001`/`-002`, P2) |
| `description` | no | |
| `base` | generated recipes only | Written by Sindri's generator; on a hand-written recipe it is `RCP-CAP-004`, and `recipe validate` treats every recipe as hand-written |
| `inputs` | no | The facts a start supplies: `{ key, label?, type?: text \| file \| item }` |
| `initial` | yes | An `active` or `wait` state (`RCP-INIT-001`) |
| `states` | yes | At least one non-terminal and one terminal state. `cancelled`, `escalated`, `parked` and `taken-over` are reserved (`RCP-STATE-001`) |
| `transitions` | yes | |
| `loops`, `budget` | no | |
| `evals` | to approve (P2) | Paths of eval files, relative to the recipe's directory (`evals/<id>/<case>.jsonl`, spec §9.2). The paths' form isn't checked yet |
| `hash` | no | Checked when present |

### Value grammars

| Value | Form |
|---|---|
| Fact key | `fact:<name>` or `evidence:<kind>@<base\|head>` |
| Block id | `<gate\|step\|effect\|evidence\|input>.[<area>.]<name>`, kebab-case, at most 64 characters (`step.produce`, `gate.budget.available`; spec §3.3) |
| Event kind | `<area>.<name>`, registered with `AddEvent` (this build: `claim.created`, `item.routed`, `notify.delivered`) |
| Duration | `<n>m`, `<n>h` or `<n>d`, at least `1m` (`RCP-DUR-001`) |
| Tokens | An integer, or with a `k` or `M` suffix |
| `with:` value | A literal (string, number, bool, or a list of these), `{ fact: <key> }` or `{ profile: <key> }`. Capability parameters take only literals or `{ profile }` (`RCP-CAP-007`) |

### States

Every state has `kind: active | wait | terminal`. State, loop and parameter (`with:` key) names match
`[a-z][a-z0-9-]{0,63}` (`RCP-NAME-001`).

| Field | `active` | `wait` | `terminal` |
|---|---|---|---|
| `step` | required: `{ use, with?, inputs? }`; `inputs` are `input.…` blocks run before the Step starts | — | — |
| `waitFor` | — | required | — |
| `timeout` | required: `{ after, then: park \| escalate \| retry }` | required | — |
| `loop` | optional: a declared loop's name | optional | — |
| `budget` | optional: `{ tokens }` | — | — |
| `next` | required | required | optional |
| `outcome` | — | — | required: `resolved`, `unresolved`, `alreadySatisfied` or `failed` |

`waitFor` is one of `{ event: <kind> }`, `{ answer: { id, question } }`, `{ resource: heavy | slot }` or
`{ child: { recipe, inputs, onParked: propagate | ignore } }`. No resource has an allocator in this build, so a resource
wait is reported as information (`RCP-RES-001`); at run time it parks with `SND-WF-010` until one lands. `then: retry`
re-arms the deadline in the same frame and re-sends the entering transition's effects; it isn't allowed on a child wait
(`RCP-TIME-002`).

### Transitions

| Field | Required | Notes |
|---|---|---|
| `from` | yes | A non-terminal state, or a list of them. A name that isn't a state of the recipe is `RCP-SCHEMA-002` |
| `to` | yes | A state of the recipe; any other name is `RCP-SCHEMA-002` |
| `on` | yes | `step.completed` or `step.failed` from an active state; `wait.satisfied` from a wait state; `command.<verb>` from any non-terminal state (`RCP-TRIG-001`). Verbs match `[a-z][a-z-]{1,31}` (`RCP-TRIG-001`) and may not be one of `sindri workflow`'s own (`RCP-TRIG-002`) |
| `when` | no | Below |
| `gates` | no | `{ use, threshold?, with? }`, run in order. A judge or model gate needs a threshold above 0 and at most 1; a deterministic gate takes none (`RCP-THRESH-001`/`-002`) |
| `evidence` | no | `{ kind, at, expect: pass \| fail }` |
| `effects` | no | `{ use, with? }` |
| `produces` | no | Facts the transition records; each must be an output of the trigger's source (`RCP-WIRE-002`) |

The first transition whose `when` holds is the one taken. If its gates refuse, that is a refusal, not a fall-through to
the next transition.

### `when`

A closed set: `{ outcome }`, `{ fact, is: present | absent }`, `{ loop, below: cap }`, `{ all: [...] }`, `{ any: [...] }`
and `{ not: ... }`. Outcomes come from the trigger's source: the Step's declared outcomes, the event's, or a child's
terminal outcomes plus `cancelled` (`RCP-WHEN-001`). A loop must be declared (`RCP-WHEN-002`).

### Loops, budgets and `next`

- **Loops:** `loops: { <name>: { cap ≥ 1, noProgress } }`. `noProgress` defaults to `[findings, emptyDiff, sameFailure]`
  and can't be empty (`RCP-LOOP-003`). A looped state needs a transition back into it (`RCP-LOOP-002`).
- **Budgets:** `budget: { tokens }` on an active state and on the recipe. Exhausting one parks the instance with `budget`.
- **`next`** tells whoever reads the status what happens or what to do. Its variables are `{item}`, `{instance}`,
  `{state}`, `{user}`, `{child}`, `{loop.<name>}`, `{loop.<name>.cap}` and `{budget.tokens.left}` (`RCP-NEXT-001`).
  It never tells the reader to run an approval-tier command such as `--grant` (`RCP-NEXT-002`). Waits are descriptions
  ("Waiting for …"); states a human acts in are instructions.

### Engine-owned states

The engine adds `parked`, `escalated`, `taken-over` and `cancelled` to every recipe, with their own `next` texts and ways
out. A recipe can't declare them. In short: loop caps, no progress, budgets, refusals, timeouts, revoked or drifted
recipes and missing runtimes park; a top-rung gate below its threshold, an `escalate` timeout or an escalated child
escalates; `takeover` hands the instance to a human until `handback`; `cancel` ends it. Spec §3.8 has every row with its
`next` text and resume action.

## Rules

Every rule `recipe validate` runs has an invalid fixture, `tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/<RULE-ID>.yaml`,
and a test that pins its exact line (the P2 rows have none yet). All are errors except where the severity says otherwise.

| Rule | Checks | Fix |
|---|---|---|
| `RCP-SCHEMA-001` | No unknown or missing field, the fields a state's kind needs and no others (a missing `timeout` is `RCP-TIME-001`'s), and one form where a value takes exactly one (`waitFor`, `with:` values, `when`) | Remove the field, or add the missing one |
| `RCP-SCHEMA-002` | Each value has the right type and form (an enum is exactly one of its names), and a transition's `from` and `to` name states of the recipe | Write the form the message names |
| `RCP-SCHEMA-003` | No refused YAML (see Reading) | Write the value out in full |
| `RCP-STATE-001` | No state uses a reserved name, and the recipe has an active or wait state and a terminal state | Rename the state |
| `RCP-NAME-001` | Every state, loop and parameter (`with:` key) name matches `[a-z][a-z0-9-]{0,63}`; each bad name is reported once, where it is written | Rename it |
| `RCP-INIT-001` | `initial` names an active or wait state | Point `initial` at one |
| `RCP-DUR-001` | Durations are `<n>m/h/d`, at least `1m` | Write a whole number and m, h or d |
| `RCP-BLOCK-001` | Every block a recipe names is registered | Name a registered block, or fix the id |
| `RCP-BLOCK-002` | A block is used where its kind fits (gates under `gates`, Steps under `step`, …) | Name a block of the right kind |
| `RCP-BLOCK-003` | `with` names only the block's parameters, with the right types, and every required one | Remove, retype or add the parameter |
| `RCP-EVID-001` | An evidence kind has a registered provider (a bad `at` or `expect` is `RCP-SCHEMA-002`) | Name a provided kind, or register the provider |
| `RCP-EVENT-001` | A waited-for event is registered | Wait for a registered event |
| `RCP-WIRE-001` | A block's inputs are produced on every path into its state, or are recipe inputs | Produce it on every path, or add it to `inputs` |
| `RCP-WIRE-002` | `produces` lists only what the trigger's source records | List only its outputs |
| `RCP-WHEN-001` | `when: { outcome }` names an outcome the source reports | Test one of its outcomes |
| `RCP-WHEN-002` | `when: { loop }` names a declared loop, and `when: { outcome }` isn't used on a trigger that reports no outcome | Declare the loop |
| `RCP-OUTCOME-001` | Every outcome a Step, event or child reports is handled by a transition, and terminal outcomes are `resolved`, `unresolved`, `alreadySatisfied` or `failed` | Add a transition for it, or use one of those outcomes |
| `RCP-CHILD-001` | A child recipe resolves (registered, or next to the recipe) and gets its inputs | Register it, or put `<id>.yaml` beside the recipe; pass the inputs it declares |
| `RCP-CHILD-002` | Child recipes form no cycle | Remove a child wait |
| `RCP-CHILD-003` | Children nest at most 4 deep | Flatten the chain |
| `RCP-CAP-001` | **Generated recipes only** (run against the base; `recipe validate` doesn't): a generated recipe adds no capability use its base doesn't have | Remove it, or generate from a base that has it |
| `RCP-CAP-002` | **Generated recipes only:** a generated recipe keeps its base's capability parameters | Set them to the base's values |
| `RCP-CAP-003` | The recipe's hash is approved and its pinned blocks match the build (registry, P2) | Approve it |
| `RCP-CAP-004` | A hand-written recipe has no `base` | Remove `base` |
| `RCP-CAP-005` | **Generated recipes only:** a generated recipe keeps every guard its base puts on a capability use | Restore the guard |
| `RCP-CAP-006` | **Generated recipes only:** a generated recipe raises no loop cap or budget above its base | Lower it to the base's |
| `RCP-CAP-007` | A capability parameter is a literal or `{ profile }`, never a fact | Give a literal or `{ profile: <key> }` |
| `RCP-REACH-001` | Every state is reachable from `initial` | Add a transition into it, or remove it |
| `RCP-EXIT-001` | Every non-terminal state has a path to a terminal state | Add a transition towards a terminal state |
| `RCP-TIME-001` | Every active and wait state has a timeout | Add `timeout: { after, then }` |
| `RCP-TIME-002` | A child wait doesn't `retry` | Use `park` or `escalate` |
| `RCP-LOOP-001` | A state's `loop` is declared, every loop's `cap` is at least 1, and every cycle of step and wait transitions passes through a state with a `loop` (a cycle closed by a `command.<verb>` or by the answer to an `answer` wait is a human's, and a cycle with no way out is `RCP-EXIT-001`'s) | Declare it under `loops`, or add `loop` to a state on the cycle |
| `RCP-LOOP-002` | A looped state has a transition back into it | Add it, or remove `loop` |
| `RCP-LOOP-003` | `noProgress` isn't empty | Name a signal, or leave it out |
| `RCP-TRIG-001` | `on` is a known trigger, a command verb matches the pattern, and `on` fits the `from` state's kind | Use the trigger the message lists |
| `RCP-TRIG-002` | A `command.<verb>` isn't a reserved verb | Choose another verb |
| `RCP-THRESH-001` | A judge or model gate has a threshold | Add `threshold` in (0, 1] |
| `RCP-THRESH-002` | A threshold is in (0, 1], and a deterministic gate has none | Fix or remove it |
| `RCP-NEXT-001` | `next` uses only recipe variables | Use only the listed variables |
| `RCP-NEXT-002` | `next` names no approval-tier command | Leave it out |
| `RCP-EFFECT-001` | Every effect is on the profile's effect allowlist. **Skipped** while the profile has no `workflow.effects` (P6) | Add it to `workflow.effects` |
| `RCP-RES-001` | **Information:** a resource wait has no allocator in this build | None needed; it parks with `SND-WF-010` until one lands |
| `RCP-HASH-001` | A `hash` in the file matches the content | Set it to the hash the message names, or remove it |
| `RCP-VERSION-001` | Same id and version, same hash (registry, P2) | Raise `version` |
| `RCP-VERSION-002` | `version` is above the highest registered (registry, P2) | Raise `version` |

Rows marked P2 need the registry and aren't checked by `recipe validate` yet; neither are the generated-recipes-only rows,
which need the base recipe. Each message names the node and the
concrete fix, so the table's Fix column is a summary.

## How to add a block

A block is a class with a static descriptor, registered explicitly (no assembly scanning).

1. **Write the test first** in `tests/Aw.Sindri.Tests/Workflow/` (by feature): what the block decides, for each outcome.
   Its contract tests derive from `BlockContract` in `Aw.Sindri.Testing`, which runs once per block; that base arrives
   with the engine (P3/P4), so until then test the block directly.
2. **Write the block.** Pure blocks go in `src/Sindri/Aw.Sindri.Features/Blocks/<Kind>/`, blocks that do I/O in
   `Aw.Sindri.Infrastructure/Blocks/<Kind>/`. It implements its kind's interface (`IGate`, `IStep`, `IEffectKind`,
   `IEvidenceProvider` or `IStepInput`, in `Aw.Sindri.Core.Workflow.Blocks`) and `IDescribedBlock`:

   ```csharp
   internal sealed class BudgetAvailableGate : IGate, IDescribedBlock
   {
       public static BlockDescriptor Descriptor { get; } = new(
           BlockId.From("gate.budget.available"), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
           Capability.None, Parameters: [], Rung.Deterministic, DeskCapable: false);

       public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken) => …;
   }
   ```

   The descriptor is what the validator checks recipes against: the facts it needs (`Inputs`), what it records and on
   which outcomes (`Outputs`), its outcomes, its capabilities and parameters, a gate's rung, and whether a Step may run
   at the desk. A block over a lower-tier package (Judge, Evidence, Packets, BugFix) reaches it through that package's
   `Core` or CLI, under a declared `deps.yaml` edge.
3. **Register it** in `BlockRegistrations.AddRecipeBlocks` (`Aw.Sindri.Features/Blocks/`) with `AddGate<T>()`,
   `AddStep<T>()`, `AddEffect<T>()`, `AddEvidence<T>()` or `AddStepInput<T>()`; an I/O block is registered by
   `AddSindriInfrastructure`. An event a wait can wait for is `AddEvent(descriptor)`. A block whose runtime comes in a
   later phase can ship as `AddBlockDescriptor(descriptor)`, so recipes that name it validate; at run time it is
   unavailable. Add it to `ShippedBlocksTests`, which pins the catalog.
4. **Use it in a recipe** and run `sindri recipe validate` on it.
5. **A behaviour change bumps `Version`** in the descriptor, in the reviewed change that makes it. Once approvals pin
   blocks (P2), a changed descriptor parks the instances of every recipe approved against the old one until it is
   re-approved (`SND-RECIPE-006`).
