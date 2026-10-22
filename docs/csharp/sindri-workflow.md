# Sindri workflow engine (C#) — recipes and blocks

A **recipe** is a YAML state machine that Sindri's workflow engine runs for an item; a **block** is the C# code a
recipe names (a gate, a Step, an effect, an evidence provider or a Step input). This page is the reference for writing
both. The design and its reasons are in the engine spec,
`docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (§2 blocks, §3 the schema, §9 the validator); where
this page summarizes, the spec wins. The app shell (the `sindri` binary, its home, the error catalogue) is in
`docs/csharp/sindri.md`.

This page covers reading, normalizing, hashing and validating recipes in-process (P1) and the pure engine's eval verb,
`recipe eval` (P3). Registering and approving recipes (P2) and running instances on Temporal (the recipe interpreter,
desk Steps and the bridge, P4; engine spec §0 and §5, revision 8) come in later phases.

## `sindri recipe validate`

```
sindri recipe validate <file> [--json] [--against <manifest.json>]
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
- **`--against <manifest.json>`** (setup-once SO-E1) checks the file against a saved `recipe manifest --json` output as if its recipes were
  registered: the same id and version with another hash is `RCP-VERSION-001`, and a version below the highest in the manifest that is not in it is
  `RCP-VERSION-002`; a recipe the manifest lacks passes. This is the rule **"a content change bumps the version"**: CI runs `recipe manifest` over
  `git archive origin/main recipes/sindri` and validates every recipe of `git archive HEAD recipes/sindri` against that output
  (`scripts/ci/recipe-versions.sh`), and no manifest is committed anywhere. A manifest file that is missing or is not a manifest is `SND-RECIPE-010`
  (exit 2). `--against` may come before or after `--json`.
- **Child recipes** resolve from the recipe file's directory (`<child id>.yaml`), so `recipes/sindri/workflow.item.yaml`
  finds `workflow.step.yaml`. Once the registry exists (P2), a served validation resolves them from the registered recipes.
- **Hand-written only:** `recipe validate` passes no base recipe, so it checks every recipe as hand-written: a `base`
  field is `RCP-CAP-004`, and the generated-recipe rules (`RCP-CAP-001`, `-002`, `-005`, `-006`) don't run. Those will run
  when Sindri's generator checks a generated recipe against its base (the packet freeze, W9); no command runs them in this build.
- **The profile:** `RCP-EFFECT-001` checks the **approved** profile's effect allowlist (`workflow.effects`, P6): an
  effect off it is an error. It is reported as `skipped` only when no profile is approved or the approved one has no
  `workflow.effects` (an empty list allows nothing). `recipe validate` reads the approval and writes nothing.

| Exit | When |
|---|---|
| 0 | No finding is an error (information and skipped findings don't count) |
| 1 | One or more errors |
| 2 | A usage error (no file: `SND-CLI-003`; an unknown `recipe` verb: `SND-CLI-001`), or a tool error: the file doesn't exist, is a directory or can't be read (`SND-RECIPE-010 <path> doesn't exist`, `… is a directory, not a recipe file` or `… can't be read: <reason>`, each with its fix), or a bug inside `sindri` (`SND-API-005 recipe validate failed inside sindri: …`) |

Errors go to stderr as `CODE message (fix: …)`, or `CODE message (usage: …)` for a usage error, like every `sindri` verb.


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

## recipe eval

`sindri recipe eval <file> [--json]` runs a recipe's eval cases (spec §9.2) in this process, never contacting the
control plane. It reads the recipe and its child recipes beside it, then folds the pure engine's decisions (P3) over
each case listed in `evals:`, in order, with fake gates; the decisions read no clock. From P4 the same cases also run
through the interpreter on Temporal's time-skipping test server, and the two must agree (spec Phase 3). Each case's path is relative to the recipe
and stays inside its directory (`evals/<recipe id>/<case>.jsonl`).

A case file is JSON lines, LF or CRLF:
- line 1: `{"start":{"inputs":{"<fact>":"<value>",...}}}` (`"scaffold": true` marks a generated case);
- each later line: `{"trigger":{"kind","outcome"?,"outputs"?,"park"?,"variant"?,"amount"?,"reason"?},"gates"?:{"<block>":"pass|fail|uncertain"},"expect":{"state","park"?,"outcome"?}}`.

`kind` is a recipe trigger (`step.completed`, `step.failed`, `wait.satisfied`, `command.<verb>`), a child's change
(`child.parked` with `park`, `child.resumed`, `child.escalated`; a child's end is `wait.satisfied` with its outcome),
or an engine trigger: `answer` with a `variant` (`grant`, `grant-tokens`, `accept`, `reject`, `resume`, `text`; the
grants take `amount`, a `text` answer's text is `outputs` `fact:answer`), `takeover`, `handback`, `cancel` and
`deadline`. An answer wait is satisfied by an `answer` line with `variant` `text` (outcome `answered`), not by
`wait.satisfied`. A child's `wait.satisfied` line may give `outputs`: only the child's declared outputs, under the child's keys; a key it doesn't declare, or any on `cancelled`, fails the line. `--scaffold` fills a child line with the outputs its outcome always carries. A `deadline` line fires the running timer: `takeoverIdle` while taken over, the retry timer after a `then: retry` re-armed the state, otherwise the state timer. It fails when no timer runs (parked, escalated or ended).

A `step.failed` line takes an optional `reason`, what the Step reported as its failure. Left out, it is the constant
`the Step failed`, the same on every line, so two `step.failed` lines in a loop are the same failure and park
`no-progress` when the loop names `sameFailure` (spec §3.7). Give different reasons to make a round count as progress.
`reason` on any other kind fails its line. `--record` writes each failure's reason from the event, so a recorded
`sameFailure` park replays. `reason` isn't in the spec's §9.2 field list yet; the owner has been asked to add it there,
and this page documents it in the meantime.

Output, one line per case:

```
$ sindri recipe eval recipes/sindri/workflow.step.yaml
happy pass
no-progress pass
rounds pass
$ sindri recipe eval <copy with line 3 of happy.jsonl expecting checks>
happy fail at evals/workflow.step/happy.jsonl:3: expected checks, got verify
no-progress pass
rounds pass
```

A line the engine rejects (an answer that doesn't fit the park, a grant of 0 or above the loop's cap, an undeclared
outcome) or one out of the format (not JSON, an unknown `kind`, a `variant` that doesn't go with its kind) fails its case
at that line, saying why. A recipe without `evals:` prints `no evals declared` and exits 0: nothing failed. Approving
such a recipe is refused (`SND-RECIPE-008`, see `sindri recipe approve` below). `--json` prints `{"cases":[{"case","passed","line","detail"}],"scaffold":null,"passed"}`, where `"case"` is the eval file's path as `evals:` lists it (`evals/workflow.step/happy.jsonl`); the text output shows the bare name (`happy`).
The same eval run twice prints the same output.

`--scaffold <file>` prints a happy-path case for the recipe on stdout, marked `"scaffold": true` on line 1: the start
with a placeholder for each input, then, from the initial state, the first of each state's declared outcomes whose
transition moves into a state not yet visited, until a terminal state. Every gate passes, and each line expects what the
engine did, so it passes once it is in the recipe's `evals:`. `--record <address> --out <file>` (engine P7) writes a served instance's history in the same format, read from the ledger and the stored facts without contacting serve: the recipe is the address's recipe as approved now. A child's park, resume and escalation come back as `child.parked`, `child.resumed` and `child.escalated`, and its end as `wait.satisfied` with the child's outcome and the child's declared outputs the parent kept under the child's own keys (a renamed output is not recorded under the rename). A trigger an eval line can't name (a held event's expiry, a drift wake) refuses the recording. It writes nothing when the file exists or the ledger has no event of the address (`SND-WF-001`).

A recipe with a route state (a `step.judge.choose`, see "Writing a decision tree") has a path per choice, plus `uncertain` and `step_failed` (the Step failing); `--scaffold <file> --out <dir>` writes each as `<dir>/scaffold-<branch>.jsonl` and prints the paths. It refuses when any of those files exists (`SND-CLI-003`) and writes nothing then.

| Exit | When |
|---|---|
| 0 | Every case passes, or the recipe declares none |
| 1 | A case fails (`<case> fail at <file>:<line>: <detail>`) |
| 2 | A usage error (no file, `--record` without an address and `--out`: `SND-CLI-003`; an unknown argument: `SND-CLI-001`; `--record` of an address the ledger never saw: `SND-WF-001`; an `--out` file that exists: `SND-CLI-003`), or a tool error: the recipe can't be read (`SND-RECIPE-010`) or doesn't bind (`SND-RECIPE-001`, fix: `sindri recipe validate`), a listed eval file is missing, unreadable or outside the recipe's directory (`SND-RECIPE-008`), or a bug inside `sindri` (`SND-API-005 recipe eval failed inside sindri: …`) |

## `sindri recipe manifest` and `recipe eval --shipped`

```
sindri recipe manifest <dir> [--json]
sindri recipe eval --shipped [--json]
```

Both are local (setup-once spec §5.3, SO-E1): they run in the calling process, never start or contact `sindri serve`, and write nothing.

**`recipe manifest <dir>`** reads `<dir>` as a recipe set laid out like `recipes/sindri` (its flat `*.yaml` files and `evals/<recipe>/*.jsonl`; any other file or
directory, such as the README, is left alone) and prints one line per recipe, **sorted by id (ordinal)**, with the registry's own hash function (the reader's):

```
workflow.build@2 → sha256:<64 hex> (3 eval cases)
workflow.line@2 → sha256:<64 hex> (6 eval cases)
digest sha256:<64 hex>
```

A recipe that does not read prints `invalid <file>: <first finding>` instead, and there is no digest line. `--json` prints one document:
`{"recipes":[{"id","version","hash","displayName","evalCases","evalsHash"}],"invalid":[{"file","finding"}],"digest"}`. `evalsHash` is the SHA-256 over the recipe's
eval files (path, length and bytes, by path), so an eval-only change changes the manifest; `digest` is the SHA-256 over the lines `id@version TAB hash TAB evalsHash`
in output order and is `null` when `invalid` is not empty; `displayName` is `null` for a recipe without one (it is also `RCP-DISPLAY-001` at validate). The publish
compares this output with the previous publish's to decide whether to run the evals; the running app never reads it.

| Exit | When |
|---|---|
| 0 | Every recipe read |
| 1 | A recipe did not read (the rest are still listed) |
| 2 | A usage error (no directory, more than one, an unknown flag: `SND-CLI-003`), or the directory cannot be read (`SND-RECIPE-010`) |

**`recipe eval --shipped`** runs the eval cases of every recipe in the **embedded set** (`IEmbeddedRecipes`, below) over the pure engine, exactly as `recipe eval <file>` runs
one file, with the embedded siblings as the children and the embedded `evals/…` files as the cases. One header line per recipe (`id@version`), one indented line
per case (`happy pass`, or `happy fail at <file>:<line>: <detail>`), then `26 recipes passed (80 cases)` or `1 case failed in 1 recipe`. A recipe that does not read, whose
eval file is not in the set, or that declares **no** evals fails (approval needs evals declared, so a shipped recipe must have them). An empty embedded set passes and says
`no recipes are embedded in this build; nothing to evaluate`. `--json` prints `{"passed","empty","recipes":[{"id","version","hash","displayName","passed","cases":[{"case","passed","line","detail"}],"error"?}]}`
(`version` and `hash` are `null`, and `error` is set, for a recipe that did not read). Exit 0 pass, 1 a case fails, 2 usage or a bug inside `sindri`.

**`IEmbeddedRecipes`** (Core port; the Host adapter `ManifestEmbeddedRecipes` reads the manifest resources named `aw-recipes/sindri/<relative path>` in `Aw.Sindri.Host`) returns one list of
`(relativePath, bytes)`, relative to `recipes/sindri`, sorted by path: flat `*.yaml` and `evals/<recipe>/<case>.jsonl` (anything else under the prefix, the README, is left out).
The publish embeds `git archive HEAD recipes/sindri` there (SO-A7a); until it does, and in any build that stages nothing, the set is empty, and an empty set is legal.
Nothing at run time reads `recipes/sindri` from disk. `FakeEmbeddedRecipes` is the test fake; both pass `EmbeddedRecipesContract`.

## `sindri recipe register`, `show` and `list`

`sindri recipe register <file>` (`desk`; starts the control plane if it may) validates the recipe on the server with the
registry's view (`RCP-VERSION-001/002`, and `RCP-CAP-003` for a generated recipe's base) and stores its normalized long
form under its hash: `registered <id>@<hash12> (unapproved)`, or `already registered …` for a hash it has (one
`recipe.registered` event per hash). Other content under a registered id and version is `SND-RECIPE-007`; any other
failing rule is `SND-RECIPE-001`, each followed by its rule lines.

`sindri recipe show <id|id@hash> [--json]` (a query; a bare id is the approved version, a hash is 8 or more digits) prints the recipe's `<id>@<hash12>`, then `urgency: <urgency>`, `name: <template>` when the recipe has a `name`, one
`input: <key> type=<type> kind=<kind|-> [generate="<template>"]` line per input (the type is the declared one, else the one the kind pairs with; a row registered before input kinds shows `kind=-`) and one `output: <key> type=<type> from=<state>` line per output, then the path order (below) and one `loop: <name> <from> -> <to> cap=<n>` line per loop;
an unknown id or hash is `SND-RECIPE-002`, an id with no approved version `SND-RECIPE-003`. The standing, the approval and the approval preview are not in `show`: `recipe list` has the standing and `recipe approve` prints the preview before it asks. `show --json` prints the served `RecipeView`, `{id, hash, version, name, inputs, outputs, urgency, orderedBy, states, loops}`, where an input is `{key, label, type, kind, required, generate}` (`kind` is `-` on a legacy row, `required` is false for an input with a `generate` template) and an output is `{key, label, type, from}`; the standing, `via` and `stale` are `recipe list` fields, not part of the view. `sindri recipe list [--stale] [--json]` prints one line per registered recipe,
`<id>@<hash12> <standing> (<via>)[; stale: …]`, where standing is `approved`, `unapproved` or `revoked` and stale names
`pin-drift` (a pinned block's descriptor changed in this build), `child-stale` (a pinned child was revoked or has a newer approved version) or `legacy-shape` (registered before input kinds; the line's fix is `register the current file at a new version and approve it`, and an unapproved row of that shape carries the mark too);
`--stale` keeps only the approved ones that are stale. `--json` prints the server's body,
`{"recipes":[{id, version, hash, standing, via, stale, inputs, urgency, name, outputs, orderedBy, states, loops}]}`, where an input is `{key, label, type, kind, generate}`
(`type` `null` when not declared, `kind` `null` only on a legacy row, `generate` `null` when the input has no template; `name` is the instance-name template or `null`) and an output is `{key, label, type, from}`. A row registered before `urgency`, `outputs`, `name` and the inputs' `kind` and `generate` existed keeps its stored text and hash and lists with the defaults (`work`, no outputs, no name, no kinds); registering the same
file again hashes differently and is `SND-RECIPE-007` naming `RCP-VERSION-001` until the author raises `version`. Such a row (an input with no `kind`, or no input: `RCP-INPUT-001`/`-002`) is *legacy-shape*: it can't name a subject, so a start on it is `SND-RECIPE-001` (checked before a pin-drift refusal, since re-approving can't fix it), a first approval of it is `SND-RECIPE-001` too (before the no-evals check; a row already approved can still be re-approved or woken, so a parked instance resumes), and an instance already running on it keeps running unnamed, with the commands it made before (engine spec §3.11, "A pre-P4c instance"). Revoking it works as for any row. The queries never start the control plane: when it
isn't running they print `SND-API-001 not running: …` and exit 1.

**Path order (spec §3.10, UI E-20).** `recipe show` and `recipe list --json` carry the recipe's states in path order, computed from the
transition graph by `StatePathOrder` and never hashed (a recipe's hash is the same with or without it): `orderedBy` (`path`, or
`transition-list` when `initial` has more than one distinct non-terminal first target other than itself), `states[]` (`id`, `order`, `branchOf`) and
`loops[]` (`name`, `from`, `to`). Text form: `order: <orderedBy>`, one `state: <name> order=<n>|branch-of=<state>|order=-` line per state and
one loop line per loop edge: `recipe list` prints `loop: <name|-> <from> -> <to>` (every loop edge), `recipe show` prints `loop: <name> <from> -> <to> cap=<n>` (named loops with a cap only). The walk starts at `initial` and follows each state's transitions in list order:
the first to a non-terminal state not yet seen continues the main path, a later one is a branch and every state only that branch reaches is folded
under the state it leaves (`branchOf`, no `order`); a transition back to a state still on the walk is a loop edge, named by the target's
`loop:` (the loop the engine counts the round on; else the looping state's), listed in the order the walk finds them. A terminal target neither takes the main-path slot nor decides `orderedBy`; terminal states are numbered after the non-terminal ones in
the order the walk found them, wherever they were reached from; a state the walk never reaches is listed last with neither. `order`
starts at 1 (WF spec §3.10; the API spec's StateCell says 0-based, an open cross-spec question), set in one place
(`StatePathOrder.FirstOrder`).

## `sindri recipe approve` and `sindri recipe revoke`

Both are `protected` (engine spec §4.2, §6.2, §6.3): only a human at a terminal gets past the command chain; a session
or a CLI without a terminal gets `SND-AUTH-002` naming the agent variable or process it saw, and nothing is recorded but
the audit row.

`sindri recipe approve <file|id@hash>`:

1. With a file, the CLI validates it in-process (`SND-RECIPE-001` if it fails) and checks every eval file its `evals:`
   names is present beside it (`SND-RECIPE-008` otherwise; P4 runs them at approve). It registers the file if the server
   doesn't know it yet.
2. It asks `POST /sindri/workflow/challenges` for `approve <id> <version>` and prints the preview: id, version, hash and
   base; the diff against the last approved version of the id (`nothing` for a first approval); the capability uses
   (block, `from -> to`, capabilities and capability parameters); the effect kinds and their allowlist status; the pinned
   blocks with their descriptor hashes and the pinned children; the eval files. A re-approval of an approved hash also
   shows the blocks whose descriptor changed and the drift-parked instances it resumes.
3. You type the action text, `approve workflow.step 1`. With a file, the CLI re-reads it first and refuses with
   `SND-RECIPE-009` if it changed while you read.
4. It prints `approved workflow.step@<hash12>`.

The server refuses before any challenge when the hash is revoked (`SND-RECIPE-005`), the recipe declares no evals
(`SND-RECIPE-008`) or a child it starts has no approved hash to pin (`SND-RECIPE-003`). An approved hash approved again
with the same pins is not refused: it records nothing, sends the approve wake to its running instances again and prints
`already approved workflow.step@<hash12>; woke N instances` (exit 0). That is the remedy for a drift-parked instance a first
wake missed (the wake failed with `SND-API-001`, or it was absorbed before the drift park existed). A wrong typed text, a challenge for another action, an expired one (5 minutes) or one whose
subject changed (another hash, or other pins: a child approved while you read) is `SND-RECIPE-009` and records nothing.
A generated recipe whose base was revoked after it was registered is refused too (`SND-RECIPE-005`). With a file whose
id and version are registered with other content (edited without raising its version), the CLI refuses before the preview
with the register call's `SND-RECIPE-007` and its `RCP-VERSION-001` line.

`sindri recipe revoke <id@hash>` (the hash by 8 or more digits) previews the instances that will park, the takeovers it
ends, the generated recipes approved from it (revoked with it, recursively) and the parents pinning it as a child
(marked `child-stale`), asks for `revoke <id>@<hash12>`, and prints `revoked …; N instances parked` (N counts the running instances
the registry wake signalled; see "Registry wake (P6)"). Revoking an already revoked hash is `SND-RECIPE-005`, unless a revoke stopped partway through
its cascade: then the same command finishes it (each step is keyed by its hash, so nothing is recorded twice).

Each approval appends `recipe.approved` (keyed `recipe.approved:<hash>:after:<seq>`) and an approval row keyed by
(id, version, hash) with its pins; each revoked hash appends `recipe.revoked` (keyed `recipe.revoked:<hash>`) and its row,
and is sent to `IRecipeWake`. Challenges live in the server process and die on restart.

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
| `inputs: [{ key: fact:item, kind: item }]` | `inputs: [{ key: fact:item, label: null, type: null, kind: item, generate: null }]` |
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
| `displayName` | yes | The name the app shows instead of the id (setup-once SO-10): sentence case, at most 60 characters, what the recipe does (`Idea to merged pull request`), no id or code name (`RCP-DISPLAY-001`). It is part of the hash, so adding or changing it needs a new `version`: **a content change bumps the version** (`recipe validate --against`) |
| `base` | generated recipes only | Written by Sindri's generator; on a hand-written recipe it is `RCP-CAP-004`, and `recipe validate` treats every recipe as hand-written |
| `urgency` | no | `incident`, `triage` or `work` (default `work`; UI E-21). The app orders its Workflows strip by it; the engine never reads it. The normalizer writes `work` when it is absent, so an absent urgency and `urgency: work` hash the same. Any other value is `RCP-URG-001` |
| `inputs` | yes | At least one (`RCP-INPUT-002`): `{ key, label?, type?, kind, generate? }`. `kind` is a subject kind and required (`RCP-INPUT-001`); `type` is a fact type and defaults to the type `kind` pairs with; `generate` is a template that supplies the value when the start gives none (text inputs only, `RCP-NAME-005`/`-006`). The bare-key short form (`[fact:item]`) normalizes to an input with no `kind`, so it fails `RCP-INPUT-001`. The first input is the instance's subject (UI E-22) |
| `name` | no | A template for the instance's name, rendered once at start: literals, `{fact:<input>}`, `{start:date}`, `{start:time}`, `{{` and `}}` (engine spec §3.11). The name is the subject's label. `RCP-NAME-002` to `-004` |
| `outputs` | no | What the recipe produces for a human: `{ key, label?, type?, from }`. `from` names the state that produces the fact. Declarations only: they add no wiring (UI E-22), except on a child: a child's declared outputs are the facts of its parent's `wait.satisfied` (DT-C) |
| `initial` | yes | An `active` or `wait` state (`RCP-INIT-001`) |
| `states` | yes | At least one non-terminal and one terminal state. `cancelled`, `escalated`, `parked` and `taken-over` are reserved (`RCP-STATE-001`) |
| `transitions` | yes | |
| `loops`, `budget` | no | |
| `evals` | to approve (P2) | Paths of eval files, relative to the recipe's directory (`evals/<id>/<case>.jsonl`, spec §9.2). The paths' form isn't checked yet |
| `hash` | no | Checked when present |

### Fact types and subject kinds

Fact type: `text`, `file`, `item`, `link`, `scope-map`, `packet`, `evidence`, `decision`, `payload` (`FactTypes`). It picks a viewer in
the app and nothing else. Subject kind: `item`, `linear-project`, `webhook`, `file`, `text`, `link`, `packet`, `payload` (`SubjectKinds`); `item`, `file`, `text`, `link` and `packet`
pair with the type of the same name, `linear-project`, `webhook` and `payload` with `payload` (owner decision 2026-10-16: a packet or a plain payload as a subject); an input with no `type` has its kind's. Both are strings in the long form, so a bad value
reaches its rule (`RCP-TYPE-001` to `-003`, `RCP-INPUT-001`/`-002`, `RCP-NAME-002` to `-006`, `RCP-OUT-001` to `-003`, `RCP-URG-001`) instead of a reader error. Those rules
(the Declarations rows under Rules) run on every path that validates a recipe, so a value of another spelling is refused with its rule's line.

### Value grammars

| Value | Form |
|---|---|
| Fact key | `fact:<name>` or `evidence:<kind>@<base\|head>` |
| Block id | `<gate\|step\|effect\|evidence\|input>.[<area>.]<name>`, kebab-case, at most 64 characters (`step.produce`, `gate.budget.available`; spec §3.3) |
| Event kind | `<area>.<name>`, registered with `AddEvent` (this build: `claim.created`, `item.routed`, `notify.delivered`, and the PR babysitter's `pr.changed` (routed by item), `pr.pushed`, `pr.restacked`, `pr.mergeResult`, `pr.docsMerged`, `pr.stacked`) |
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

Any state may also carry two optional plain-words fields the app shows (setup-once SO-E1; each is written into the hashed form only when set):
`displayName` (sentence case, at most 60 characters, no id; `RCP-DISPLAY-003`) names the step instead of the state id, and `why` (one plain sentence, at most 200
characters, no id or code name; `RCP-DISPLAY-002`) says why the state is in the recipe. Neither is on a block descriptor.

`waitFor` is one of `{ event: <kind> }`, `{ answer: { id, question } }`, `{ resource: heavy | slot }` or
`{ child: { recipe, inputs, onParked: propagate | ignore } }`. The `heavy` resource is allocated over the box's heavy-job lock
(LN-WF3, see "The heavy resource" below); `slot` is a stub that is always free (P7), so a `slot` wait is reported as information
(`RCP-RES-001`). `then: retry`
re-arms the deadline in the same frame and re-sends the entering transition's effects; it isn't allowed on a child wait
(`RCP-TIME-002`), nor on a state whose Step runs on the block runtime, whose timeout must also outlast the Step's `maxDuration` (`RCP-TIME-003`).

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
| `label` | no | Plain words for what the transition means, drawn on the arrow (`gaps found: draft again`): at most 60 characters, no id or code name (`RCP-DISPLAY-003`). Written into the hashed form only when set |
| `produces` | no | Facts the transition records; each must be an output of the trigger's source (`RCP-WIRE-002`; for a child, one of its declared `outputs`). An entry is a fact key, or `{ fact, from }` to record a child's output under another key (DT-C) |

The first transition whose `when` holds is the one taken. If its gates refuse, that is a refusal, not a fall-through to
the next transition.

### `when`

A closed set: `{ outcome }`, `{ fact, is: present | absent }`, `{ loop, below: cap }`, `{ all: [...] }`, `{ any: [...] }`
and `{ not: ... }`. Outcomes come from the trigger's source: the Step's declared outcomes, the event's, or a child's
terminal outcomes plus `cancelled` (`RCP-WHEN-001`, whose message ends with `(outcomes here: <list>)`). A choice Step's outcomes are its `choices` names plus the Step's own (`uncertain`); see "Writing a decision tree". A loop must be declared (`RCP-WHEN-002`).

### Loops, budgets and `next`

- **Loops:** `loops: { <name>: { cap ≥ 1, noProgress } }`. `noProgress` defaults to `[findings, emptyDiff, sameFailure]`
  and can't be empty (`RCP-LOOP-003`). A looped state needs a transition back into it (`RCP-LOOP-002`).
  **No-progress (spec §3.7, A §17.23.13, P8):** a round made no progress over the last when a signal the loop names fires.
  `findings`: a report's `fact:findings` that is a JSON list of `{file, check, message}` gets one key per finding (the
  SHA-256 of `file|check|message`, the file with `/` separators, the message with whitespace collapsed and case dropped),
  and the signal fires when the round still has every key of the last round (a superset, or the same set). Findings in any
  other shape have no keys and keep the whole-value digest rule (fires only when identical). `emptyDiff`: fires when the
  engine measured an empty diff on an agent run (the worktree's HEAD is the start HEAD and `git status --porcelain` is
  empty); sessions never report it, and desk and block reports leave it false. `sameFailure`: a `step.failed`, or a gate
  refusal, repeats its reason. The first round has nothing to compare with.
- **Budgets:** `budget: { tokens }` on an active state and on the recipe. Exhausting one parks the instance with `budget`.
  An agent Step is charged its fresh input, cache writes and output; **cache reads are not counted** (a session re-counts them every turn),
  though `session.usage` still carries them as `cacheReadTokens` (absent when none). A report the engine accepted is **never discarded for its cost**:
  its transition applies and the spend is recorded, and the park falls where the next Step would start (`budget`, answered with `--grant-tokens`,
  which starts that Step without counting a round). A run that fails over budget still parks on it.
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
| `RCP-SCHEMA-002` | Each value has the right type and form (an enum is exactly one of its names), and a transition's `from` and `to` name states of the recipe. A fact key written with hyphens (`fact:evidence-summary`) gets the camelCase fix | Write the form the message names; fact names are camelCase (`fact:evidenceSummary`) |
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
| `RCP-WIRE-002` | `produces` lists only what the trigger's source records (for a child, a declared output); only a child's output is renamed; no fact is written twice on one transition | List only its outputs |
| `RCP-WIRE-003` | A transition from a child wait produces a child output only on outcomes whose every path inside the child produces it, never on `cancelled`, and never on a `when` that names no outcome (it matches every outcome, `cancelled` among them) | Produce it on that path in the child, or `when: { outcome: <o> }` an outcome that always carries it |
| `RCP-WHEN-001` | `when: { outcome }` names an outcome the source reports; the message lists the outcomes available there (`outcomes here: ui, api, uncertain`) | Test one of its outcomes |
| `RCP-WHEN-002` | `when: { loop }` names a declared loop, and `when: { outcome }` isn't used on a trigger that reports no outcome | Declare the loop |
| `RCP-OUTCOME-001` | Every outcome a Step, event or child reports is handled by a transition, and terminal outcomes are `resolved`, `unresolved`, `alreadySatisfied` or `failed` | Add a transition for it, or use one of those outcomes |
| `RCP-CHOICE-001` | A `step.judge.choose` state's `choices` has 2 to 32 entries | List 2 to 32 choices, each `"<outcome>: <criterion>"` |
| `RCP-CHOICE-002` | `choices` is a literal list (not `{ fact }` or `{ profile }`) and each entry is text `"<outcome>: <criterion>"`: the outcome is `[a-z][A-Za-z0-9]{0,31}` (camelCase), the criterion 1 to 400 characters. An unquoted `- ui: ...` is a YAML mapping and gets the quote fix | Quote the entry; write the choices as a literal list |
| `RCP-CHOICE-003` | Outcome names are distinct, and none is `uncertain` (the Step adds it) | Give each choice its own outcome |
| `RCP-CHOICE-004` | `facts` is a literal list of at least one fact key; a hyphenated key gets the camelCase fix | List the facts the question reads |
| `RCP-CHOICE-005` | Every route state routes `uncertain`, if it does, to a `step.desk.choose` state whose `from` names it, and has a `step.failed` transition to that twin; a twin's `from` is a literal naming a route state (not `{ fact }` or `{ profile }`) | Route to the desk twin; set `from:` to the route state |
| `RCP-CHOICE-006` | A route state's `uncertain` and `step.failed` transitions come after its branch transitions (the app takes the first transition as the main path) | List the branches first |
| `RCP-CHILD-001` | A child recipe resolves (registered, or next to the recipe) and gets its inputs | Register it, or put `<id>.yaml` beside the recipe; pass the inputs it declares |
| `RCP-CHILD-002` | Child recipes form no cycle | Remove a child wait |
| `RCP-CHILD-003` | Children nest at most 5 deep | Flatten the chain |
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
| `RCP-TIME-003` | A state whose Step's descriptor says `runtime: block` (whether or not this build registers a runtime for it) doesn't `retry`, and its `timeout.after` is longer than the Step's `maxDuration` (two minutes when the descriptor sets none) | Use `then: park` (a block call that runs out ends as `step.failed`, which the route sends to its desk twin); make the state timeout longer than the Step's `maxDuration` |
| `RCP-LOOP-001` | A state's `loop` is declared, every loop's `cap` is at least 1, and every cycle of step and wait transitions passes through a state with a `loop` (a cycle closed by a `command.<verb>` or by the answer to an `answer` wait is a human's, and a cycle with no way out is `RCP-EXIT-001`'s) | Declare it under `loops`, or add `loop` to a state on the cycle |
| `RCP-LOOP-002` | A looped state has a transition back into it | Add it, or remove `loop` |
| `RCP-LOOP-003` | `noProgress` isn't empty | Name a signal, or leave it out |
| `RCP-TRIG-001` | `on` is a known trigger, a command verb matches the pattern, and `on` fits the `from` state's kind | Use the trigger the message lists |
| `RCP-TRIG-002` | A `command.<verb>` isn't a reserved verb | Choose another verb |
| `RCP-THRESH-001` | A judge or model gate has a threshold | Add `threshold` in (0, 1] |
| `RCP-THRESH-002` | A threshold is in (0, 1], and a deterministic gate has none | Fix or remove it |
| `RCP-NEXT-001` | `next` uses only recipe variables | Use only the listed variables |
| `RCP-NEXT-002` | `next` names no approval-tier command | Leave it out |
| `RCP-EFFECT-001` | Every effect is on the approved profile's effect allowlist. **Skipped** while no profile is approved or it has no `workflow.effects` | Add it to `workflow.effects` and approve the profile |
| `RCP-HUMAN-001` | An effect whose block is `humanTrigger` runs only off a human's act: a `command.*` transition, a `step.completed` out of states that each run a `humanOnly` Step, or a `when` that requires a fact (`{fact: F, is: present}`, at the top or inside `all`) that only such command transitions produce. The initial state never counts | Move the effect onto a command transition or a human-only Step, or gate it on a fact a human's command produces |
| `RCP-RES-001` | **Information:** a `slot` wait has only the stub allocator in this build (`heavy` has the real one, LN-WF3 and is no longer reported) | None needed |
| `RCP-URG-001` | `urgency` is `incident`, `triage` or `work` | Use one of them, or remove `urgency` (the default is `work`) |
| `RCP-OUT-001` | An output's `from` names a state of the recipe | Name one of its states |
| `RCP-OUT-002` | An output's `from` isn't a terminal or engine-owned state, and the state's Step, its leaving transitions' `produces` or its wait records the output's key. For a state that waits on a child, the child's declared outputs | Record the fact there, or name the state that does |
| `RCP-OUT-003` | Output keys are unique and none is also an input key | Give each output its own key |
| `RCP-TYPE-001` | An input's or output's `type` is a fact type | Use one of the nine types |
| `RCP-TYPE-002` | An input's `kind` is a subject kind | Use one of the eight kinds |
| `RCP-TYPE-003` | An input's `kind` pairs with its `type` (an absent `type` takes its kind's); not reported for an entry with a bad type or kind | Set `type` to the kind's, or change `kind` |
| `RCP-INPUT-001` | Every input declares a `kind` | Add `kind: item \| link \| linear-project \| webhook \| file \| text \| packet \| payload`, paired with its type |
| `RCP-INPUT-002` | The recipe declares at least one input | Declare one; a scheduled recipe gives it `generate: "<template>"` |
| `RCP-NAME-002` | A `name` or `generate` template isn't empty and is at most 200 characters | Write literal text with placeholders |
| `RCP-NAME-003` | A template has no unknown placeholder, unbalanced `{` or `}`, or control character | Use `{fact:<input>}`, `{start:date}`, `{start:time}`; write `{{` or `}}` for a literal brace |
| `RCP-NAME-004` | `{fact:x}` in `name` names a declared input | Declare it under `inputs`, or remove the placeholder |
| `RCP-NAME-005` | A `generate` template has no `{fact:…}` placeholder | Use only literals, `{start:date}` and `{start:time}` |
| `RCP-NAME-006` | `generate` is only on an input of kind `text` | Supply that input at start |
| `RCP-DISPLAY-001` | A recipe has a `displayName`: required, sentence case, at most 60 characters, no id or code name | Write the name the app shows (`Idea to merged pull request`) |
| `RCP-DISPLAY-002` | A state's `why` is one plain line of at most 200 characters, with no id or code name | Say in plain words why the state is there |
| `RCP-DISPLAY-003` | A state's `displayName` (sentence case) and a transition's `label` are at most 60 characters, with no id or code name | Use plain words |
| `RCP-HASH-001` | A `hash` in the file matches the content | Set it to the hash the message names, or remove it |
| `RCP-VERSION-001` | Same id and version, same hash (registry, P2; and `recipe validate --against <manifest.json>`: a content change bumps the version) | Raise `version` |
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
   A Step whose outcomes or inputs depend on its parameters sets `OutcomesFrom` and `InputsFrom`: a list parameter's name
   (`choices`), or `<stateParam>.<listParam>` for a twin that reads another state's Step (`from.choices`). Read outcomes
   and inputs through `StepOutcomes.Of` and `StepInputs.Of`, never from the descriptor directly: the validator, the report validation and the eval read through them (the desk's views still read the descriptor and move onto them in DT-V2). An output that every
   outcome must carry is `Every: true` (read it with `StepOutputs.RequiredOn`; what a caller must write, in the views, the commands and the eval scaffold, is `StepOutputs.CallerWritesOn` (`CallerWritesSomewhere` when the Step declares no outcomes, and for a fact row's required flag), which leaves out engine-filled outputs), and one the engine writes when a report
   omits it is `EngineFilled: true`. These descriptor fields are written only when set, so a descriptor without them
   keeps its hash; `ShippedBlocksTests` pins every shipped hash.

   Three more optional fields are written only when set: `Runtime` (`desk`, `block` or `agent`: who runs the Step; descriptor-only
   until DT-J, absent means today's rule), `HumanOnly` (true or absent, never false) and `HumanTrigger` (an effect that acts with a
   human's authority, checked by `RCP-HUMAN-001`; true or absent, and a false reads as absent). A Step is **human-only** when its descriptor
   says `HumanOnly`, or when its `Runtime` is `block` and it runs on the desk (`HumanOnlySteps.Applies`). `workflow complete` on a
   human-only Step is `SND-AUTH-002` unless the caller is a human (the app or a terminal): the refusal sits in `CompleteStep`, before
   the nonce is claimed, so nothing is consumed, and a session report on an instance that can't be read is refused with that read
   error, not let through. The message names the Step, its state and the command the human runs. Today `CompleteStep` is a desk-class command, so the command chain (`AuthorizingService`) refuses every session with its generic `SND-AUTH-002` before the service runs; this per-Step check is defence in depth, and takes over once a non-human class can reach `CompleteStep` (step reports open to sessions, P10).
4. **Use it in a recipe** and run `sindri recipe validate` on it.
5. **A behaviour change bumps `Version`** in the descriptor, in the reviewed change that makes it. Once approvals pin
   blocks (P2), a changed descriptor parks the instances of every recipe approved against the old one until it is
   re-approved (`SND-RECIPE-006`).

### Block catalog (decision-tree Steps)

Descriptor-only Steps (`AddBlockDescriptor`): recipes that name them validate, they run on the desk or are unavailable until their runtime
(DT-J, DT-B) ships. `evidence.check` is a descriptor-only evidence provider (runtime in DT-B): a recipe that guards a transition with it validates (no `RCP-EVID-001`), the engine refuses the transition at run time ("no runtime for evidence checks in this build"), and `recipe eval` still answers every evidence check as holding, since an eval line has no evidence answer yet (a known gap until DT-B). Facts are `fact:` keys. Outputs are
what a report must carry, on which outcomes; "every outcome" outputs are on all of them.

| Step | Runtime | Inputs | Outcomes | Outputs |
|---|---|---|---|---|
| `step.intake.ticket` | desk | - | `done` | `issue` on done; `expected` on done; `actual` on done |
| `step.rootcause.investigate` | agent | `issue`, `expected`, `actual` | `diagnosed`, `unfixed` | `rootCause` on diagnosed; `findings` on unfixed |
| `step.reproduce.ui-check` | agent | `issue`, `rootCause` | `reproduced`, `notReproduced` | `check` on reproduced; `baseline` on reproduced; `findings` on notReproduced |
| `step.reproduce.test-check` | agent | `issue`, `rootCause` | `reproduced`, `notReproduced` | `check` on reproduced; `baseline` on reproduced; `findings` on notReproduced |
| `step.bugfix.start-attempt` | block | `decision` | `single`, `split`, `capReached` | `attempt` on single,split |
| `step.bugfix.record-candidate` | block | `diff` | `recorded` | `candidate` on recorded |
| `step.bugfix.recheck` | block | `check`, `candidate` | `passed`, `failed` | `recheck` (every outcome) |
| `step.bugfix.judge` | block | `check`, `candidate`, `evidenceSummary` | `resolved`, `partial`, `unresolved`, `needsHuman` | `verdict` (every outcome) |
| `step.report.write` | agent | `issue` | `done` | `resolution` on done |
| `step.handtest.run` | agent | `phasePacket`, `workspace` | `works`, `broken` | `manualTest` (every outcome) |
| `step.verify.journey` | agent | `phasePacket`, `workspace` | `pass`, `fail` | `evidencePack` (every outcome) |
| `step.verify.design` | agent | `phasePacket`, `workspace` | `pass`, `fail`, `skipped` | `designCheck` on pass,fail |
| `step.evidence.ui-plan` | agent | `phasePacket`, `built` | `planned`, `noTarget` | `evidencePlan` on planned; `evidenceSummary` on noTarget |
| `step.evidence.api-plan` | agent | `phasePacket`, `built` | `planned`, `noTarget` | `evidencePlan` on planned; `evidenceSummary` on noTarget |
| `step.evidence.db-plan` | agent | `phasePacket`, `built` | `planned`, `noTarget` | `evidencePlan` on planned; `evidenceSummary` on noTarget |
| `step.evidence.ui-run` | agent | `evidencePlan`, `built` | `passed`, `failed`, `broken` | `evidenceSummary` (every outcome) |
| `step.evidence.api-run` | agent | `evidencePlan`, `built` | `passed`, `failed` | `evidenceSummary` (every outcome) |
| `step.evidence.db-run` | agent | `evidencePlan`, `built` | `passed`, `failed` | `evidenceSummary` (every outcome) |
| `step.evidence.none` | block | `built` | `done` | `evidenceSummary` on done |
| `step.evidence.combine` | block | `evidenceUi`, `evidenceApi`, `evidenceDb` | `done` | `evidenceSummary` on done |
| `step.evidence.desk` | desk, human-only | `built` | `recorded`, `noneFound` | `evidenceSummary` (every outcome); `evidenceReason` (every outcome) |
| `step.review.round` | block | `change`, `phasePacket`, `lensRounds` | `ready`, `noLens` | `lensQueue`, `lensRounds` on ready |
| `step.lens.run` | agent; parameters `subject` (required), `bundle` | `lensQueue`, `lensRounds` | `reviewed` | `lensReport` on reviewed |
| `step.review.log` | block | `lensReport`, `lensRounds`, `lensQueue` | `next`, `clean`, `findings`, `behaviourWrong`, `needsOwner` | `lensRounds` (every outcome); `lensQueue` on next; `review` on clean, findings, behaviourWrong, needsOwner; `escalation` on needsOwner |
| `step.review.record` | block | `change`, `review`, `lensRounds` | `recorded` | `review` on recorded |
| `step.review.fix` | agent | `change`, `review` | `fixed`, `needsOwner` | `change` on fixed; `escalation` on needsOwner |
| `step.review.apply-answer` | agent | `change`, `review`, `escalation`, `answers` | `fixed`, `needsOwner` | `change` on fixed; `escalation` on needsOwner |
| `step.lens.ask` | desk, human-only | `escalation` | `answered`, `stop` | `answers` on answered |
| `step.review.recheck` | agent | `change`, `review` | `clean`, `findings`, `behaviourWrong` | `review` (every outcome) |
| `step.owner.decide` | desk, human-only | - | `retry`, `stop` | `notes` on retry |
| `step.ship.green` | block | `phasePacket`, `workspace` | `green`, `red` | `green` on green; `findings` on red |
| `step.ship.pack` | block | `phasePacket`, `proofPack`, `review`, `lineContext` | `packed` | `handoffDraft` on packed |
| `step.ship.freeze` | block | `handoffDraft`, `proofPack`, `review` | `frozen`, `stale` | `handoff` on frozen; `findings` on stale |
| `step.ship.open` | desk, human-only | `handoff`, `workspace` | `opened`, `updated`, `refused` | `pr` on opened and updated (stands in for `effect.pr.open`; nothing reaches the forge) |
| `step.ship.return` | block | `findings` | `noted` | `returnNotes` on noted |
| `step.docs.scope` | block | `pr`, `phasePacket` | `candidates`, `none`, `disabled`, `notOpen` | `docsResult` (every outcome); `docsScope`, `prState`, `findings`, `handoffNote`, `workspace` on candidates |
| `step.docs.update` | agent | `docsScope`, `prState`, `findings`, `handoffNote`, `phasePacket` | `changed`, `noChange`, `blocked` | `handoffNote` (every outcome) |
| `step.docs.check` | block | `docsScope`, `workspace` | `pass`, `fail` | `findings` on fail |
| `step.docs.open-pr` | desk, human-only | `pr`, `workspace` | `opened`, `updated`, `rejected` | `docsPr` on opened and updated (stands in for `effect.pr.create-stacked`; nothing reaches the forge) |


### Block catalog (scoping station)

Descriptor-only too (`ScopingSteps` in `Aw.Sindri.Features/Blocks/`; the three context inputs are real runtimes, see "The context inputs (S-LB2)"); the recipes are `workflow.scoping` and `workflow.idea-to-brief` (scoping spec §6, §9). Every Step is desk-capable. An agent Step reaches only `Tools` (the read-only rule is W9's, enforced at launch).

The table also lists the ship and phase Steps. Three of them have block runtimes (BS-3): `step.ship.return` (`ShipReturnStep`) and `step.phase.drift` (`PhaseDriftStep`, which compares HEAD with `built.head`), and `step.build.prepare` of the build station (`BuildPrepareStep`, which checks the workspace is clean). The other rows are descriptor-only.

| Step | Runtime | Inputs | Outcomes | Outputs |
|---|---|---|---|---|
| `step.scope.intake` | block | `scopeInput` | `brief`, `tracker`, `idea`, `change` | the eight seeds (`scopeMap`, `acceptance`, `findings`, `answers`, `answerLog`, `challenge`, `questions`, `openQuestions`) on all four |
| `step.scope.read-brief`, `step.scope.read-change` | block | `scopeInput` | `read`, `empty` | `brief`, `anchors` on read |
| `step.scope.read-tracker` | block (`Network`) | `scopeInput` | `read`, `empty` | `brief`, `anchors` on read |
| `step.brief.seed` | block | - | `seeded` | `answers`, `answerLog` on seeded |
| `step.brief.questions` | agent | `idea`, `answers`, `answerLog` | `asked`, `ready` | `questions` on asked; `interviewClosed` on ready |
| `step.brief.write` | agent | `idea`, `answers`, `answerLog` | `written`, `empty` | `brief` on written |
| `step.scope.anchor` | block | `brief` | `anchored` | `anchors` on anchored |
| `step.scope.gather` | block | `anchors` (+ its Step inputs) | `gathered` | `lenses`, `coverage` on gathered |
| `step.scope.draft` | agent | `brief`, `lenses`, `coverage`, `scopeMap`, `acceptance`, `findings`, `answerLog`, `challenge` | `drafted` | `scopeMap`, `acceptance` on drafted |
| `step.scope.check` | block | `brief`, `lenses`, `coverage`, `scopeMap`, `acceptance`, `answers`, `answerLog` | `pass`, `gaps`, `regather` | `findings` on gaps, regather |
| `step.interview.questions` | agent | `scopeMap`, `acceptance`, `lenses`, `answerLog`, `challenge`, `openQuestions` | `asked`, `none` | `questions` on asked; `openQuestions` on asked, none |
| `step.interview.answer` | desk, human-only | `questions`, `answerLog` | `answered`, `doneAsking` | `answers` on both; `interviewClosed` on doneAsking |
| `step.interview.record` | block | `answers`, `answerLog` | `recorded` | `answerLog` on recorded |
| `step.interview.apply` | agent | ten facts (spec §9) | `updated`, `rescope` | `scopeMap`, `acceptance`, `openQuestions` on both |
| `step.scope.reanchor` | block | `scopeMap`, `anchors`, `lenses` | `anchored` | `anchors` on anchored |
| `step.scope.challenge` | agent | `brief`, `lenses`, `coverage`, `scopeMap`, `acceptance`, `answerLog`, `challenge` | `nothingNew`, `found` | `challengeRound` (every outcome) |
| `step.scope.challenge-log` | block | `challenge`, `challengeRound` | `nothingNew`, `found` | `challenge` (every outcome); `findings` on found |
| `step.scope.propose` | block | `scopeMap` | `proposed`, `skipped` | `scopeMap` (every outcome) |
| `step.scope.evidence` | block | `brief`, `lenses`, `coverage`, `scopeMap`, `acceptance`, `answers`, `answerLog`, `challenge`, `openQuestions`, `decision`, `anchors`, `questions` | `complete`, `incomplete`, `regather` | five facts on complete; `findings` on incomplete, regather |
| `step.scope.review` | desk, human-only | `scopePacket` | `approve`, `revise`, `drop` | `answers` on revise |
| `step.scope.freeze` | block | `scopePacket` and the twelve facts `step.scope.evidence` reads (it re-runs the evidence checks) | `frozen`, `mismatch` | `scopePacket` = `{hash, dir}` on frozen (the run area's `scope-packet/` stays in place); `findings` on mismatch. The engine appends `scoping.approved` under the lease for each `frozen` report (key `scoping.approved:<packetHash>`; `Instance` is the address, the row's item is set, and the hash is aw-packets' hash of that directory) |
| `step.scope.withdraw` | block | - | `withdrawn` | - |
| `step.phase.seed` | block | `phasePacket`, `lineContext` | `seeded` | `buildNotes`, `workspace`, `built`, `change`, `redTests` on seeded |
| `step.phase.drift` | block | `built`, `change`, `workspace` | `same`, `moved` | `built`, `change` on moved |
| `step.phase.decide` | desk, human-only | `phasePacket` | `rebuild`, `stop` | `buildNotes` on rebuild |
| `step.babysit.watch` | desk, human-only (stub of `workflow.pr-babysitter`) | `pr`, `handoff`, `phasePacket`, `lineContext`, `workspace` | `merged`, `rework`, `stuck` | `mergedSha` on merged; `reworkNotes` on rework |
| `effect.line.report` | effect (descriptor only) | - | - | parameters `status`, `line` |

`step.interview.answer` and `step.scope.review` carry no form yet: the answer form built from `fact:questions` and the stop's fixed form (approve, revise, drop) await W2, so at the desk they complete with plain outcomes and outputs. The output schemas, their fact-or-packet-file design and field-name choices are in `schemas/scoping/README.md`.

### Block catalog (line design and plan stations)

Steps of `workflow.design` and `workflow.plan` (line spec §4-§6, LN-Y1; all descriptor-only except `step.lens.log`, a block runtime since BS-4; `Aw.Sindri.Features/Blocks/Step/{DesignSteps,LensRoundSteps,LensSteps,PlanSteps}.cs`). A served instance parks at a descriptor-only one with SND-WF-010 until its runtime ships; at the desk each completes with plain outcomes and outputs. `bundle`, `pack` and `subject` are optional text parameters (`subject` required on `step.lens.run`), descriptor-only until LN-PK resolves packs; `workflow.packs.lenses.design` and `workflow.packs.style` are profile key names only. Neither recipe uses an `input.*` Step input.

| Step | Kind | Reads | Outcomes | Writes |
|---|---|---|---|---|
| `step.design.intake` | block | `scopePacket`, `designRevision`, `priorDesignPacket` | `fresh`, `revise` | the eight seeds (`scope`, `packs`, `design`, `findings`, `lensRounds`, `changedSections`, `answers`, `accepted`) on both; on `revise`, `design` is the previous design packet's design |
| `step.design.draft` | agent | `scope`, `packs`, `design`, `findings`, `changedSections` | `drafted` | `design`; optional `bundle` |
| `step.design.apply` | agent | `scope`, `packs`, `design`, `designRevision`, `answers` | `applied` | `design`, `changedSections`; optional `bundle` |
| `step.design.check` | block | `scope`, `packs`, `design` | `pass`, `gaps` | `findings` on gaps |
| `step.design.revise` | agent | `scope`, `packs`, `design`, `findings`, `answers` | `revised` | `design`, `changedSections`; optional `bundle` |
| `step.design.lens-desk` | desk, human-only | `findings`, `lensRounds` | `accept`, `revise`, `drop` | `accepted` on accept, `answers` on revise |
| `step.design.evidence` | block | `scope`, `packs`, `design`, `findings`, `lensRounds`, `accepted` | `complete`, `incomplete` | `designPacket`, `design`, `lensRounds` on complete; `findings` on incomplete |
| `step.lens.round` | block | `scope`, `packs`, `lensRounds`, `changedSections` | `ready` | `lensQueue`, `lensRounds` on ready; optional `pack` |
| `step.lens.run` | agent | `lensQueue`, `lensRounds` | `reviewed` | `lensReport`; `subject` (required), optional `bundle` |
| `step.lens.log` | block | `lensReport`, `lensRounds`, `lensQueue` | `next`, `clean`, `findings`, `needsOwner` | `lensRounds` on all; `lensQueue` on next; `findings` on findings, needsOwner; `escalation` on needsOwner |
| `step.plan.intake` | block | `scopePacket`, `designPacket` | `loaded` | the seven seeds (`design`, `plan`, `findings`, `questions`, `answers`, `answerLog`, `holdoutSets`) and `anchors`: the anchor keys of the design's covered surfaces, read by the planner's `input.context.code` (LN-CTX2) |
| `step.plan.graph` | agent | `scopePacket`, `design`, `plan`, `findings`, `answerLog` | `drafted` | `plan` |
| `step.plan.check` | block | `scopePacket`, `design`, `plan` | `pass`, `gaps` | `findings` on gaps |
| `step.plan.exec-review` | agent | `design`, `plan` | `executable`, `objections` | `findings` on objections |
| `step.plan.holdout` | agent | `design`, `plan` | `written` | `holdoutSets` (ids and hashes, never scenario text) |
| `step.plan.evidence` | block | `scopePacket`, `design`, `plan`, `holdoutSets`, `findings`, `answerLog` | `complete`, `incomplete` | `planPacket` on complete, `findings` on incomplete |
| `step.plan.questions` | block | `design`, `answers`, `answerLog` | `asked`, `none` | `questions` on asked |
| `step.plan.settle` | block | `design`, `answers`, `answerLog` | `assumed`, `changed`, `replan` | `designRevision` on changed |
| `step.plan.review` | desk, human-only | `planPacket`, `design` | `approve`, `reviseDesign`, `revisePlan`, `drop` | `answers` on reviseDesign and revisePlan |
| `step.plan.freeze` | block | `planPacket`, `design`, `plan`, `holdoutSets` | `frozen`, `mismatch` | `planPacket`, `phasePackets` on frozen; `findings` on mismatch |

The Step inputs `input.context.lenses`, `input.context.code` and `input.context.history` are real Step inputs (parameters `anchors`, `depth`, `hops`, `window`; "The context inputs (S-LB2)" below), so the recipe validates and approves. The output schemas are `schemas/scoping/` (`node scripts/check-scoping-schemas.mjs`).

### Design and plan stations on the block runtime (BS-2, BS-2b)

Nine Steps of `workflow.design` and `workflow.plan` run on the block runtime in this build (code in `Aw.Sindri.Features/Line/`, registered by `AddLineSlices`; the descriptors above are BS-2b's, except `step.design.intake` and `step.design.draft`, whose Reads BS-2c widened, so those two hashes and both recipes' hashes moved; no `Workflow.Patched` marker was needed): `step.design.check`, `step.plan.check`, `step.plan.questions`, `step.plan.settle` (BS-2, pure rule sets over facts) and the five that touch packets (BS-2b): `step.design.intake`, `step.design.evidence`, `step.plan.intake`, `step.plan.evidence`, `step.plan.freeze`. They work through ports, never the disk or the aw-packets binary directly: `IPackets` (hash, validate, freeze), `IRunArea` (now with `PathFor`, the absolute path of a run-area directory, so aw-packets can be pointed at a packet a Step wrote), `IPacketFiles` (reads one named file of a packet directory inside Sindri's home) and `IRepoCheckouts` (the checkout aw-packets needs for PKT-BASE-001; the evidence and freeze Steps are `IRepoStep`).

Shapes the spec leaves open, chosen here:

| Shape | Choice |
|---|---|
| Packet facts | `fact:scopePacket`, `fact:designPacket` and `fact:planPacket` are `{ "hash": "sha256:…", "dir": "<absolute directory>" }`; a bare hash has no files to read and fails the Step with the fix. The scoping station's freeze must write `dir` too (today it writes only `hash`: a cross-lane item). |
| Verification | The Step recomputes the hash from the directory (`IPackets.Hash`) and compares: a mismatch fails with `SND-LINE-001` naming both hashes. The port has no `verify`, so the freeze record is not looked up. |
| `fact:scope` | `{ hash, dir, baseSha, change, scopeMap, acceptance, acceptanceHash, openQuestions[], answerLog[] }`, loaded from `scope-map.json`, `acceptance.json`, `questions.json` and `answers.jsonl`; an open question is one no answer batch answered. `acceptanceHash` is the digest of `acceptance.json`'s bytes, which the design's acceptance chain extends. |
| `fact:packs` | `{}`: no pack resolver exists until LN-PK, so `SND-PACK-001/-002` are not checked and `style.json` holds only what `fact:packs` carries (`style`, `lenses`). |
| Revision | `fact:designRevision` with decisions or notes is `revise` and seeds `fact:answers` with `[revision]`; the first pass text is `fresh`. A revision edits the previous design: `workflow.design` takes `fact:priorDesignPacket` (the line passes its `fact:designPacket` to the redesign child, and a text on the first pass, which fresh ignores). Revise verifies that packet's hash and that its index names the scope packet (SND-LINE-001) and loads it into `fact:design` with `DesignPacket.FromFiles`, so its acceptance chain carries over and `step.design.apply` rewrites only the sections in `affects`. A revision with no previous packet fails with the fix; it never seeds an empty design. `step.design.draft` also reads `designRevision` and `answers`, so a redraft after `gaps` keeps the owner's picks. |
| `fact:design` | The design packet's files, field by field: `spec` (the `spec.md` text), `sections`, `coverage`, `decisions`, `deferred`, `resolutions` (the last two live in `decisions.json`), `signals`, `contracts`, `acceptance` (`items`), `escapes`, `runs`; the plan station's copy adds `lensRounds`, `accepted` (the owner's accepted risks, kept in `runs.json`), `acceptanceHash` and `packet` (`{ hash, dir }`). The gold fixture's `signals.json` and `coverage.json` have other shapes than the fact's (an object keyed by item; surfaces with sections): the checks read the fact's. |
| Repo slug | A packet's `repo` is `sindri/<repo name>`: the profile names no remote, and aw-packets wants `owner/name`. Ids: `design-<change>`, `plan-<change>`, `<change>-<phase>`. |
| `fact:plan` | The checks' phases (`id`, `workstream`, `deps`, `size`, `kind`, `acceptance`, `waitsForMerge`, `execReview`), plus optional `title`, `goal`, `writeScope` or `allowedFiles`, `readSeeds`, `rules`, `testChange` and `budget`. `tasks.json` is PKT's task plan made from it (`kind:` and `test-change:` rules); `graph.json` is derived: wave = 1 + the deepest dependency, a phase continues its first dependency's stack unless an earlier one did (else a new stack), base = the default branch with no dependency, else the first dependency's head, `waitsForMerge` = the phase's flag or more than one dependency. |
| Phase packets | Made at `step.plan.evidence` in `phase-packets/<phase>/` (unfrozen) and named in `fact:planPacket.phases[]` (`{ phase, id, dir, hash }`, the hash being what `step.plan.freeze` compares before it freezes); `step.plan.freeze` freezes them and writes `fact:phasePackets` (`[{ phase, id, hash, dir }]`). `design.md` is the spec's sections that cite the phase's items. |
| Freezer | `step.plan.freeze` freezes as `owner` (LC-5: it runs only on stop 2's human-only approve, and a freeze is write-once, so a machine freeze first would never be upgraded). A packet frozen by someone else first is `mismatch` (`PKT-FREEZE-002`). |

Run-area files: `design-packet/` (13 files) in the design instance, `plan-packet/` (7) and `phase-packets/<phase>/` (7 each) in the plan instance, written by the evidence Steps and read by the next station through `fact:*Packet.dir`. A Step has no ledger port (a write needs a lease grant), so `line.escape`, `design.approved` and `plan.approved` rows are not written (the escapes are in `escapes.json`), the holdout sets are not sealed, and LN-CTX1's proposals to the context map are not made; `step.plan.freeze` also cannot re-run the design checks (it reads no scope), so it re-runs the plan checks and confirms the design and plan packets still hash to what the evidence checked. Also known: `DSN-XSPEC-001/-002` and `DSN-STYLE-001` only warn, `PLN-PKT-001`'s `--sizes` is not passed.

`step.judge.choose` is `block` runtime and `step.desk.choose` is `desk` and human-only; their hashes moved once when the two fields
landed (provisional owner default). The agent runtime (engine P8) moved `step.produce`, `step.build.red`, `step.build.green` and `step.prove.holdout`; every other shipped descriptor hashes as before (`ShippedBlocksTests` pins all of them). `sindri recipe list --stale` shows which approved recipes use a moved descriptor and need re-approval.

### Lens loop facts (`step.lens.log` and the lens report)

`step.lens.log` is a block runtime (BS-4, `Aw.Sindri.Features/LensLoops/`); `step.review.log` joined it in BS-12, and `step.lens.round` and `step.review.round` in LN-PK lite (see "Lens packs" below); `step.review.record` still runs at the desk. The facts the loop passes between its Steps have these shapes, so the prompts of `step.lens.run` and the Steps that read the log can target them. Facts are JSON text; every field not listed is kept as written. Severity names are matched ignoring case: the design scale is CRITICAL, HIGH, MEDIUM, LOW (the gold packet writes them upper case), the review scale blocking, issue, suggestion, nit. The two most severe stop a round.

| Fact | Written by | Shape |
|---|---|---|
| `fact:lensQueue` | `step.lens.round` (seed), `step.lens.log` (on `next`) | array of lens ids, in pack order (an element may be an object `{ "id": ... }`). `step.lens.log` takes the reported lens off it |
| `fact:lensReport` | `step.lens.run` (the agent) | `{ "lens": "<id>", "packHash"?: "sha256:...", "model"?: "...", "changedSections"?: ["sec-..."], "findings": [Finding], "notApplicable"?: "<reason>" }`. `lens` is required and must be in `fact:lensQueue`; either `findings` (may be empty) or a non-empty `notApplicable` is required. A severity off the loop's scale fails the Step |
| Finding | the agent; `resolution` is added later by the revise Step or the desk | `{ "id"?, "severity", "cite", "text", "resolution"? }` (the gold `lens-rounds.jsonl` shape) |
| `fact:lensRounds` | `step.lens.log` (append-only; every outcome) | array, one entry per lens run: the report as written plus `"round": n` (counted from 1) and, on the last entry of a round (the queue is empty), `"closes": true`. A round continues while the last entry does not close; the entry after a closing one opens round n+1, and so does a report for a lens the open round already logged. A `closes` or `round` the agent wrote in its report is ignored: the Step sets both. `step.design.evidence` writes `lens-rounds.jsonl` from these entries and drops `closes` |
| `fact:findings` (design, on `findings`) | `step.lens.log` | array of the round's CRITICAL and HIGH findings, each with `"lens": "<id>"` added |

Outcomes of `step.lens.log`: `next` while the queue is not empty (writes `lensRounds`, `lensQueue`); at the round's end `clean` (no CRITICAL or HIGH finding in any entry of the round), `findings`, or `needsOwner` (the same findings and a typed escalation; see below). Caps and no-progress are the recipe's loops, not the Step's.

**Review loop (BS-12).** `step.review.log` has the same job with the review scale and a running `fact:review`, and is a block runtime now that its descriptor reads `fact:lensQueue` (so it tells `next` from a round's end). `step.review.record` stays at the desk: it needs a review-packet writer over `IPackets` (kind `review`), which does not exist in the build; `step.review.round` is a block runtime (LN-PK lite, below). The `fact:review` is `{ "round": n, "verdict": "clean" | "findings" | "behaviourWrong", "blocking": n, "issue": n, "suggestions": n, "nits": n, "behaviour"?: "wrong", "findings": [Finding + lens] }`, where `behaviourWrong` is a blocking finding with `"behaviour": "wrong"`. `step.review.recheck` writes the same shape, `step.review.record` freezes `review.json` from it with `fact:lensRounds` and the head sha of `fact:change`.

**Escalation (BS-12, owner 2026-10-19).** The review and design loops run by themselves and ask a human only on an ambiguous fix or a product question. The one way there is an agent's typed report: a finding (lens report, `schemas/line/lens-report.json`) carries `escalate: { kind: "ambiguousFix" | "productQuestion", question, options? }`, or `step.review.fix` reports `needsOwner` with `fact:escalation` of the same shape (`schemas/line/escalation.json`). At a round's end `step.lens.log` and `step.review.log` report `needsOwner` instead of `findings` when a stopping finding (CRITICAL or HIGH; blocking or issue) carries a valid one, and write it as `fact:escalation` (plus the finding's `lens` and `finding` id); a `behaviourWrong` round keeps its route. A finding that only says "please decide", has another kind, no question or a bad option never escalates and stays in the loop, round after round. The recipes route `needsOwner` to `ask` (`step.lens.ask`, human-only, the form is the agent's question and options): `answered` writes `fact:answers` and goes to the fixer (`step.review.apply-answer` in the review loop, which can ask again; `step.design.revise` in the design loop); `stop` ends the loop (review: `stuck`, unresolved; design: `dropped`). The review loop's `asks` loop caps the asks at 3; a loop past its cap parks (a failure the owner sees, not a question). A cap hit or no progress keeps its route: review rounds at the cap go to the narrow re-check and then `stuck`; the design lens cap goes to `step.design.lens-desk`.

### Lens packs (`step.lens.round`, `step.review.round`; LN-PK lite)

The lenses a round queues come from a **built-in pack** in `config/packs/lenses/<id>/` (`pack.json` and `lenses/<lens>.md`, line spec §5.2), embedded in the Infrastructure assembly. `design-five` (applies `design`: product, architecture, design, devex, security) and `code-review` (applies `review`: correctness, security, performance, architecture, tests, and the conditional data-integrity, accessibility and ui-races) ship with the toolkit; a pack holds 1 to 10 lenses. `ILensPacks.Resolve(kind, repo)` (Core port, `ProfileLensPacks` over the approved profile) picks the id from `workflow.packs.lenses.design` or `.review` (a repo's `repos` entry, else `default`, else the built-in default) and returns `{ id, hash, lenses[] }`; the hash is SHA-256 over each file's path and the hash of its bytes, in path order. An id that is not a built-in pack, a pack for the other loop, or a pack of more than 10 lenses is an `SND-CLI-003` error (the Step fails with it; `SND-PACK-001` to `-004` are not run in this unit, and there are no repo packs, machine packs or pins yet).

Each round queues the lenses whose `when` holds, in pack order, as `fact:lensQueue` (lens ids), and appends one `{ "lens", "round", "notApplicable": "<reason>", "packHash" }` row to `fact:lensRounds` for every lens it does not queue, so none is skipped silently. `when` is `"always"`, `{ "surfaceKinds": [...] }` (design: the kinds of the surfaces in `fact:scope`, ignoring case) or `{ "paths": [...] }` (review: the paths of `git diff --name-only base...head` of `fact:change`, `**` crossing directories, `*` and `?` within one). A subject that does not say (a scope with no surface list; a change with no `base` and `head`, such as the empty change a bug fix passes) selects every lens. `step.review.round` reports `noLens` when nothing applies (the loop ends `stuck`); `step.lens.round` fails the Step instead (the design lens loop has no such route).

**Round 1.** Both Steps read `fact:lensRounds`, and the runner fails a Step whose declared fact is missing, so the log must exist before round 1. The design recipe's `intake` already seeds it. `workflow.review-loop` (version 4) declares `fact:lensRounds` as an input, and `workflow.phase` (version 2) passes the literal `[]` for it each time it starts a review, so every review starts with an empty log and `step.review.round` carries it (and the next round's start numbers after the closed one).

### Block catalog (line stations: build and prove)

`step.build.prepare` has a block runtime (BS-3); the rest is descriptor-only (`BuildSteps`, `ProveSteps` in `Aw.Sindri.Features/Blocks/Step/`); the recipes are `workflow.build` and `workflow.prove` (line spec §7.2, §7.3, Appendix C.3 and C.4). Every Step is desk-capable.

| Step | Runtime | Outcomes | Outputs |
|---|---|---|---|
| `step.build.prepare` | block | `ready` | the seeds `activePacket`, `redTests`, `built`, `change`, `findings`, `answers` |
| `step.build.red` | agent | `written` | `redTests` |
| `step.build.red-check` | block | `red`, `vacuous` | `findings` on vacuous |
| `step.build.green` | agent | `built`, `blocked` | `built`; `findings` on blocked |
| `step.build.checks` | block | `pass`, `failed`, `outOfScope` | `built`, `change` on pass; `findings` otherwise |
| `step.build.scope-desk` | desk, human-only | `allow`, `revert`, `stop` | `answers` on allow and revert |
| `step.build.amend` | block | `amended` | `activePacket` |
| `step.prove.intake` | block | `ready` | the seeds `findings`, `proof`, `holdoutResult`, `beforeAfter` |
| `step.prove.before-after` | block | `pass`, `fail` | `beforeAfter` |
| `step.prove.holdout` | agent | `ran` | `holdoutResult` |
| `step.prove.holdout-log` | block | `passed`, `failed`, `leaked` | `holdoutResult`; `findings` on failed and leaked |
| `step.prove.evidence` | block | `complete`, `incomplete` | `proofPack`; `findings` on incomplete |
| `step.prove.fail-log` | block | `logged` | `findings` |

`step.prove.holdout-log` has a block runtime (BS-6b, `Aw.Sindri.Features/Prove/ProveHoldoutLogStep.cs`): it checks the evaluator's `fact:holdoutResult` against the phase's hidden set in the holdout store (a result naming a scenario the set lacks, or whose `observed` line repeats a run of 8 words of the scenario, fails the Step), scans the phase's commits (base to `fact:built.head`) and the facts it reads for the canary phrase (`leaked`, one PRV-HOLD-003 finding), marks failed scenarios spent (`failed`, PRV-HOLD-001 findings by item) and otherwise records `passed` with `canary: clean`. Its descriptor now says block, so its hash moved (`sha256:9511ad11…`); no shipped recipe changed.

### Build and ship Steps on the block runtime: fact shapes

`step.build.prepare`, `step.phase.drift`, `step.ship.return`, `step.build.amend` and `step.ship.freeze` have block runtimes (`Aw.Sindri.Features/Build`, `Ship`); the descriptors above are unchanged. They read these shapes (the line spec does not define them, so these are the contract the desk and the other Steps write to):

| Fact | Shape | Written by |
|---|---|---|
| `fact:phasePacket`, `fact:activePacket` | `{ hash, dir, repo? }`: the packet's frozen hash and directory; `repo` is a checkout of the repository it builds on (PKT-BASE-001 needs the base commit), which `step.build.prepare` sets to `fact:workspace` | the phase's seed (`hash`, `dir`), prepare (`repo`), amend (the child's) |
| `fact:answers` | object `{ allow?: string[], reason?: string, revert?: string[] }`, the shape the shipped `workflow.build` evals use. `allow` holds the granted paths (repo-relative globs or `new: <path>`); amend needs at least one, and takes the owner's `reason` or the stock line `allowed at the scope desk` | prepare seeds `{}`; the scope desk writes it |
| `fact:handoffDraft` | `{ dir, head, workspace }`: the hand-off packet directory (`pr.json`, `body.md`, `green.json`, `packet.json`, `index.json`), the head it was packed on, and the phase worktree | `step.ship.pack` (at the desk until its fact shapes are settled) |
| `fact:proofPack`, `fact:review` | each holds `head`, the commit its run covered | `step.prove.evidence`, `step.review.record` |
| `fact:handoff` | `{ hash, dir }`: the frozen hand-off | freeze |
| `fact:findings` | array of `{ rule, message }` | `SHIP-SHA-001` on stale |

The build Steps read and write whole packet directories through `IPacketDirectory` (`Read(dir)` returns every file, `Create(address, name, files)` writes a new directory under the run area), a different port from the line Steps' `IPacketFiles`, which reads one named file; the adapters are `DiskPacketDirectory` and `InMemoryPacketDirectory`.

**The three Steps that run the repo's commands** (`step.build.red-check`, `step.build.checks`, `step.ship.green`) run every line of `workflow.build.commands` in the phase worktree under one heavy slot, taken and released inside the Step. Each refuses a dirty worktree before running anything: `BLD-TREE-001` (checks), `SHIP-TREE-001` (green), and for red-check a failure naming the worktree (it also needs HEAD to be the red commit, `BLD-RED-001`).

| Step | Outcome | When | Rule |
|---|---|---|---|
| `step.build.red-check` | `red` | at least one command fails on the red commit; it stops at the first failure | |
| | `vacuous` | every command passes, with one finding (`fact:findings`) naming the items and their tests | `BLD-RED-001` |
| `step.build.checks` | `pass` | the scope gate and the build rules hold and every command passes | |
| | `failed` | a command fails, one finding per failing command | `BLD-COMMAND-001` |
| `step.ship.green` | `green` | every command passes (`fact:green` records the commands run, `fact:green.commands`) | |
| | `red` | a command fails, one finding per failing command | `SHIP-GREEN-001` |

With **no commands** (no approved profile, or a profile that lists none): red-check fails the Step with the fix (add `workflow.build.commands`) and takes no slot; `step.build.checks` and `step.ship.green` pass, and green records an empty `fact:green.commands`. A command that outlives its own limit is that command's answer (a finding for checks, `red` for green, a failed Step for red-check). Each command may run for at most `workflow.steps.longMax`, and the Step gives the slot wait and all commands one budget: its `maxDuration` of 2 h (the default `longMax`, the cap on what a Step declares; a descriptor is static, so a larger `longMax` does not lengthen it) less 5 min to write its report. A 1 h suite therefore fits even after the longest slot wait (30 min). A batch that runs out of that budget fails the Step (`(fix: make the commands faster, or list fewer of them in workflow.build.commands)`), it is not a command's finding. The states that run these Steps time out after 3 h, longer than the Steps (RCP-TIME-003).

**`step.build.amend`** reads `fact:activePacket` and `fact:answers` only (the descriptor's reads), so the child packet goes in **this instance's** run area, `workflow/runs/<WFI-id>/packets/<parent id>-a<n>/`, not the line's (`fact:lineContext.runArea` is not among its reads). It copies the parent's files, sets `packet.json` to the next id (`-a1`, then `-a2`...), `draft` and a null hash, adds the granted paths to `phase.json`'s `allowedFiles`, writes `amends: { parent, paths, reason, at }` (the parent's hash, this grant's paths only) and sets `index.json`'s parents to the parent's hash. It owner-freezes the child through `aw-packets freeze --kind phase --by owner` and reports `{ hash, dir, repo }` as the packet in force. A grant with no usable `allow`, a missing `repo`, an unreadable parent or a child that fails its checks fails the Step (the rule ids are in the message). The LC-7 escape is a line `{ from: "build", station: "amend", kind: "scopeGrant", id, parent }` appended to the run area's `escapes.jsonl`; the Step reads no ledger, so the `line.escape` row is written from that file.

**`step.ship.freeze`** passes `SHIP-SHA-001` when the draft's, proof's and review's heads and the worktree's HEAD (`IRunDiff.Head(workspace)`) are one sha; it then machine-freezes the hand-off (`aw-packets freeze --kind handoff --by machine`) and reports `frozen`. Any other combination reports `stale` with one `SHIP-SHA-001` finding naming all four shas and freezes nothing. A hand-off that fails its checks, or a missing fact, fails the Step.

#### The prove station's fact shapes (BS-5)

The line spec names the facts but not their fields; these are the shapes the prove Steps on the block runtime read and write (`Aw.Sindri.Features/Prove/`, rules in `Aw.Sindri.Core/Prove/ProofChecks.cs`). They are all JSON, and a fact the instance does not hold is a Step failure or, for an optional one, a rule reported as not run.

- `findings`: a list of `{rule, item, message, fix, severity}` (`severity` is `fail` or `warn`; `item` is an acceptance item id or null), the scoping findings shape as a bare list, as `step.build.prepare` seeds it.
- `proof`: `{head, items[{id, text, signals[{id, kind, statement, runsAt}], artifacts[{kind, path, sha256, commit, at, passing, signal?, superseded?}]}]}`. `step.prove.intake` seeds it from the phase packet's `acceptance.json` (every item that is not `superseded`) and `signals.json`, with no artifacts.
- `beforeAfter`: `{commit, results[{item, signal, base, head, baseScript?, headScript?}]}`, `base` and `head` being `fail` or `pass`. `holdoutResult`: `{commit, canary?, results[{scenario, item, verdict, category, observed, spent?}]}`, `verdict` being `pass` or `fail`, `canary` `clean` or `leaked`. Both start as `{commit, results: []}`.
- `redTests` (spec A.7, §7.2): an object keyed by item id. Each value is a list of test ids (`{"i1":["T1"]}`, the short form the evals seed) or `{tests?: [id | {id, path}], appOnly?: {signal}, commit?, output?}`. `PRV-RED-001` holds when every live (not superseded) item has at least one test or an `appOnly` entry; each item without either is one finding naming it, and a value of another shape is a finding naming the item and the expected shape, never read as "no tests". `docs` and `config` phases are exempt. Whether the tests failed (BLD-RED-001) is `step.build.red-check`'s, not checked again here. `red.json` of the pack is the fact as it is.
- The canary: a result of `holdoutResult` with `kind` `canary`, or with no `item` (the canary scenario shows no visible item; BS-6's `HoldoutScenario.CanaryKind`), is not a hidden scenario. `PRV-HOLD-001` leaves it out of the count of scenarios that ran and out of the verdict failures, `PRV-HOLD-002` leaves it out of the unspent count, and the pack's `holdout.json` does not list it. `holdout.json`'s `count` in the phase packet counts the hidden scenarios without the canary.
- `phasePacket.dir`: absolute as it is; relative, it is read under the repo's checkout (never under serve's working directory), by `step.prove.intake` and `step.prove.evidence` alike. A relative directory with no repo or no checkout fails the Step.
- `beforeAfter` matching: a result with no `signal` matches any signal of its item.
- `proofPack`: `{hash, dir}`, the machine-frozen proof pack (the same shape as a packet fact).
- `step.prove.before-after` runs each live item's `base+head` signal's `script` (a relative path inside `fact:workspace`, no `..`) with bash, at the phase's `baseSha` in a scratch worktree under the run area (`before-after/base`, removed afterwards, never forced) and at the head in the workspace: the same file runs in both trees, so `baseScript` and `headScript` are equal. Exit 0 is `pass`, anything else `fail`; a signal with no usable `script` is recorded `missing` with a note (nothing run; it does not fail the Step). The outcome is `pass` only when every result is `fail` at the base and `pass` at the head. A script that cannot run (no bash, no file, a 20 minute timeout) fails the Step, which parks the instance.
- `step.prove.fail-log` keeps the findings the instance holds, adds one `PRV-BA-001` per before/after result that did not fail at the base and pass at the head, and one `PRV-HOLD-001` per hidden scenario that did not pass (by item and the observed line, never the scenario's id or text; spec §9.3), never the same rule and item twice; with nothing recorded it adds `PRV-FAIL-001`.
- `step.prove.evidence` reads exactly its descriptor's five facts (`phasePacket`, `built`, `redTests`, `beforeAfter`, `holdoutResult`), and the runner hands it nothing else, so `fact:proof` never reaches it in a served instance. `PRV-ACC-001` and `PRV-SHA-001` need `proof`'s artifacts and have no structural cover in the recipe, so without them **the Step fails** (`ProofReport.Unrunnable`, `ProofChecks.Required`): an unrouted `step.failed` parks the instance for the owner, nothing is frozen and the phase is not proven (LN7). Until something writes the artifacts into `proof` and the descriptor reads it, the evidence Step therefore parks every phase for the owner. `PRV-APP-001` needs the hand test and verify results (`manualTest`, `evidencePack`, which the descriptor does not read), and `PRV-HOLD-003` needs `holdoutResult.canary`: the recipe guarantees both structurally (evidence-pack is reached only after the hand-test and verify children resolve, and holdout-log routes `leaked` to fail-log), so each is reported **not run**, listed in the pack's `proof.json` under `notRun`, and is a warning, not a failure. `PRV-HOLD-002` uses a minimum of 2 until the profile key `workflow.prove.holdoutMin` exists. A phase of kind `docs` or `config` needs no application run and may have no red tests. `PRV-NP-001` is met by construction: `not-proven.md` lists every planned signal no passing head artifact names (by `signal`), or says `nothing skipped`. A pack that aw-packets refuses is the Step's own output, which the builder cannot fix: the Step fails with aw-packets' failed checks as the message instead of sending findings back to build.
- The pack is written to `proof-pack/` of the instance's run area (`packet.json`, `index.json`, `proof.json`, `red.json`, `before-after.json`, `holdout.json` with ids, verdicts and `spent` only, `not-proven.md`, `acceptance.json`, empty `app-run` and `evidence` lists) and frozen by aw-packets as kind `proof`, with the repo's checkout as `--repo`. The packet id is the phase packet's id and `-proof`; its only parent is the phase packet's hash; its acceptance extends the hash of the phase's `acceptance.json` bytes and flips an item to `passing` only with the evidence of a passing artifact of the head (`PRV-ACC-002`). `IRunArea.PathFor` gives the directory.
- `step.prove.holdout-log` (BS-6b) reads `holdoutResult`, `built` and `phasePacket`. The phase key is the phase packet's `packet.json` `id`, the base of the diff its `baseSha`, and the line slug the first segment of the instance's address; the hidden set comes from `IHoldoutStore` (`$AW_SINDRI_HOME/holdout/<line>/<phase>/`, spec §9.1). It parks the instance (the Step fails) when the observed line of a result shares a run of 8 words with its scenario's steps or expected text (case and punctuation ignored), when the report names a scenario the set lacks, when the set has no canary, or when the store or the commits can't be read. Otherwise it looks for the canary phrase (ignoring case) in the commits and patches from `baseSha` to the head (`ICommitText`, `git log -p`) and in the facts it reads: a hit is `leaked` with one `PRV-HOLD-003` finding and nothing else written. Failed scenarios are marked spent in the store and written as `PRV-HOLD-001` findings by item and observed line ('failed'); else 'passed'. The recorded `holdoutResult` is `{commit, canary: clean, results[...]}`: each result keeps the evaluator's fields, gets `spent: true` when the store holds it spent, and the canary's own result (`kind: canary`, no `item`, verdict `pass`) is appended and is never counted as a hidden scenario. It does not read `fact:findings`, so on 'failed' and 'leaked' it writes its own findings only. Left at the desk: the canary scan of every stored fact of the phase's instances (no reader of facts by address prefix exists), so only the facts the Step reads are scanned.
- Left at the desk: `step.prove.before-after` (needs the command port, BS-3d).

### Block recipes

The shipped block recipes (`recipes/sindri/`: report, investigate, reproduce, implement, hand-test, verify, the evidence
family and review-loop) are listed with their inputs, outputs and terminals in `recipes/sindri/README.md`. The inputs are
`issue` (report), `issue`, `expected` and `actual` (investigate), `issue` and `rootCause` (reproduce, implement), `phasePacket` and `workspace` (hand-test, verify), `phasePacket` and `built`
(every evidence recipe) and `change` and `phasePacket` (review-loop). `phasePacket` and `change` use the `packet` and
`payload` subject kinds. Approve the leaf recipes first, then `workflow.evidence-all`, then `workflow.evidence`.

## Writing a decision tree

A route is a `step.judge.choose` state that picks one of a recipe's branches, and a desk twin, a `step.desk.choose` state
that lets a person pick when the judge is unsure or fails. Until the block runtime ships both run at the desk. The
complete example is `recipes/dev/workflow.dev-evidence-route.yaml`.

- **The route state** gives `prompt`, `facts` (what the question reads; each must be produced on every path into the
  state) and `choices`, each entry quoted `"<outcome>: <criterion>"` with a camelCase outcome (`notStartable`, `[a-z][A-Za-z0-9]{0,31}`). Its outcomes are the choice names plus `uncertain`.
  The choices are literal, so approving the recipe fixes its branches.
- **The desk twin** is `step: { use: step.desk.choose, with: { from: <route state> } }`. Its outcomes are the route's choices.
- **Transitions:** one per choice, listing both states in `from`, each `produces: [fact:decision]`; then
  `{ from: <route>, to: <twin>, on: step.completed, when: { outcome: uncertain }, produces: [fact:decision] }` and
  `{ from: <route>, to: <twin>, on: step.failed }`, last (`RCP-CHOICE-006`). A missing branch or `uncertain` is `RCP-OUTCOME-001`.
- **`fact:decision`** is written by the engine, never typed: at the desk `complete --outcome <choice>` stores
  `{ "choice", "provider": "desk", "actor" }`, and `complete --output fact:decision=...` is refused. `recipe eval` fills it
  with provider `eval` when a line leaves it out; an eval line on a desk twin that writes `fact:decision` itself fails with the same
  "is written by the engine" text.
- **Evals:** `recipe eval --scaffold <file>` prints the first path; `--scaffold <file> --out <dir>` writes one
  `<dir>/scaffold-<branch>.jsonl` per path (each choice, `uncertain`, and `step_failed`) and refuses to overwrite a file.
- Until DT-V2, `status` on a choice state doesn't list the choices; read them in the recipe.

## Child outputs

A child declares what it hands back in `outputs`; the parent's `wait.satisfied` from it then carries those facts, and the
transition decides which to keep (decision-tree spec §8.4, DT-C). The complete example is the pair
`recipes/dev/workflow.dev-child-summary.yaml` (the child) and `recipes/dev/workflow.dev-child-outputs.yaml` (the parent).

- **Declare** the output on the child (`outputs: [{ key: fact:output, ..., from: export }]`) and produce it in the child
  (`produces: [fact:output]` on the transition out of `export`).
- **Take it** on the parent's transition from the child wait: `produces: [fact:output]`, or rename it with
  `{ fact: fact:exported, from: fact:output }` (both required; `from` is the child's key). A rename is only for a child's output.
- **Where it is there:** an output is present on an outcome only if every path inside the child to a terminal state that ends
  with it produces it. Produce it on `resolved`, not on `unresolved` if some path skips it (`RCP-WIRE-003` names the path), and
  never on `cancelled`. The `when` is resolved against the child's outcomes (its terminal outcomes and `cancelled`): `not` is the
  complement, `all` the intersection, `any` the union, and a fact or loop test **may** hold on any outcome and **must** hold on
  none: the check runs over the outcomes the `when` may fire on, so `not: { outcome: cancelled }` is fine when the output is on
  every other outcome, and `all: [{ outcome: resolved }, { not: { fact: ... } }]` is still checked as `resolved`. A `when` with
  no outcome test anywhere (`not: { fact: ... }` included) matches every outcome, `cancelled` among them, and is refused. Route `cancelled` with a transition of its own.
- **Eval:** a child's `wait.satisfied` line gives the outputs the child would (`"outputs":{"fact:output":"..."}`), under the
  child's keys. A parent branch that tests the renamed fact (`when: { fact: fact:exported, is: present }`) is reached only
  if the line carried it. The test of a `when` runs before the transition's own `produces`, so it reads facts recorded earlier. A `when` reads the trigger's outputs under the child's keys, not the renamed ones.

## Children, gates and the item stubs (engine P7)

A `waitFor: { child }` state's entry starts the child as a Temporal child workflow (`ParentClosePolicy.Terminate`) whose
workflow id is the child's address, `{parent}/{recipe name}.{entry}`; a looped child wait therefore starts a new child on
each entry, and a child may wait on its own child (RCP-CHILD-003 allows depth 5). The child is created from the hash its
parent's approval pinned: `LoadChildren`, a local activity, reads the pinned children and their pinned children, and an
unpinned or re-hashed child is refused (`SND-RECIPE-002`).

- **Up:** the child signals its parent (`child`) when it parks, escalates or resumes; the parent decides it through the
  pure rules (`onParked: propagate` mirrors the park as `parked: <reason> (child)`, an escalation always escalates the parent
  as `child`, a resume ends the mirror). A change the parent can't take while it is suspended by something else is held and
  tried again after the parent's next change.
- **Down:** an answer to a parent that mirrors a child goes to the child (see "Start, answer, command and cancel"). A parent that ends,
  by a cancel or any other ending, signals each live child to cancel (`parentCancel`, origin `parent`; workflow code can't send an
  Update), so each records its own `cancelled` and frees its claim; the parent waits up to 30 seconds for them, then the parent-close
  terminate backstops. A history without the `p7-children` marker starts no child.
- **The end:** the child's workflow result carries its outcome and its declared `outputs` present at its end, **whichever
  outcome fired but `cancelled`**; the parent's `wait.satisfied` carries them and its `produces` picks (and renames) from them
  (DT spec §8.4). A child that fails or is terminated from outside ends the wait as `cancelled`.
- **Status reads each child live:** the parent's own view lists its live children; `GetInstance` through the control plane
  replaces each child's state with the answer of that child's own query (`unreachable` when none answers). The needs-you row of a
  mirrored park names the child's address in `child` (the `workflow.*` event's suspension carries `mirrorsAddress`, omitted otherwise).
- **Gates:** before the loop decides a trigger it asks which transition the trigger matches; when that transition has gates, the
  `EvaluateGates` local activity runs each over the registered runtimes (`gate.route.startable`, `gate.budget.available`).
- **Item stubs (spec §12):** `workflow.item` is routed by the router stub, a hosted service that appends `item.routed` when a
  `workflow.item` starts, with the outcome `workflow.route: { startable: all|none }` of the approved profile says; a `slot` wait is granted at once by
  the interpreter. Nothing but `workflow.route: { startable: none }` holds an item back.

## Temporal foundation

Engine rev 8 runs recipe instances on Temporal (engine spec §0, §5; README X53). Temporal is decided (owner,
2026-10-12), and **P4a, the Temporal foundation** (engine spec rev 8.1, "Phase 4a"), lands the code that measured it:
the `Temporalio` pin, `binaries.lock`, `Aw.Sindri.Workflows`, the dev server under `sindri serve` and `doctor`, the
worker, a first `RecipeWorkflow`, `workflow complete` and `workflow list`, and the ledger mirror. P4's units rebuild or
extend each of these to the spec. This section is P4a's acceptance record: each of the spec's five "Acceptance
(Temporal)" checks below with its measurement and how it was taken. The checks were written before the code and are not
tuned; the verification plan is `~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-wf-p0/verification-plan.md`.
The measurements were taken while the work was still called Phase 0, so the units (a)–(d), the "P0 scratch dir" and
the `proof/sindri-wf-p0/` paths below keep that name.

**Pins.** `Temporalio` 1.19.0 (`Directory.Packages.props`), referenced only by `src/Sindri/Aw.Sindri.Workflows`
(`AwAotCompatible=false`, temporalio/sdk-dotnet#286). `binaries.lock` pins the `temporal` CLI (1.9.1, the dev server)
and the time-skipping `temporal-test-server` (1.39.0) for darwin-arm64, darwin-x64, linux-x64 and linux-arm64.
`scripts/binaries-lock.sh fetch <name>` installs one for this platform under
`${AW_STATE_DIR:-~/.agentic-workflow}/cache/bin/<name>-<version>/` after checking its sha256;
`scripts/binaries-lock.sh check` validates the file (`scripts/tests/binaries-lock.test.sh`).

**Status:** done (2026-10-12). Units (a)–(d) merged; the prover re-proved every check on the merged branch (head `1e586964`), through `sindri serve`'s own worker; that is the branch the spec's numbers come from. After merging main (P4a), journeys 1, 3, 4, 5 and 7, the eval and timer tests and the merge gate were re-run; journeys 2 (loopback, 0600), 6 (the pause) and 9 (footprint, check 4) and the bad-query `workflow list` case were not (proof: `~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-wf-p0/evidence.md`, "After merging main (P4a)").

**On the merged head** (2026-10-12): `sindri stop` answered in 1.33 s, waiting for the worker to drain and then the dev server to exit before the release (both pids gone; the restart reopened the same `temporal.db` inode); after a `kill -9` of serve the next serve adopted the server and `complete --run 2.1` took 0.39 s wall (CLI start included); the ledger held 1 + 3 rows with 4 distinct keys after a stop, a serve and a full replay; the evals took 52 ms with the desk Update and 24 ms with the desk activity, and the `4h` timeout parked in 8 ms of wall time.

| # | Acceptance check | Measurement | Result |
|---|---|---|---|
| 1 | The server starts and stops with `sindri serve`, loopback only, its file under `$AW_SINDRI_HOME`, and survives a host sleep with the instance intact | listen addresses: all seven of the server's listeners on `127.0.0.1` (gRPC frontend, Web UI and five internal ports), none elsewhere (`lsof -nP -iTCP -sTCP:LISTEN -a -p <pid>`) · db path and owning pid: `$AW_SINDRI_HOME/temporal.db` (0600) open by the temporal pid (`lsof -p <pid>`), output in `logs/temporal.log` · pid in `control.json`: `"temporal":{"pid":…,"port":…,"uiPort":…}` beside serve's own `url`, `pid`, `epoch`; `sindri doctor` prints `ok temporal 127.0.0.1:<port> answers (pid <pid>), version 1.9.1 as pinned, namespace sindri present` · serve's start-to-`control.json`: 1.03 s with a fresh database, 0.47 s on the existing one · stop takes the server down: `sindri stop` exits 0 and the temporal pid is gone before the lease is released; a restart starts a new pid on the same `temporal.db` with the namespace and the seven search attributes (`Item`, `Recipe`, `State`, `NeedsYou`, `Repo`, `Tenant`, `Ring`) intact; `--namespace sindri` and `--search-attribute` on an existing database are no-ops, and serve also registers missing attributes through the OperatorService on every start · unclean exit: after `kill -9` of serve the server keeps running (it runs detached, in its own session, never as serve's child) and the next `sindri serve` adopts it (same pid); a server that doesn't answer within 3 s (SIGSTOPped, after `kill -9` of serve) is killed and replaced (new pid, 3.5 s to `control.json`) · sleep (simulated with `kill -STOP`/`-CONT` of serve and the server together, 2 min; a stand-in for host sleep, which isn't used on the owner's box): an open `RecipeWorkflow` instance (no worker yet, started with `NeedsYou=true`) kept its run id, history (2 events) and search attributes; `workflow list --query 'NeedsYou = true'` still found it; serve kept its lease (epoch 4) and `doctor` was green for `lease`, `api` and `temporal` 2 s after the resume. How: the built apphost (`artifacts/bin/Aw.Sindri.Host/debug/sindri`, `DOTNET_ROOT=~/.dotnet`), `AW_SINDRI_HOME=$TMPDIR/sindri-dev-p0`, `AW_SINDRI_PORT=0`, pinned `temporal` 1.9.1 (archive sha256 checked by `scripts/binaries-lock.sh fetch`; the extracted binary is `33c298d4…`); raw outputs in `p0-a/hand/` (`j123.out`, `j6.out`) in the P0 scratch dir. Tests: `TemporalDevServerTests`, `DevServerTemporalContractTests`, `DevServerTemporalTests` (integration, the real CLI), `TemporalCheckTests` and the slice tests (unit) · **Prover, merged build (journeys 1, 2, 3, 6):** doctor `ok temporal … version 1.9.1 as pinned, namespace sindri present`; seven listeners on `127.0.0.1` and none elsewhere; `temporal.db` open by the temporal pid; `sindri stop` takes it down and a restart opens the same file (same inode); the 2-min SIGSTOP stand-in (not a real sleep) left `J-6/step/1` with the same run id, history length 8, state `produce` run `1.1`, `workflow list` still finding it and its one ledger row, and doctor green 2 s after the resume | Pass |
| 2 | Async completion, timers and the kill-and-resume case work as stated; the eval cases pass on the time-skipping server in under 10 s total | `complete --run 1.1` moves `produce -> checks`: yes, `RecipeWorkflowTests` (integration, pinned dev server 1.9.1 via `ExistingPath`): run 1.1 completed through its task token, the instance is in `checks` with run 2.1, a second `complete --run 1.1` is `SND-WF-005`, the history replays clean; a run completed with no worker running moves on when a worker starts (both tests 2.4 s) (P0 (b)); the same completion through the task token moves it in the test interpreter (`SpikeEvalTests`, every eval line) · `4h` timeout wall time: **3 ms** with the desk report as the `complete` Update; **never fires** while the desk Step is an open async-completion activity (2 s asserted, see finding F1) · worker restart mid-Step, same run token after restart: **yes**, `1.1` scheduled once and completed by the new worker, next run `2.1` opened 26 ms (time-skipping, first worker uncached) / 15 ms (dev server); a real `kill -9` is journey 5 · eval cases (`happy`, `no-progress`, `rounds`) total: **0.06 s** (desk activity), **0.52 s** (desk Update), **0.54 s** with PR #180's real `TransitionDecision` (local merge, not pushed) · **Prover, merged build, through `sindri serve`'s own worker** (journeys 4, 5, 8): `sindri workflow complete J-4/step/1 --run 1.1 --outcome done --output fact:output=…`, with the frontend read from `control.json` (no `--temporal`), printed `-> checks`; the history shows `RunStep` `run:1.1:1` completed by the CLI's identity · a real `kill -9` of serve with `checks` open: the dev server kept running, the next serve adopted it, `GetInstance` still `checks` run `2.1`, and `complete --run 2.1` moved it to `verify` (activity `run:2.1:2` scheduled once) · the 19 probe tests (`Workflows/Spike/`) pass in 3.8 s on the merged build (evals 46 ms with the desk Update, 19 ms with the desk activity; 4 h timeout 3 ms wall with the Update, no skip with the activity open) | Pass, with F1: engine spec rev 8.1 §5.3 (desk Steps wait for the `complete` Update; P4 builds it) |
| 3 | The ledger holds one `workflow.transition` per transition after a replay (no duplicates) | transitions in history: **3** (+1 `workflow.started`; 4 `MarkerRecorded` local-activity markers) · ledger rows before replay: **2** (worker A, sticky cache, stopped at `verify`) · after replay: **3** `workflow.transition` + 1 `workflow.started`, no key twice (`sqlite3`), after worker B with `MaxCachedWorkflows = 0`, the SDK `WorkflowReplayer` over the fetched history, and a direct re-run of event 2 (`AlreadyRecorded`) · **Prover:** the key is now `workflow:{workflow id}:{run id}:{activity id}` (rev 8.1 §5.6; the test above re-proven with it, and a re-execution under the same activity id is `AlreadyRecorded`) · **through `sindri serve`** (journey 7): `J-4/step/1` after a `kill -9`, a `sindri stop` + `sindri serve` (empty sticky cache) and a full replay by `GetInstance`: 1 `workflow.started` + 2 `workflow.transition`, 3 distinct keys; after the next transition on the new worker, 1 + 3, 4 distinct keys (`…:2`, `…:4`, `…:6`, `…:8`), equal to the 3 `RunStep` transitions in history (`sqlite3 ledger.db`) | Pass |
| 4 | Dev server RSS ≤ 300 MB idle and ≤ 500 MB with ten instances; worker overhead inside `sindri serve` ≤ 150 MB; server ready in ≤ 5 s | **Prover re-measure (2026-10-12), macOS physical footprint** (`footprint -p <pid>`, cross-checked with `vmmap --summary` "Physical footprint" at start and end) beside `ps -o rss=`, because RSS leaves out compressed and swapped pages and the box was under memory pressure (unit (d)'s RSS also went *down* under load): the real `sindri serve` with its Temporal worker (`SindriWorkerHost`) and its dev server, 15 min at 60 s, minutes 0–5 idle (no open instance), 10 `RecipeWorkflow` instances started after minute 5, each holding an open desk activity (`temporal workflow count` = 10 to the end), Ollama running · dev server idle: footprint median 129, max 137 MB (RSS median 164, max 171) · with 10 instances: footprint median 133, max 134 MB (RSS median 181, max 182) · `sindri serve` (whole process, worker included): footprint idle median 106, max 106 MB; with 10, median 109, max 115 MB (RSS 73/76 idle, 108/111 with 10) · worker overhead, serve minus unit (d)'s no-Temporal probe host as the baseline (an upper bound: serve also carries the ledger, lease and API): footprint median 50, max 59 MB (RSS median 72, max 75 MB with 10) · memory pressure: swap used 6983 MB at the start, 6759 MB at the end (of 8192), `memory_pressure` free 59% → 63%, compressor pages 226440 → 187362 · samples: `p0-prover/footprint/samples.csv`, `pressure.txt`, `run.sh` · ready ≤ 5 s: not re-measured (unit (d): 0.66 s max headless; unit (a): serve start to `control.json` 1.03 s fresh) · **Unit (d)'s hour (RSS only, a probe worker):** dev server RSS idle: median 133, max 164 MB (11 samples, minutes 0–10) · with 10 instances: median 116, max 165 MB (50 samples, minutes 11–60; `temporal workflow count` = 10 throughout) · `sindri serve` RSS minus no-worker baseline: median 8, max 16 MB (worker process max 48 MB, baseline max 40 MB) · start-to-ready ×5: 0.66 s max headless (0.22–0.66), 1.10 s max with the web UI (0.21–1.10) · samples: 60 min at 60 s, Ollama running (`ollama serve` up in every sample, no model loaded), CSV at `p0-d/footprint/hour.csv` in the P0 scratch dir. How: pinned `temporal` 1.9.1 `server start-dev --db-filename <tmp>/temporal.db --ip 127.0.0.1 --port <free> --ui-port <free> --namespace sindri` (web UI on for the hour; ready = `temporal operator namespace describe sindri` succeeds, measured from launch, fresh db each run, `ready.csv`); RSS = `ps -o rss=` on macOS arm64. The worker is a stand-alone probe that mirrors serve's shape, not `sindri serve` itself (unit (a) owns that wiring): an ASP.NET Core slim host on 127.0.0.1:0 with a hosted service, in `worker` mode a `TemporalClient` plus a `TemporalWorker` for a workflow that waits on a signal, in `baseline` mode the same host without Temporal; both ran side by side under `dotnet` for the hour, and 10 executions were started at minute 10 | Pass: every number is under its threshold in both measures (footprint max 137 of 300 idle, 134 of 500 with 10; overhead max 59 of 150) |
| 5 | The AOT split builds with zero warnings and the host publishes framework-dependent; the Avalonia app still publishes NativeAOT | `dotnet build AgenticWorkflow.slnx -warnaserror`: 0 warnings, `IL*` outside `Aw.Sindri.Workflows`: 0 (grep of the normal-verbosity log: no `IL` codes at all; `IsAotCompatible=true` on Sindri Core, Features and Infrastructure, unset on Workflows, by `dotnet msbuild -getProperty`) · host framework-dependent publish: passes (`src/Sindri/aot-smoke/run.sh`: `dotnet publish -c Release --no-self-contained -warnaserror`, runtimeconfig names `Microsoft.AspNetCore.App`, no runtime beside it, `Temporalio.dll` and its native core bridge present, the published apphost matches `expected.txt`; output 271 MB because the portable publish carries the Temporal bridge for every RID) · Avalonia app NativeAOT publish: passes on a throwaway merge (never pushed, removed afterwards) of `feat/sindri-wf-p0-d` 5bb297ef with `origin/feat/sindri-s1b-ui-shell` 3e5eb8a2 (U0, PR #168), `AgenticWorkflow.slnx` and `Directory.Packages.props` union-resolved (Temporalio and Avalonia pins side by side), the other conflicts (docs, deps tooling, architecture tests) not touched by the app: `src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh` published `sindri-ui` NativeAOT for osx-arm64, 19 MB Mach-O, 0 warnings, no `IL` codes. The full merged solution wasn't built (those conflicts are U0's to resolve on rebase) · **Prover, merged build (2026-10-12):** `dotnet build AgenticWorkflow.slnx -warnaserror` 0 warnings, 0 errors (Debug and Release); `src/Sindri/aot-smoke/run.sh` ok (framework-dependent publish, 272 MB); the Avalonia app is still not on main (U0 open), so unit (d)'s throwaway-merge NativeAOT publish is the evidence for that clause | Pass |

### What each server supports, the evals and the ledger mirror

Tests: `tests/Aw.Sindri.Engine.Tests/Workflows/Spike/` (integration lane, about 4 s for the 19 tests, both servers started
from `binaries.lock` with `ExistingPath`; the SDK never downloads). The interpreter there (`SpikeStepWorkflow`) is
test-only, the eval harness: it walks `recipes/sindri/workflow.step.yaml` read by P1's `YamlRecipeReader`, with a decision
stub (`SpikeDecision`: first matching transition, identical findings park `no-progress`, a loop past its cap parks
`rounds`). Unit (b) built `RecipeWorkflow`, the foundation's interpreter, in parallel. The same evals also ran with PR #180's
`TransitionDecision` folded inside a Temporal workflow, on a local branch with #180 merged
(`throwaway/p0-c-real-decisions`, never pushed): all three pass, 0.54 s.

**Support.** Each row is one test that runs on both servers (`TemporalProbes`, bound by `TimeSkippingProbeTests` and
`LocalProbeTests`), or the named class.

| Feature (rev 8 §, §10) | Test | `temporal-test-server` 1.39.0 (time-skipping) | `temporal` 1.9.1 dev server |
|---|---|---|---|
| Update with a validator; a rejected Update records nothing; the same update id twice returns the first result (§5.2) | `Update_validator_rejects_and_a_repeated_update_id_applies_once` | yes | yes |
| Async activity completion through the task token, from outside the worker (§5.3) | `Async_completion_completes_the_activity_through_its_task_token` | yes | yes |
| Signals, including ones sent before the wait (§5.4) | `Signals_reach_the_workflow_and_wait_until_it_waits` | yes | yes |
| Child workflow, id `{parent}/child.1`, `ParentClosePolicy.Terminate` (§5.8) | `Child_workflow_returns_its_result_to_the_parent` | yes | yes |
| `DelayAsync` with time skipping (§5.5) | `DelayAsync_skips_time_only_on_the_time_skipping_server` | yes: 4 h in 74 ms | no skipping (by design): 1 s took 1.07 s |
| Timer fires while an activity is scheduled or open | `Four_hour_timeout_does_not_skip_while_the_desk_activity_is_open` (time-skipping) | **no**: time stays still while any activity is scheduled or open (F1) | real time only |
| Worker restart mid-Step resumes with the same run token (§10) | `Worker_restart_mid_step_resumes_with_the_same_run_token` | yes, first worker without a sticky cache | yes |
| Restart from a worker that had the instance in its sticky cache | `Worker_restart_from_a_sticky_cache_resumes_only_on_the_dev_server` | **no**: the task stays on the dead sticky queue (F2) | yes, 15 ms |
| Complete an activity by id (workflow id, activity id) | exploratory, not kept | **no**: the server fails the call (`missing method "getNamespace"`) | not tried; Sindri uses task tokens |

**Findings for P4.**
- **F1. A desk Step held open as an activity blocks its own timeout on the time-skipping server.** The Java test server
  doesn't skip time while any activity is scheduled or open, so §5.3's async-completion `RunStep` and §5.5's timer can't
  be tested together there (4 h timer still pending after 2 s; an unpolled activity blocks it too). What works, measured:
  `RunStep` records the desk task and returns at once, and the report arrives as the `complete` Update (§5.2 already
  lists `complete` as an Update) with a validator that refuses a stale run token; the 4 h timeout then parks in 3 ms of
  wall time, and the evals pass that way too (0.52 s in unit (c)'s run, 46 ms in the prover's). Also measured: an auto-skipping result wait begun while an
  activity was open didn't resume skipping after it closed (the test timed out at 30 s), so a test waits until the
  timer is in history before awaiting the result. The prover also found that an activity left open in an instance
  that was then terminated keeps time skipping off for the rest of that server's life (the shared-fixture `DelayAsync`
  probe timed out after the restart probes; it now starts a server of its own). **Owner decision (2026-10-12), engine
  spec rev 8.1 §5.3:** a desk Step is a short activity that records the open task, and the workflow waits for the
  `complete` Update, validated synchronously; agent Steps (P8) keep a heartbeating activity, and their timeout tests
  run on the dev server. The foundation's `RecipeWorkflow` keeps unit (b)'s async-completion desk activity; P4 builds the
  Update model.
- **F2. The time-skipping server never moves a task off a dead worker's sticky queue** (no fallback after 20 s with a
  1 s `StickyQueueScheduleToStartTimeout`). Restart tests there run the first worker with `MaxCachedWorkflows = 0`, or
  run on `StartLocalAsync`; the dev server resumes on a new worker in 15 ms after a graceful stop. After a real crash
  (journey 5, `kill -9` of `sindri serve` with `checks` open), the new serve's worker resumed the instance: a
  `GetInstance` query (not timed) answered first, and `complete --run 2.1` then returned `-> verify` in 0.16 s, so any
  sticky-queue wait fell on that first query. (Engine spec rev 8.1 §11.)
- **F3. Workflow code and `CA2007`.** Library code must write `ConfigureAwait(false)`, which leaves the workflow's
  deterministic scheduler; the probe workflows (`Workflows/Spike/`) live in the test project, where `CA2007` is off. Workflow code
  in `Aw.Sindri.Workflows` writes `ConfigureAwait(true)` instead (the foundation's `RecipeWorkflow` does; P4's keeps
  it). `CancellationTokenSource.CancelAsync` (`MA0042`) has the same problem; workflow code calls
  `Cancel`. (Engine spec rev 8.1 §11.)

**The ledger mirror.** `RecordEventActivities.RecordAsync` (`src/Sindri/Aw.Sindri.Workflows`) appends through S1's
`ILedgerStore` under the held lease, with the idempotency key `workflow:{workflow id}:{run id}:{activity id}` (engine
spec rev 8.1 §5.6, owner decision 2026-10-12), read inside the activity from its own `ActivityInfo`. Every activity,
local or regular, has a deterministic `ActivityId`, the same on every replay; no activity sees a history event id
(`ActivityInfo` has no scheduled-event-id field). Unit (c) first keyed it by a workflow-side counter; the prover changed
it test-first (`Replay_leaves_one_ledger_row_per_transition_keyed_by_the_activity_id`, red 98c8ec60, green 0395ca65).
The local activity ids are the SDK's activity sequence, shared with regular activities, so in the test interpreter the
four mirrored events are `1`, `3`, `5`, `7`. It is a
**local** activity, as §5.6 says: its result is a marker written with the workflow task, so a replay reads the marker
and never runs it again, and it costs no task-queue round trip or extra history events. The one case that runs it
twice, a worker that dies after the append but before the workflow task completes, is absorbed by S1's `Append`, which
looks the key up and inserts in one write transaction and returns `AlreadyRecorded`. A regular activity would give the
same at-least-once execution with three history events per ledger row, so exactly-once comes from the key either way
and the local activity is the cheaper of the two. Its retry policy is unbounded with backoff, so the workflow waits for
the ledger.

Each payload is scrubbed before it is appended (free text in an event's detail can carry a secret). Until the context lane's
scrubber is wired in, the engine's ledger scrub uses `Redactor.Default`, the built-in rules; the profile's
`context.scrub.extraPatterns` do not reach it yet. A payload the scrub blocks, or breaks so it is no longer JSON, is stored
without its detail; one that still can't be scrubbed throws, and the activity retries.

**How it was measured.** `AW_SPIKE_LOG=<file> dotnet test --project tests/Aw.Sindri.Engine.Tests -- --filter-namespace
Aw.Sindri.Tests.Workflows.Spike` appends each measurement line to the file; `AW_SPIKE_LEDGER_COPY=<dir>` keeps the
ledger replay test's `ledger.db`. Counted with `sqlite3 ledger.db "select type, count(*) from events group by type"`
(`workflow.started|1`, `workflow.transition|3`) and `select idempotency_key, count(*) c from events group by
idempotency_key having c > 1` (no rows). Wall times are `Stopwatch` around the awaited call, on the owner's Mac (arm64),
2026-10-11. The raw logs and CTRF reports are in the unit's scratch directory (`p0-c/final-spike.log`,
`p0-c/ledger-sqlite3.txt`, `p0-c/ctrf-final/`).

**Verdict: pass.** All five pass lines hold on the merged build, line 4 in both physical footprint and RSS. Temporal stays the substrate (rev 8); P4 starts from rev 8.1: desk Steps wait for the `complete` Update (F1), the ledger key is the RecordEvent activity id, and time-skipping tests follow F1–F3 (spec §11).

## The interpreter's interface (engine P4)

Engine spec rev 8.1 §5.1–§5.6, "Phase 4". The P4 contract fixes the interface; the units fill in the bodies.

| Part | Shape | Where |
|---|---|---|
| Workflow input | `InstanceStart`: the recipe `RecipeVersion (id, version, hash)`, the item, the attempt, the input facts (`InputFact`: the file name status shows, and the stored `FactValue`), the tenant and the ring | `Aw.Sindri.Core/Workflow/Engine/Runtime` |
| Workflow id | The address, `InstanceStart.WorkflowId()` (`{item}/{recipe-name}/{attempt}`) | same |
| `GetInstance` Query | `InstanceView`: the state, its detail and since, `next`, `allowedCommands` as `AllowedCommand` (`{ actionId, cli }`, UI E-13), the open `OpenQuestion` or `DeskTask` (the Step's declared outcomes and outputs, the inputs, when the timeout fires), the run token, the children and the outcome. `InstanceViews.Of` builds it, and it is pure | same |
| Updates (§5.2) | `complete(StepCompleted)`, `answer(AnswerGiven)`, `command(CommandFired)`, `cancel(CancelRequested)`, `takeover(TakeoverRequested)`, `handback(HandbackRequested)`, `open-request(OpenRequested)` (update id `wf:{instance}:open:{launch}`, from the engine). Each has a `[WorkflowUpdateValidator]` and returns `CommandResult` (the view after the command, and what happened). The update id is the nonce | `RecipeWorkflow` |
| Desk Step (rev 8.1 §5.3) | `RunStep` is a short activity with id `run:{token}:{n}`. The workflow then waits for the `complete` Update, bounded by the state's timer (`TimerScopes`), the state's timeout raised to `workflow.deskTimeout`; the expiry always parks `timeout` | `RecipeWorkflow.RunStep`, `RunStepActivities` |
| Events (§5.4) | The `event` signal carries a `WaitSatisfied` | `RecipeWorkflow.EventAsync` |
| Ledger mirror (§5.6) | `RecordEvent`, a local activity keyed `workflow:{workflow id}:{run id}:{activity id}` | `RecordEventActivities` |
| Client port | `IInstanceRuntime`: `Start` (UseExisting), `Get` (the Query), `Send` (the Update the trigger names, with the nonce as its id), `OpenRequest` (the engine's takeover, id `wf:{instance}:open:{launch}`), `Signal` (the bridge) | Core port; `TemporalInstanceRuntime` |
| Items | `IItems.Resolve` (an ItemId or a tracker alias). The stub `SelfItems` stands in until API P3 lands | Core port; `Aw.Sindri.Workflows` |
| Approved recipes | `IApprovedRecipes` (`Latest`, `Find`, `Check`) is the minimal read port the interpreter and the start and command slices use. `RegistryApprovedRecipes` implements it over P2's registry (`IRecipeStore`, `IRecipeApprovals`): a recipe starts only when it is registered and its latest approval isn't revoked (`SND-RECIPE-003`) | `Aw.Sindri.Core/Workflow/Registry` |

The Updates, their validators, the `IInstanceRuntime` methods and every `workflow` verb are built. Every Update on this path is built,
including `takeover`, `handback` and the engine's `open-request` (P5). A rejected
Update records nothing. `RecipeWorkflowTests` pins the interface (the desk task 1.1 with its declared outcomes through
the Query, `allowedCommands`, the ledger key, and replay), and `PhaseFourSignalTests` (integration) is phase 4 signal 1:
start `workflow.step` on an item, then status shows the desk task with run 1.1.

**The state loop (unit (a)).** `RunAsync` loads the recipe, enters `initial`, then repeats until a terminal state: an
active state runs its desk Step (`RunDeskStep`, unit (c)); a wait state, a park, an escalation or a takeover waits
(`RecipeWorkflow.Loop.cs`). Every trigger reaches the instance at a wait and is decided there, one at a time, by P3's
`TransitionDecision`: a command an Update hands over through `Submit(trigger)` (which returns the decision to the
Update), or a delivered `event` signal whose source fits the wait's `waitFor` (its event kind, its resource, or its
child's end). A delivered event that fits no wait yet stays buffered until one does. An applied or refused trigger writes
one ledger event (`workflow.transition` or `workflow.refused`) and sets the search attributes; a rejected one records
nothing. The row's payload is `WorkflowEventPayload` (instance, from, to, trigger kind, outcome, run) plus a `detail` projection of
the decision's event (`WorkflowEventDetail`, §5.6): the trigger's id (its key: a command's nonce and arguments digest, a
report's run and digest, an event's source sequence), actor, source sequence, answer variant and amount, any gates, evidence,
grant, override and refusal records, the effect identities and the post-state delta (state names, reasons, counters and fact
digests). It never holds an answer's text or a parked question, and no row but `started` holds text from a fact. The `started`
row carries the start's key and actor, plus the subject as named at start (`delta.subject`: kind, key and the label or rendered
name, §3.11: a text subject's key and label are cut to 40/80 and the rendered name to 40 per value and 80 in all, while a
link or payload subject's key and label are copied as stored) and each input's producer (`delta.producers`,
`engine:generate` for a generated input); a held event's expiry and the desk deadline's park, which have no decision, carry no `detail`. A matched transition's gates run in the `EvaluateGates` local activity (engine P7, "Children, gates and the item stubs" below); a gate with no runtime refuses (fail closed, W5) and the
refusal follows §5.8's precedence. A wait state's timer wakes the wait like a delivered event, and `TransitionDecision.Expired` decides the expiry (park, escalate, or retry once); a child wait starts its child as a Temporal child workflow (engine P7, below).

| Search attribute | Value |
|---|---|
| `Item`, `Recipe`, `Tenant`, `Ring` | From `InstanceStart`; `StartRequest` carries the tenant and ring into the instance |
| `State` | The state after each change (`parked`, `escalated`, `taken-over` while suspended) |
| `NeedsYou` | True while parked or escalated, or while an open desk task waits; false while taken over |
| `Repo` | `fact:item`'s `repo`, read once on start by the `ReadItemRepo` local activity; unset without one |

The loop's tests run on the time-skipping server: `InterpreterEvalTests` runs every shipped `workflow.step` eval case
through the interpreter and checks each line against the in-process `recipe eval`, and `InterpreterStateLoopTests`
covers the search attributes, a delivered event, a refused event's park (`NeedsYou` flips) and replay.

## Status, list and events (engine P4 unit (e))

Engine spec rev 8.1 §5.1, §5.6, §6.3, §6.4, S1-D6 and S1-D11; README X5 and X16.

All three are clients of serve's routes, like the write verbs: `status` resolves its reference with `GET /sindri/workflow/instances?address=` and
reads `GET /sindri/workflow/instances/{id}`; `list` calls `GET /sindri/workflow/instances` with `needsMe`, `item`, `state`, `recipe`, `urgency`, `subject`, `root`, `open` and `query`;
`events` resolves the same way and reads `GET /sindri/workflow/instances/{id}/events?after=` (`--follow` polls it). None starts serve:
with none running they print `SND-API-001 not running: …` and exit 1. Only `recipe validate` and `recipe eval` run in process (spec §6.1).
The `--json` output is the served body as the CLI's serializer prints it: `list` prints `InstanceList`, `status` prints the `current.status` fields of the instance detail (`StatusFields`: header, text, next, answer, commands).

| Verb | Slice | What it reads | Prints |
|---|---|---|---|
| `workflow status <address> [--json]` | `GetInstance` | the `GetInstance` Query (`TemporalInstanceRuntime.Get`) through `GET /workflow/instances/{id}`; an ended instance answers too | the §6.4 layout (the served `current.status.text`), or the `StatusFields` as JSON. Exit 2 with `SND-WF-001` for an address or id no instance has (the `address=` lookup), `SND-ITEM-404` for another repo's instance, `SND-API-002` when no worker answers in 10 s |
| `workflow list [--needs-me \| --item <id> \| --state <s>] [--query <q>] [--json]` | `ListInstances` | a visibility query on the search attributes, then each row's `GetInstance`; ended roots are counted from the listing without a Query | one line per instance in the API order (`<address>  <recipe>@<hash12>  <state>: <now>  since <time> (<age>)`; no version column); `--needs-me` prints **Decisions** (PARKED, ESCALATED and WITH YOU rows: parks and escalations, and a takeover's idle row once P6 lists it; a taken-over instance has `NeedsYou` false, so it is listed only through its `workflow.notified` row, which P6 delivers as `notify.desk`; listing idle takeovers under `--needs-me` isn't built yet, an owner item for the API / P6) then **Tasks** (DECISION rows: open desk tasks), each section by address. Empty: the sentence the server served for the filter: `nothing needs you` (`--needs-me`), `No workflows are open.` (bare `list`) or `No workflows match.`; exit 0 |
| `workflow events <address> [--follow] [--json]` | `GetInstanceEvents` | the ledger through `ILedgerReader`, never Temporal's history | one line per event: `seq  MM-dd HH:mm:ss  type  from -> to  trigger outcome  run r  actor`; `no events`. `--follow` reads after its cursor every second until interrupted |

- **`--needs-me` is served by the list route.** `GET /needs-you` (API P4) serves the owner's needs-you list, a different list; `ListInstances` serves this one with the
  visibility query `NeedsYou = true` (`needsMe=true`) over the unfinished instances, and drops a parent whose open
  question mirrors a listed child (`NeedsYouList.Shown`; spec §6.3: once, under the child). The CLI groups the rows by
  their chip into Decisions, then Tasks, each by address, and prints `nothing needs you` when no row falls under either.
- **Which instances.** Bare `list` and `--needs-me` list running instances; `--item` and `--state` list ended ones too
  (`IWorkflowInstances.List(query, ended, …)`). `--state` takes a state name (`[a-z][a-z0-9-]{0,63}`); `--query` adds a
  raw visibility query for scripts.
- **Version.** Temporal keeps no row version, so the instance's version is the count of events the interpreter has
  mirrored into the ledger (`workflow.started` and every transition): a new instance is `v1`. The workflow counts
  them in `Mirror` and sets `InstanceView.Version` in the Query. It shows as `header.version` of `status --json` and in
  the `instance:<address>@v<n>` challenge of the protected verbs; the list line has no version column.
- **Times.** Every clock time status prints is UTC (`since 11:54`; `since 10-09 11:54` once a day or more has passed),
  in the header and in `next` alike, because the Query has no local time zone. Durations read `1d 22h 48m`, or `40s`
  under a minute (`StatusText`).
- **What the view renders** (`InstanceViews.Of`, pure, so the Query and the goldens call the same code): the header's
  detail (`desk task`, the park reason with `(child)` for a mirrored park, `spec-conflict (<kind>)`, the escalation
  kind, the outcome when it isn't the state's name), `next` (§3.8's text per row, a park's instruction on its own line;
  a desk task's recipe `next` with its variables filled and the `workflow complete` line with the Step's first declared
  outcome and the outputs required on it), the answer line per park reason (`--grant 1`, `--grant-tokens <half the
  state's budget>`, `--text`, `--resume [--text]`, `--resume`, `--accept`, and the approval or cancel a spec-conflict
  needs), the drift line for a drift recorded behind a park, the budgets, and `allowedCommands`. A spec-conflict park
  whose only way out is an approval or a cancel (revoked, child revoked, drift) offers no `answer`. An open park or escalation serves one `answer` entry per fitting variant, each under its catalog id (`answer-text`, `answer-resume`, `answer-grant`, `answer-grant-tokens`, `answer-accept`, `answer-reject`), the status line's own variant first, each with the full command that runs it (the status line itself may keep its hint form, such as `--resume [--text <answer>]`; `--text <answer>` stays a template because the text is the person's). A suspension that mirrors a child serves only the variants its reason alone fixes (rounds, no-progress, timeout, no-runtime). An escalation from a child and a mirrored needs-answer, budget or spec-conflict park serve none, and their status `answer` line points at the child (`sindri workflow answer <address> <the child's answer>`, `(forwarded to <child>)`, action id `answer-text`) and is marked explicitly: `via: "child"` and `child: <the child's address>` on the status answer record (both omitted on every other answer; `child` is present only when the served view lists the mirrored child, matched by its instance id, and a parent's view lists its live children, each with the state that child answers now (engine P7); a child the status read cannot reach shows the state `unreachable`, never its last known one); the needs-you row carries the same `via` marker, so a client never reads the cli text to tell a pointer from a form. A budget grant's amount is whole `k` or `M` when exact, else the plain integer, so it parses (`TokenCounts.Format`) whenever a cap exists to halve; a budget park with no cap (a `gate.budget` refusal on a recipe with no budget) serves no `answer-grant-tokens` entry, and its status line keeps the `--grant-tokens <n>` hint. The one table of which variants fit is `AnswerVariants.Fitting`, shared by the answer check and the served list.
- **Layout** (`StatusLayout`): the labels take 12 columns, `next` wraps at 120, and the lines come in a fixed order:
  next, child, inputs, drift, timeout, paused, budget (beside an open desk task, as §6.4 shows it), answer, commands.
  An input's digest prints as its first four hex digits and `...` (ASCII for §6.4's ellipsis). The answer note is set
  off by five spaces. Golden files pin the six §6.4 layouts and one per §3.8 row P4 reaches
  (`tests/Aw.Sindri.Tests/GetInstance/Golden/`).
- **The event stream's filter (S1-D6).** `GET /sindri/events?item=<ITM id or alias>` keeps one item's rows: the item is
  resolved for the caller, from the ledger, and an item that isn't there, or that a session may not see, is a 404
  `SND-ITEM-404` before any frame. `?instance=<address>` keeps the rows an instance mirrored (keys `workflow:{address}:…`,
  so a child's rows stay out of its parent's); an engine row keyed by a tracker key (not an ITM id) is reached through
  `?instance=`, not `?item=`. The stream's cursor moves by `StreamEventsResponse.ScannedTo` (how far the read looked, also
  past the rows the filter or the caller's scope skipped).
- **Profile keys (S1-D11).** `workflow.deskTimeout` (default `2d`) and `workflow.eventHold` (default `24h`), each a
  duration (`1m`, `4h`, `2d`); a profile without a `workflow:` block keeps the canonical JSON (and so the hash) it had.
  The worker reads them through `ProfileWorkflowTimes`: `LoadRecipe` reads both when an instance starts, so the desk
  floor (unit (c)) and the interpreter's `eventHold` expiry come from history and a replay sees the same values. The dev profile sets `deskTimeout: 1m`.
- **Not yet:** approval doesn't run a recipe's evals. P2's `ApproveRecipe` only checks that the recipe declares them
  (`SND-RECIPE-008`); the Core port `IApprovalEvals` names the hook and has no implementation or caller yet. Binding it
  (moving `EvalRecipe`'s case running into Core) is a later phase's work.
  `workflow events` reads the ledger directly until API P3's item timeline exists.
- **The revoke wake and the drift park (P6):** `IRecipeWake` is `TemporalRecipeWake` (see "Registry wake (P6)"), so a revoke
  parks the running instances at once. A pin drift parks an instance lazily, as built: when one of its recipe effects hits
  the effect recheck or the run-time check (`SND-RECIPE-006`). The startup drift sender (engine spec §5.8: "on startup when
  the build's descriptor hashes differ from an approval's pins") is **not built in P6**; it is a named follow-up, so an
  instance that sends no effect after a drift keeps running until one does. The approval check at each command, answer and
  `complete` still refuses a revoked or drifted recipe with `SND-RECIPE-005`/`-006`.

## The event bridge (engine P4 unit (d))

Spec rev 8 §5.4, §7.2 S1-D8 and S1-D10. `EventBridge` is the built-in service `workflow-events` (a service name is
kebab-case, so not `workflow.events`). It runs every 5 s without a profile entry; a profile entry of the same name sets
its cadence. The scheduler gives every service its own loop: a run still in flight holds back no other service, and its
own slots that come due meanwhile are skipped, never overlapped.

| Part | Behaviour |
|---|---|
| Cursor | The last delivered ledger sequence, in the owner-only file `<home>/workflow/events.cursor`, written to a temporary name and moved into place. A missing file reads as 0. The cursor moves past an event once every target has it |
| Held window | Visibility lags a start, so an instance started just before its item event can be missing from the item query when the event goes out. Every item event the bridge delivers stays in an in-memory window (`HeldItemEvents`) for `HeldItemEvents.VisibilityLag` (1 minute from delivery), not `workflow.eventHold`. Each run ends by sending it to a live instance of its item that hasn't had it and **started at or before the event** (visibility's start time), one visibility query per item. An instance started after the event, such as a later attempt, never gets it. The workflow drops a repeat by its ledger sequence. The window covers the seconds visibility lags, not a serve restart. An event whose item isn't an item key is logged and skipped, and a delivery refused for a reason other than Unavailable or Timeout is logged and passed, so one bad row can't stall the cursor |
| Accepted events | A registered `EventDescriptor` kind whose envelope actor is the descriptor's `producer`. Every other event signals nothing |
| `route: item` | Every live instance of the event's item (a visibility query on `Item`). Temporal buffers the signal until the instance reaches the wait, so an early event isn't lost. The workflow drops a kind its recipe never waits for |
| `route: causation` | Only the instance that the reply's causing event names (`ICommandOrigins`: the causing ledger event's `instance` field, which is the design: no `workflow_commands` map) |
| Trigger | `wait.satisfied` from `EventWaitSource(kind, sequence)`, the payload's `outcome` (one the descriptor declares) and the descriptor's facts from the payload's `facts` |
| Idempotency | The ledger sequence is the key. `RecipeWorkflow.EventAsync` drops a sequence it has already taken, so a bridge that crashed between a signal and its cursor write re-sends harmlessly |
| Failures | An instance that has ended is skipped. A Temporal or ledger failure ends the run with the cursor before that event, and the next run retries it |
## Start, answer, command and cancel (engine P4 unit (b))

Engine spec rev 8.1 §5.1, §5.2, §5.8, §6.1–§6.3.

- **`workflow start <recipe> --item <id> [--attempt next] [--fact <key>=<file>]…`** runs the `StartInstance` slice: it
  resolves the item (`IItems`) and the approved recipe (the latest, or `id@hash`, a hash prefix of 8 or more digits),
  refuses a recipe registered before input kinds (`SND-RECIPE-001` with the fix; checked before a pin-drift refusal), a `fact:item` whose `id` isn't the item (`SND-WF-014`) and a missing declared input (`SND-CLI-003`; an input with a `generate` template is exempt, the start renders it, produced by `engine:generate`), stores
  each fact owner-only (`IFactStore`, at most 1 MiB, the CLI sends name and content, never the path), and starts
  `RecipeWorkflow` with the address as its workflow id (`WorkflowIdConflictPolicy.UseExisting`,
  `WorkflowIdReusePolicy.RejectDuplicate`). The attempts come from `IInstanceDirectory.Latest`, which describes
  `{item}/{recipe}/1`, `/2` … so a start never waits on visibility. Without `--attempt` the latest attempt is used (a
  live one is returned as it is; an ended one is shown with `start a new one with --attempt next`); `--attempt next`
  is refused while the latest is live (`SND-WF-012`). It prints the status header.
  A recipe whose Step names an input with a registered descriptor but no runtime in this build is refused with
  `SND-WF-016` naming `states.<s>.step.inputs[k]`, here and at `recipe approve` (the challenge too); `recipe show`, `recipe validate`
  and `recipe eval` still accept it, and eval ignores inputs. An input id with no registration at all is not this case: it is
  `RCP-BLOCK-001` at `recipe validate` and at register.
- **`workflow answer`, `command <verb>`, `cancel`, `takeover`, `handback`** run the `ApplyCommand` slice, in this order: resolve the address
  (a bare item is the root of its latest attempt, `SND-WF-006` listing the candidates when the item has roots of more
  than one recipe); check the arguments; claim the nonce for exactly this command (`SND-WF-009`); for a protected
  answer (`--grant`, `--grant-tokens`, `--accept`) redeem its challenge (`IChallenges`, P2's shape; action text
  `grant n <address>`, `grant-tokens n <address>`, `accept <address>`; subject `instance:<address>@v<version>`, the
  address and the instance version the preview showed, so any event mirrored since makes it stale; `SND-WF-013`); check the recipe's approval unless the trigger is exempt (cancel, takeover, handback; `SND-RECIPE-005`, `-006`);
  then send the Update with the nonce as its id. A refusal gives the nonce back. A repeated nonce skips the checks and
  Temporal returns the first outcome, printed `already applied (…)`. The CLI prints the status header and what happened;
  an answer prints the resume action it ran (`resumed (transition): -> produce`).
- **In the workflow**, each validator runs P3's pure `TransitionDecision` on the instance as it stands and rejects what
  it rejects (an answer that doesn't fit is `SND-WF-003`, its fix naming the answer that does), so nothing is recorded.
  An accepted Update hands its trigger to the loop through `Submit` (`RecipeWorkflow.Loop.cs`). The desk wait wakes for a submitted command as well as for the report. An answer
  that ends a suspension arms the state's full timeout again (`StateTimers.Between`), so `--resume` on a timeout park gets a fresh timeout from the answer.
- **`IssueNonce`** returns a nonce for an existing instance (class issue). API P1 has no nonce table yet, so the
  `POST /workflow/instances/{instanceId}/nonce` route issues them from unit (c)'s `HomeCommandNonces` stand-in.
- **Routes and ids (§6.3).** The write verbs are clients of the API's command chain: `POST /workflow/instances`
  (start), `/{instanceId}/nonce`, `/{instanceId}/commands` and `/{instanceId}/runs/{run}/complete`. `{instanceId}` is
  the instance's opaque id, `WFI-` and 32 hex digits: a UUIDv8 over the SHA-256 of its address
  (`WorkflowInstanceId.Of`, the same rule P3 uses for a child), so the same instance always has the same id. Start, list
  and get return it (`InstanceView.id`). A CLI address travels only in `GET /workflow/instances?address=<address or
  item>`, which answers the one instance it names; the CLI resolves there first, then calls the id routes. An address in
  a route is a 400 (`SND-CLI-003`); an id no instance has is `SND-WF-001`. The server finds an id among the instances it
  has started or shown (`KnownInstances`), else by minting the id of every instance visibility lists.
- **The start request.** `POST /workflow/instances` takes `{ recipe, item, nextAttempt, facts }`, where `facts` maps a fact key
  to `{ name, content }`. `content` is the base64 of the raw bytes (the System.Text.Json default for `ReadOnlyMemory<byte>`),
  not the text; the golden `workflow-instances-start/request.json` pins it, and `started.json` carries the sha256 of the decoded bytes.
- **Failures of an Update (§5.2, §5.3).** Each Update waits at most 4 s for a worker (`TemporalUpdates.Patience`): a
  command that first waited the 5 s command gate still answers inside the CLI's 10 s HTTP timeout. No worker in that
  time is `SND-API-002`, naming the nonce: the Update may still apply once a worker runs, so the claim stays and the same
  command with `--nonce <n>` is deduplicated. A Temporal without the settings' namespace is `SND-API-001`, not a missing
  instance. The same nonce after the instance ended (`cancel` twice) prints `already applied (-> cancelled)`: Temporal
  keeps the first outcome of an update id on a closed workflow.
- **Namespace and task queue** come from `AW_SINDRI_TEMPORAL_NAMESPACE` and `AW_SINDRI_TEMPORAL_TASK_QUEUE` (each
  `sindri` when unset; README X61). A Step's activity runs on its instance's queue; `StepTaskQueue.For` is the seam a
  later phase uses to route a Step elsewhere.

## Step inputs at run time

Before a desk Step opens, each input in its `step.inputs` list runs as a `FillInput` activity (id `inputs:{token}:{n}`, 60 s, 3 attempts), in declaration order, once per run token. The activity runs the input in its own scope, checks the section names (`^[a-z][a-z0-9._-]{0,63}$`), scrubs each section and the typed result, then hashes the scrubbed text, so the digest covers exactly what a Step sees. A section or a typed result is at most 256 KiB and a plan at most 1 MiB (`SND-WF-019`, which fails the Step without retries).

- **No text is stored.** The activity returns `{section, input, bytes, sha256}` per section and the size and digest of the typed result; that metadata is all Temporal records. The text lives in `MemoryInjectionTexts`, keyed by instance address, state, run token and input, bounded (64 entries, 8 MiB), dropped when a newer run of the instance arrives, and never persisted or logged. A history recorded before the `step-inputs-v1` patch replays without filling.
- **A later section of the same name replaces the earlier one** in place (`InjectionPlans.Merge`, pure); the plan keeps a warning for each replacement.
- **A throwing input fails the Step**: `step.failed` with the reason `input <id> failed: <error code>` (never the message), which the recipe's `on: step.failed` routes. No desk task opens.
- **An input whose runtime is gone after approval parks the instance** with the existing `ParkReason.NoRuntime` (shown as `SND-WF-010`); the question reads `input <id> of <step> has no runtime in this build (SND-WF-016; fix: install the block pack or rebuild, then restart)`. A restart runs Fill again and parks again the same way while the runtime is missing; nothing fails and nothing loops.
- **An in-process Step** gets `StepContext.Injection` (the sections) and `StepContext.InputResults` (the typed results, keyed by the input block id, or `id#n` (n = the entry's place in the Step's input list) once the Step lists that block twice; read-only, empty when it has none) from `StepContexts`. On a cache miss the input runs again and its digests must equal the recorded ones; otherwise `SND-WF-018` and the Step does not run on different data.

### The context inputs (S-LB2)

The scoping recipe's `gather` Step names three real inputs, so `sindri recipe approve` of `workflow.scoping` passes the `SND-WF-016` check only when this build has a runtime for each. All of them live in `Features/ContextCalls` (they share `ContextInputScopes` and `ContextInputFacts`) and are registered by `AddSindriFeatures`.

| Input | Section | Typed result (`StepContext.InputResults`) | Reads |
|---|---|---|---|
| `input.context.lenses {anchors, depth?, hops?}` | `lenses` | `LensesSetView` (schema 2) | `IContextEngine` lenses on the current map |
| `input.context.history {anchors, window?}` | `history` | `HistoryView` (no live sync marker: a fill depends on the stored state only) | `IContextEngine` history; `window` is `{since?, before?}` (RFC 3339 or a date) or a bare string meaning `since` |
| `input.context.code {anchors}` | `code` | `CodeView` | `IContextEngine` code on the current map |

- **`service:engine`, the instance's repo.** Each call runs as the engine's service caller with a scope cut to the one repo of the instance's item (`StepInputContext.Repo`, from the ledger), never from a caller (`ContextInputScopes`).
- **Honest sections.** An empty anchor list, a bad window, an item with no repo, a repo outside the approved profile, no approved profile, and any refusal from the engine (`SND-CONTEXT-*`) become one section ("The context lenses are unavailable for this Step (code: message); it runs without them.") with no typed result. They never throw; a degraded source stays visible in the view's `searched` rows.
- **A fill is a function of the stored state.** A re-fill after a restart or an eviction must give the same digest (`SND-WF-018` otherwise). So the views are made stable before they are rendered (`ContextInputSections.Stable`): `asOf` is the map's creation instant, the map's age is unstated (`ageDays` is -1, the text omits it), and the searched sources' instants are left out. A real change between fill and re-fill still gives 018, which is the rule: a new map version, and also a change of the repo's HEAD (the coverage and an open history window read live HEAD), counts as a change.
- **Bounded.** Each section is at most 120 KiB, and each typed result at most 256 KiB, the runner's cap for a part. A typed result is read by a code Step, not put in a prompt, and a large map's lenses view is over 120 KiB as JSON. A plan over 1 MiB fails the Step (SND-WF-019), loudly, never by dropping a result. Text is cut at whole lines with "[N more lines not shown: this section is capped at 120 KiB]"; a typed result over the budget is left out and the text says so. A Step never fails (SND-WF-019) because a map is big.
- **Arguments.** A fact-valued argument (`anchors: { fact: fact:anchors }`) is read through the fact-content port (`ContextInputFacts`, one helper for the three inputs) and checked against its SHA-256: the content is a JSON list of anchor keys (or `{"anchors": [...]}`), or a single key as text; a fact that is absent or can't be read is an unavailable section naming it, and content that no longer matches its SHA-256 fails the fill with `SND-WF-018` (the Step does not run on other data). `IStepInput.Fill` returns `Result<InjectionSections>`: an expected failure that must fail the fill (changed data, `SND-WF-018`) is a coded `Error`, which the runner returns on the path of its own refill-digest mismatch; everything else is a section. A history `window` of the wrong shape (a list, a number, a boolean, or a `since`/`before` that is not text) is an unavailable section naming the argument (an absent or null window is the open window, with a note; the engine does not pass a `{ profile: … }` window to inputs, so the recipe's `workflow.scoping.historyWindow` is named in a note and the window stays open), and a window key other than `since`/`before` is named in a note; anchors, `depth` or `hops` that cannot be used, and a `hops` list with non-text items, are named in a "note:" line at the head of the section instead of being dropped silently; an empty `hops` list is the default hops.
- **Text.** The section is the same plain text the `sindri context lenses|history|code` verbs print, with the untrusted labels fenced once (the engine fences them for its in-process caller; the input does not fence again); the typed result is the view in the route's JSON shape. The runner scrubs and caps both (256 KiB per section).

## Effects as activities (engine P6)

Engine spec rev 8.1 §5.7. A transition's effects run after the transition's ledger event, each on its own path in
`RecipeWorkflow.Effects.cs`: `RecordEffect` writes its `outbox.pending` row (a local activity keyed like `RecordEvent`),
`Workflow.DelayAsync(workflow.dispatchDelay)` waits out the undo window (cancelled with the instance), `RecheckEffect`
(a local activity) checks the recipe's approval and the approved profile's `workflow.effects`, and then `RunEffect`
(a regular activity) calls the block's `IEffectKind.Dispatch`, writes `outbox.dispatched` and appends the reply event
(`notify.delivered`, `claim.created`) with that row as its causation, so the bridge routes it to this instance only.
A refusal writes `outbox.refused` and hands the instance an `OutboxRefused` trigger: it parks with `spec-conflict` of
the code's kind, or is absorbed when the same revoke already parked it.

- **Effect rows are ledger events**, payload `EffectRow` (identity, command id, kind, origin `recipe` or `engine`, state
  `pending`, `dispatched`, `refused` or `cancelled`, instance, code, reason). `outbox list` folds them by command. Kernel's
  `outbox_commands` and `IOutbox` are not used by the engine.
- **Claim cleanup (engine P7, IDEA-033).** After any ending, `cancelled` included, `RecipeWorkflow.Cleanup.cs` runs the
  `ReleaseClaim` activity in a detached scope (a cancel of the workflow itself does not skip it): if this instance still owns
  the item's claim it deletes `refs/sindri/{user}/{item}` and frees the `workflow_claims` row; a claim another instance took
  over is left alone. Histories without the `p7-children` marker run no cleanup. P8's worktree removal joins the same seam.
- **Identity** is `{instance id}:{entry}:{index}`; an engine notice's is `{instance id}:{entry}:n{version}`
  (`EffectIdentity.ForNotice`); `effect.claim.ref` keys by item through `workflow_claims`. A re-emission (a `retry`, a
  resumed dispatch park) gets a new command id under the same identity, and the block's existence check makes it harmless.
- **Profile keys (S1-D11):** `workflow.effects` (the allowlist) and `workflow.dispatchDelay` (default 10 s; the dev
  profile sets `[effect.notify.desk, effect.claim.ref]` and `60s`), read through `EffectSettings`.

| Unit | Owns |
|---|---|
| (a) | `EffectActivities.RecordAsync`, `RunAsync`; `RecipeWorkflow.Dispatch` and its call from `Apply`; the dispatch delay from `LoadedRecipe.DispatchDelay`; the `retry` re-emission (`TransitionDecision.Retry` returns the entering transition's effects, which `Emit` dispatches); the effect ids in the transition's payload |
| (b) | `EffectRules.Recheck`; `EffectActivities.RecheckAsync`; `TransitionDecision.OutboxRefusal` (the `outbox.refused` park or absorb) and `Reemit` (`--resume` on a dispatch park) |
| (c) | `NotifyDeskEffect`, `IDeskNotices` and its adapter (`notifications.jsonl`, macOS notifications); `EngineNotices.For` and the engine-origin notices; `ListOutbox`, `GET /outbox?state=`, `OutboxCommandTable` lines |
| (d) | `ClaimRefEffect`, `IWorkflowClaims` and the `workflow_claims` migration (ledger.db, version 6); `EffectSettings.Of`, the two keys in `ProfileRules` and `ProfileDiff`, RCP-EFFECT-001 against the allowlist, `workflow.dispatchDelay` in `LoadRecipe` |

The contract's red test is `PhaseSixSignalTests` (Phase 6 signal 1 on the time-skipping server).

Unit (a), as built:

- **Dispatch** runs as a workflow task the loop doesn't await, started after every mirrored change (start, `Apply`,
  the desk report). Recipe effects wait `LoadedRecipe.DispatchDelay` in a cancellation scope; a `cancel` within the delay
  aborts the effect with an `outbox.cancelled` row (state `cancelled`, no code, reason starting `cancelled`; changed by
  unit (b): a cancel is not a refusal, and no recheck runs). Engine-origin
  notices skip the delay and the recheck. The instance returns only once every effect it emitted has settled.
- **One run per identity:** an identity already dispatched, or still on its way, is skipped, so a `retry` re-emission
  (`TransitionDecision.Retry`'s effects, through `Emit`) writes nothing twice. `RunEffect` keys its rows by identity
  (`workflow:{address}:effect:{identity}:dispatched`, `…:reply`, under the instance's address so `workflow events` lists them; envelope ids derived from the keys), so an activity retry
  writes nothing twice either.
- **The reply** is the one causation-routed event that declares the block's outcome, appended as its producer
  (`system:dispatcher`) with the dispatched row as its causation; payload `EffectReply` (`outcome`, `effect`).
- **Blocks by id:** `AddEffect` also registers the block keyed by its id; `RunEffect` resolves it in its own scope
  through the `EffectDispatch` registration.
- The transition's ledger payload lists its effect ids (`effects`, left out when there are none).

Unit (b), as built:

- **`RecheckEffect`** (`EffectActivities.RecheckAsync`, a local activity) reads the recipe's approval through
  `IApprovedRecipes.Check` (P2's `IRecipeApprovals` and `PinRules.Check`) and the **approved** profile snapshot through
  `IApprovedProfile.Latest`, never the live file. It reads `workflow.effects` straight from `WorkflowSettings.Effects`
  (unit (d) validates the key; an id that doesn't parse is not allowlisted). A read that fails throws, so the local
  activity retries; a running recipe with no approval row is treated as revoked. Engine-origin effects read neither.
- **`EffectRules.Recheck`** is pure: an engine-origin `effect.notify.desk` passes; any other engine-origin kind is
  `SND-WF-011` (`Engine_origin_limited_to_notify_desk`); a recipe effect is `SND-RECIPE-005` when revoked,
  `SND-RECIPE-006` when drifted, and `SND-WF-011` when its kind isn't allowlisted or the approved profile has no
  `workflow.effects` (or none is approved).
- **The refusal** writes `outbox.refused` (code and reason) and hands the loop `OutboxRefused`, which carries the
  effect as it was emitted (`RefusedEffect`: kind, identity, `with`), so the park names it and a resume re-sends it
  without looking it up in the recipe, even after the instance has entered another frame, and its `Origin`, so a re-send
  keeps it: an engine notice is re-sent as the engine's (interrupt class, its own text, no reply), a recipe effect is checked
  again like any recipe effect. `ResumeAction.Effects` is a list of `RefusedEffect`s.
- **`TransitionDecision.OutboxRefusal`**: `SND-RECIPE-005` parks `spec-conflict (revoked)` with resume `none` (only
  `cancel`), absorbed when already parked for a revoke; `SND-RECIPE-006` parks `spec-conflict (drift)` with resume
  `rearm` and the effect kept on it, absorbed when already parked for drift; `SND-WF-011` parks
  `spec-conflict (dispatch)` with resume `reemit`. A refusal reaching an ended instance is absorbed: the
  `outbox.refused` row records it, and `workflow.effect-refused` stays unused.
- **`Reemit`** (`answer --resume` on a dispatch park) ends the park and returns the effect as a new command under the
  same identity (a new command id, a new `outbox.pending` row, the delay and the recheck again), listed on the
  answer's event.
- **Drift approval:** the `recipe.approved` wake ends a drift park wherever it sits and re-sends the effects it carries
  (see the park invariants below).

Unit (d), as built:

- **Profile keys (S1-D11):** `EffectSettings.Of` reads `workflow.effects` (null when the key is absent; an empty list
  allows nothing) and `workflow.dispatchDelay` (the grammar of a service cadence, `<n>s|m|h` from 5s to 24h, so `60s`
  works; default 10s). `ProfileRules.Check` refuses an entry that isn't an `effect.*` id (`workflow.effects[i]`) and a
  delay outside that grammar; `ProfileDiff` names both keys. Both are left out of the canonical JSON when absent, so an
  approval made before P6 keeps its hash.
- **The approved snapshot everywhere:** the recheck (unit (b)) now reads the allowlist through `EffectSettings.Of` on
  `IApprovedProfile.Latest`; `LoadRecipe` takes `workflow.dispatchDelay` from the approved profile too
  (`ProfileWorkflowTimes.DispatchDelay`; a read that fails is retried, none approved is 10s), so the undo window is the
  one the owner approved, and a replay sees it from history. `RCP-EFFECT-001` reads the same snapshot, both in
  `recipe validate` and in the served validation `recipe approve` runs (`IRecipeProfileFacts`, read by `RecipeRegistryReader` and `ValidateRecipeService`).
- **`workflow_claims`** (ledger.db, migration 6: `item` primary key, `owner`, `attempt`, `claimed_at`) is the claim.
  `IWorkflowClaims.Take(claim, ended)` inserts for a free item, takes over a holder the caller found ended, and
  otherwise returns the holder, inside one write transaction. Liveness is `IInstanceLiveness` (Temporal: the execution
  runs), never a column.
- **`effect.claim.ref`** reads `fact:item` back (`IFactContents`), takes the user and the repo's path from the approved
  profile, takes the claim, and creates `refs/sindri/{user}/{item}` with `git update-ref` (empty old value) at the
  default branch's head (`origin/HEAD`, else `main`, `master`, `HEAD`); `git show-ref sindri/<user>/<item>` finds it.
  It never moves an existing ref and touches no branch or working tree. Replies: `created` (taken now, already held by
  this instance, or taken over from an ended owner, whose marker is reused) and `claimExists` (another live instance
  holds it; nothing is written). A repo with no commit, a repo key the approved profile lacks, or no `fact:item` is an
  error (`SND-WF-010`) that `RunEffect` does not retry (non-retryable): the effect is refused and the instance parks
  `spec-conflict (dispatch)`; resume sends it again once the setup is fixed.
- **`--attempt next`** is refused with `SND-WF-012` while the latest attempt of the recipe is live (P4) **or** while the
  item's claim is held by a live instance (`ClaimHolders.LiveHolder`), naming the holder.
**Park invariants (P6).** The instance holds its suspension and a queue of pending parks (`PendingPark`, then
`LaterParks`; the wire shape still shows the first pending park). (a) A refused effect is never dropped, except by a revoke
(which replaces every park: only `cancel` follows, and the `outbox.refused` rows keep the record): it is held by
exactly one `spec-conflict` drift or dispatch park until a resume or approval re-sends it; a refusal joins the first such
park of its own kind anywhere (current or queued), else the first of any, de-duplicated by identity, else a new dispatch
park of its own, which becomes the suspension when the instance has none and otherwise joins the queue. One rule ends every suspension (`Suspensions.EndCurrent`: answer, handback, drift approval, child
release): when a queued park takes effect, the ended park's effects stay queued as an approved holder; when nothing does,
the effects of every approved holder are released and the caller sends them (`WithReleased`); none may drop them. (b) A takeover
never drops a park: what it covers goes first in the queue, and the handback or the next ending makes the first queued
park the suspension. (c) A revoke wake, and a drift wake when one is sent, always end in a `spec-conflict` park (a revoke
replaces a takeover by design; a drift wake queues behind other parks). In P6 a drift park comes from the effect recheck
(`SND-RECIPE-006`), not from a drift wake: nothing sends `RecipeWakeKind.Drifted` yet (a named follow-up). (d) A drift approval ends the drift park wherever it sits; behind a
takeover or another park it is marked approved and its effects re-send once nothing else holds the instance.

**Registry wake (P6).** A revoke or an approval of a recipe that was already approved (a drift approval, or the same pins
again) wakes the running instances through the `wake` signal on `RecipeWorkflow`. The signal also carries the `Drifted` kind
for the startup drift sender (engine spec §5.8), which is not built in P6: nothing sends it. `TemporalRecipeWake` finds them by the `RecipeHash` search attribute (open `RecipeWorkflow` executions
with that hash) and signals each as `service:engine`. A missing Temporal for the home means nobody runs and the wake
succeeds; a listing that answers `NotFound` or `InvalidArgument` (not this home's Temporal, or no `RecipeHash` attribute
yet) is an empty list; an instance that ended between the listing and its signal (`NotFound`) is skipped; any other
signal failure doesn't stop the rest: every instance is tried, then one `SND-API-001` error names the failures and the
retry: for an approval, `sindri recipe approve <recipe>` again (same pins records nothing and wakes again); for a
revoke, the revocation is recorded: run the revoke again to finish any cascade it didn't reach and send the wake again (refused
SND-RECIPE-005 when nothing is left); an instance the wake missed is refused at its next command, answer, complete or effect
(SND-RECIPE-005) and parks then.

Unit (c), as built:

- **Engine notices** (`EngineNotices.For`): a new park or escalation interrupts; a `workflow.notified` change of a
  taken-over instance is takeoverIdle's and interrupts ("hand back {item}?"; P5 writes that row, and `Apply` passes
  the change's event type); a newly open desk run interrupts if it is the instance's first desk task and is `info`
  after that. A takeover, a resume into the same run, an ended instance and a `retry` re-emission send none. Each
  notice is `effect.notify.desk` with origin `engine`, identity `{instance id}:{entry}:n{version}`, and its class and
  text in its `with` (`class`, `text`).
- **`effect.notify.desk`** sends a `DeskNotice` through `IDeskNotices`. A recipe's notice is `info`, its text the
  entered state's rendered `next` (the interpreter passes it as `EffectRun.Text`) with the instance address beside
  it, and it replies `notify.delivered` with `delivered`; an engine notice replies nothing.
- **`notifications.jsonl`** (`FileDeskNotices`): one JSON line per identity (`identity`, `instance`, `item`, `class`,
  `text`, `at`), so a repeat writes nothing. An `interrupt` is then shown as a macOS notification through `osascript`
  (`IOsNotifier`, internal to Infrastructure; title `Sindri: <address>`); an `info` one isn't, and is collected into
  `$AW_SINDRI_HOME/digest/<yyyy-MM-dd>.jsonl` (UTC). The spec defines no digest delivery. Every test composition
  (`WorkflowsHarness.Sindri`, `Served`) replaces the notifier with `FakeOsNotifier`.
- **`outbox list [--pending | --dispatched] [--json]`** and **`GET /sindri/outbox?state=`** (`pending`, `dispatched`,
  `refused`, `cancelled`; another is SND-CLI-003) read the ledger's `outbox.*` rows through `ILedgerReader`, fold them
  by command (latest row wins, oldest emission first), and print `seq  time  state  kind  origin  instance  identity`,
  a refusal as `refused (<code>)`, or `no commands`. `outbox.cancelled` (unit (b)'s state) is its own `cancelled`
  line, neither pending nor dispatched.

## Takeover, handback, takeoverIdle and open-request (engine P5 unit (b))

Engine spec rev 8.1 §3.8, §5.2, §5.5, §5.8, §6.2; UI spec E-6, E-19.

- **`workflow takeover <address>`** (also `sindri takeover <item-or-address>`, a bare item being the root of its latest attempt; class `pause`: an agent session may take over, so the attach hook works) and
  **`workflow handback <address>`** (also `sindri handback <item-or-address>`; class `desk`: a session is refused `SND-AUTH-002`) are `ApplyCommand` kinds
  `takeover` and `handback`, sent as the `takeover` and `handback` Updates with the nonce as id. Both are exempt from
  the approval check. While the workflow is still open, a takeover of an ended or already taken-over instance, and a handback of one
  that isn't taken over, are `SND-WF-003` from the validator and record nothing. Once an ended instance's workflow has
  closed, Temporal has no running execution to take the Update: the command is `SND-WF-001` ("no running instance"),
  as for `cancel` and `command` (the same open/closed split holds for them).
- **Timers.** A takeover cancels the state timer's scope and keeps what was left (`RemainingTimer`, from
  `Workflow.UtcNow`) on the takeover's suspension; status shows it as `paused  timeout 40s left`
  (`InstanceView.paused`). No state timer fires while taken over (`StateTimers.Between` decides `arm`, `pause`, `resume`, `keep` or `stop`). The handback re-arms that timer with what was left
  (`TimerScopes` applies the decision). A takeover over a park
  has no timer to pause and the handback returns to the park; a park that arrives while taken over waits as the pending park
  and takes effect on the handback. A cancel while taken over stops every timer.
- **takeoverIdle.** A takeover arms a 2 h timer in its own scope. When it fires while still taken over the engine
  records one `workflow.notified` row and clears it; it never hands back. P6 delivers it as `notify.desk`
  ("hand back {item}?").
- **open-request (E-6).** `IInstanceRuntime.OpenRequest(address, LaunchId, …)` builds the open-request (actor `service:engine`) and sends it as the `open-request` Update with id
  `wf:{instance}:open:{launchId}`: no nonce, actor `service:engine`, past the API chain and exempt from approval. It
  takes over wherever a takeover is allowed, is a no-op (nothing recorded) when the instance is already taken over,
  and is `SND-WF-003` on an ended instance whose workflow is still open (`SND-WF-001` once it has closed), for any actor other than `service:engine`,
  and for a launch that is not a launch id (`LCH-` and 32 lower-case hex digits). One that arrives while the
  instance is still starting waits for the start, then takes over. Temporal deduplicates the id, so a repeat applies once. The ledger row
  carries the launch id as `causation`. Its sender is the launcher service (API P10a, `Features/Launching/TakeoverWait`), which waits for the result inside the request's 30 s deadline.
- **Start taken over (E-19).** `StartInstance` reads the item's launches (`ILaunchRequests.Requested` returns a `LaunchId?`: `LCH-` and 32 hex digits); when one is still
  requested the instance starts taken over (`InstanceStart.OpenLaunch`, actor `service:engine`), with the launch as the
  `workflow.started` row's `causation`, the state's full timeout kept for the handback and takeoverIdle running. The adapter
  (`LaunchRequestsView`) reads the `launches` view: the launch still `requested` and inside its 30 s deadline; a key that is no item id has none.

## Block runtime (DT-J2)

A Step whose descriptor says `runtime: block` and that has a registered `IBlockStep` runs without a desk: the workflow
opens a run with no launch and no desk task, calls the `RunBlockStep` activity (one attempt, no retry; heartbeating so a
`cancel` is delivered) and feeds the result back as `step.completed` (actor `service:block:<step>`), validated like a
desk report. An invalid result, a failure or a run past the descriptor's `maxDuration` becomes `step.failed`; the route
sends it on to the desk twin. A Step with no registered runtime keeps opening a desk run and stays human-only.

- **A desk report on a block run is refused** (`SND-WF-003`): the engine runs this Step, so only its own result applies. The
  human waits for the result, runs `cancel`, or answers the desk twin once the run fails. There is no takeover override:
  a takeover cancels the activity and pauses the run, and a `complete` while taken over is refused like any report to a
  parked instance (`is parked (Takeover); hand it back first: sindri workflow handback <address> (the handback runs the Step again)`);
  after the handback the Step runs again (a second Judge call) unless the takeover covered an unapproved park, so no report follows it. A takeover that covered an unapproved park hands back to that park (`…handback <address>; a park follows, and sindri workflow status <address> shows the answer it takes`), and a plain park is refused with `answer it first: sindri workflow status <address> shows the answer it takes`: each park takes its own answer (for example `--resume`, `--grant-tokens`, `--grant`, `--text`, `--accept` or `--reject`), and `status` shows the one it takes. A desk run's refusal after a takeover with no unapproved park ends `then complete run <token>` instead.
- **The `step.failed` reason** (what `sameFailure` compares and `--record` writes) is exactly one of:

  | Reason | When |
  |--------|------|
  | the Step's own text, as it returned it (`no provider answered`) | the `IBlockStep` returned `BlockStepResult.Failed(text)` |
  | `invalid-result: <code> <message>` | the result fails the desk-report validation of WF §5.3 (an undeclared outcome, a missing required output) |
  | `timeout` | the activity ran past its bound (below), or stopped heartbeating (a heartbeat timeout reports `timeout` too) |
  | `failed: <ErrorType>` | the Step threw; the type is the exception's, `error` when it has none |
  | `profile-unreadable: <code>` | the approved profile could not be read for the `providers` list (`SND-BLOCK-002`) |
  | `repo-unreadable` | `fact:item` could not be read, so the per-repo keys are unknown (`SND-BLOCK-003`); the box list is never used instead |
  | `the fact <key> can't be read: <message>` | a `{ fact }` argument or a fact the Step names could not be read (`SND-BLOCK-001`) |
  | the store's message (`disk full`) | an output could not be stored |

  (The workflow also falls back to `failed` and `the Step returned nothing` when Temporal reports no inner failure or
  the outcome is empty; the runner never produces either.)
- **The bound:** the activity (queue time included, `ScheduleToCloseTimeout`) is bounded by the descriptor's `maxDuration`
  and by the time left on the state timer less a second (`BlockLimits.Bound`), so after a takeover and handback, when the
  timer resumes with only what was left, the Step's own `timeout` still ends the run before the timer would park it.
- **`providers` is checked at validate time:** a literal list may name only `jev`, `claude-cli`, `codex-cli`, `cursor-cli`
  or `rules`; a non-list or a `{ fact }` is refused (`RCP-BLOCK-003`).
- **Which Steps:** `LoadedRecipe.BlockRuntimes` records the registered block ids the recipe uses, so a worker with other
  registrations replays the same set.
- **Replay: patch ids.** `Workflow.Patched("dt-j-block-runtime")` is the first call in `Begin`. A history started
  before DT-J has no marker, so it replays with the empty set and a `judge.choose` run stays a desk task exactly as
  recorded (`A_pre_DT_J_history_replays_on_the_desk_path`). New instances write the marker at start. The patch is
  deprecated only after the cutover.
- **Events:** a block run's transition (a result or a `step.failed`) carries `via: block`; a result also carries the judge `decision` id when the Step recorded one. `sindri workflow
  events` prints `via block · decision <id>` (a `sindri workflow why` verb that explains the decision comes with DT-J3); the API's event detail has `via` and
  `decision`, left out when null. Desk reports carry neither.
- **Profile:** `workflow.judge.providers` (and `repos[].workflow.judge.providers`) narrows which providers a Judge
  Step may ask; see `sindri-profile.md`.

## Agent runtime (P8)

A Step whose descriptor says `runtime: agent` runs on an agent session when the approved profile sets `workflow.agent.target`
(see `sindri-profile.md`) and a launcher is registered. Otherwise it opens a desk task as before. The workflow launches the
session once (`LaunchAgentStep`, id `agent-launch:{run}:{n}`; a repeated request for the same launch id reuses the
session), watches it (`WatchAgentStep`, heartbeating), and on an accepted report closes the run (`CloseAgentStep`: the
usage, the measured empty diff, and the stop of the session) before it decides.

- **The report is the `complete` Update**, validated like a desk report. The session sends `X-Sindri-Launch-Secret`
  (`AW_SINDRI_LAUNCH_SECRET` for the CLI); another caller gets `SND-AUTH-002`, a wrong secret `SND-AUTH-003` (it consumes no
  nonce). While the instance is taken over the secret is refused and only the human completes it, through the desk.
- **A park or a takeover leaves the session running;** the watch resumes after the handback and attaches to the same
  launch id. A `cancel` or the end of the run stops the session. A session that ends without a report is
  `step.failed` (`exit`, or `lost` when it vanished), and the loop rules apply to it as to any failure.
- **Status:** an open agent run adds `run: { runtime: "agent", launch: "LCH-…" }` to the status fields and a `run` line to
  the text (`run       agent LCH-…`). A desk run and a block run have neither; the member is left out of the JSON when
  null, so every other status golden is unchanged. An agent run has no desk task, so `commands` lists `takeover` and
  `cancel` and not `complete`. Golden: `Golden/Api/workflow-status/agent-run.json`.
- **Events:** an accepted agent report's transition carries `via: agent` and `tokens: N` (the usage the close step read, 0
  when it could not be read); `sindri workflow events` prints `via agent · tokens N`. The API's event detail has `via`
  and `tokens`, left out when null. A desk report carries neither.
- **Replay:** `Workflow.Patched("p8-agent-runtime")` follows `dt-j-block-runtime` in `Begin`, so a history started before P8
  replays with no agent Steps and its Step stays a desk task (`A_history_before_P8_replays_on_the_desk`).
- **A failed launch counts no round:** a launch that never started a session ends the run as `step.failed` with reason `launch`
  (`StepFailed.LaunchReason`); when the recipe's failed transition re-enters the looped state, the round stays, and the loop
  counts the failure in `LoopState.LaunchFailures` (a counted round resets it). The third in a row (`LoopRules.MaxLaunchFailures`)
  parks the instance with `needs-answer` (kind step, resume `rerun`) and the last launch error as the question. The decision
  is `TransitionDecision`'s re-entry; `Workflow.Patched("launch-failure-keeps-round-v1")` (after `ln-wf3-heavy-slot`) sets
  `DecisionContext.LegacyLaunchRound` for a history recorded without it, which counts the round as it did.
- **The dev recipe:** the interpreter tests (`AgentRuntimeInterpreterTests`) run an inline `workflow.dev-agent`: a `write`
  state whose Step is `step.report.write` (agent runtime), a state budget of 1000 tokens and a 1 minute timeout, with a fake
  launcher and fake sessions. It is not a shipped recipe.

## The heavy resource (LN-WF3)

`waitFor: { resource: heavy }` is granted when a slot of the box's heavy-job lock (`IHeavySlots`, the shared directory lock, its cap and its memory guard) was taken for the instance, holder `sindri <instance address>`. The grant is the `wait.satisfied` event with the resource source (`resource:heavy:<n>`). The wait's `timeout` bounds the take: a busy lock is "still waiting", never a failure, and the timer parks the wait like any other (`then: park`).

- **Take and release run in activities** (`TakeHeavy`, `ReleaseHeavy`), never in workflow code; the take waits in 5 s slices, heartbeating, and a timer or a command cancels it. The lease lives in the worker's memory (`HeavyLeases`, keyed by instance address); a worker that stops loses it.
- **Held** while the instance runs Steps off the desk (block or agent) and while it waits for its children. A child instance holds its own slot; the parent's is not released while a child runs.
- **Released** when the instance ends, is cancelled, parks or is taken over, and at a desk Step (any Step with no runtime in the build). Before the next Step off the desk, the instance takes a slot again (bounded by that Step's timeout). This one rule also covers a worker restart: the instance is still in its heavy phase, the registry has no lease, so the next Step takes one first. A Step already running when the worker died may finish without a slot; it runs again on resume anyway.
- **No second take inside a held Step:** a block Step's `StepContext.HeavySlotHeld` is true while the instance holds a slot; a Step that runs a build or tests takes none of its own. The agent launch (`AW_SANDBOX_SLOT_HELD=1`, sandbox spec) and BS-3d's check commands read the same fact once they land; `AW_HEAVY_LOCK_HELD` is never put in a session environment.
- **Ledger:** one `workflow.heavy` row per grant (`heavy.held`) and per release (`heavy.released`), keyed like the other mirrored rows, so a replay writes it once.
- A history recorded before the `ln-wf3-heavy-slot` marker never takes or grants a slot; an instance waiting on `heavy` from before stays waiting until its timeout parks it, as it did.
