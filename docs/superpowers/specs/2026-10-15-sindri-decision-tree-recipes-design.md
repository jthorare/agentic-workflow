# Sindri decision-tree recipes: `step.judge.choose`, evidence blocks, and the two harness ports

**Status:** design, revision 4 (after two /autoplan passes and a narrow re-check, §12) · **Date:** 2026-10-15 · **Owner decisions:**
2026-10-15 (§1) · **Builds on:** the workflow engine spec `2026-10-10-sindri-workflow-engine-design.md` (WF), rev 8.2,
schema 1; `docs/csharp/sindri-workflow.md` (REF, the recipe reference); `docs/csharp/judge.md` (Judge);
`docs/csharp/evidence.md` (Evidence); `docs/csharp/bugfix.md` (BugFix); the UI screens spec
`2026-10-13-sindri-ui-screens-design.md` (UI) · **Lane:** the engine lane owns the build (owner, 2026-10-15); phases in
§10 · **Next:** /specToProvenPR takes the phases to PRs once the owner approves this doc.

This doc turns two skills, `/specToProvenPR` and `/bugFixOrchestrator`, into Sindri recipes. They aren't ported as two
big recipes. Each piece they share becomes a **block recipe**, and the two harnesses become thin **meta recipes** that
chain the blocks as child workflows. A better review loop or evidence block then improves both harnesses, and every
later one, at once.

The new mechanism is the **decision tree**: a recipe declares several fixed branches, and a judge Step picks one at run
time from what the work actually is. A branch can diverge for one step and reconverge, or diverge into a separate path.
The first use is choosing which evidence proves a change.

Recipes stay schema 1, with one additive form: a `produces` entry may rename a child's output (§8.4). There is no new
top-level field and no new fact type, and no shipped recipe re-hashes. The engine, Judge and Evidence need the changes
listed in §8, each with its phase.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| DT1 | **Blocks first, harnesses second** (owner, 2026-10-15). Each piece of the two skills is a reusable block recipe (§4). `workflow.spec-to-proven-pr` and `workflow.bugfix` only chain them (§6) | One fix to a block reaches every harness that uses it, and the meta recipes stay short enough to read |
| DT2 | **A decision tree is fixed branches plus a judge Step** (owner, 2026-10-15). The recipe declares every branch. `step.judge.choose` picks one, and its choice is the Step's outcome. Ordinary `when: { outcome }` transitions do the branching (§3) | The recipe stays closed (WF W3), hashed and approved (W8). The validator checks every path (W2), and the evals cover them (WF §9.2). Nothing runs that a human didn't approve |
| DT3 | **No fan-out** (owner, 2026-10-15). "All three kinds of evidence" is its own branch, `all`, a child recipe that runs the three in sequence (§5.3) | Schema 1 has one current state per instance, and fan-out is schema v2 (WF §3.9). A combined branch needs neither |
| DT4 | **Jev is the first provider when the profile allows it** (owner, 2026-10-15). Its `choice` is the outcome. When no provider is confident enough, the Step reports `uncertain`, and the recipe routes that to a desk choice that shows what the providers leaned to (§3.3) | Jev answers exactly a choice between criteria (Judge "Jev"). The human sees only the decisions no model is sure of |
| DT5 | **`other` is always a branch** of an evidence route, and goes to the desk (owner, 2026-10-15). Recurring `other` reasons are candidates for new evidence blocks (§5.4) | The tree can't know every kind of evidence up front. The desk branch is where a new kind first shows up |
| DT6 | **New data evidence:** `evidence-api` (HTTP and CLI contracts) and `evidence-db` (persisted state), on the C# Evidence package next to `ui-evidence` (owner, 2026-10-15; §5.2) | Most changes the harnesses make aren't web UI. Today the only automated proof is a browser run or a unit test |
| DT7 | **/enhancePrompt is a porting tool, never a run-time step** (owner, 2026-10-15). It runs once per ported skill section, to rewrite that section into its Step's seed prompt (§7). No recipe picks skills at run time | The prompts get better as they move, and run-time behaviour stays what the recipe says |
| DT8 | **Text leaves the box only where the profile opts in.** Every block that sends repo text to the Judge takes its provider list from the profile. With no profile key, Jev is left out (§3.4). *Provisional owner default, 2026-10-15 (Q1)* | A diff, an issue body, API responses and DB rows are work data. The Judge redacts secrets, not business data, so egress fails closed |
| DT9 | **The skills keep working unchanged** until the owner switches them. The recipes run beside them; a skill becomes a launcher for its recipe only in a later change, against the adoption bar in Q5 | The installed toolkit stays TypeScript until the C# cutover (CLAUDE.md). Nothing here breaks the current way of working |
| DT10 | **The recipes keep every gate their skill has.** specToProvenPR's three DONE gates (PROOF, REVIEW=0, GREEN) and bugFixOrchestrator's fail-before and pass-after rule are states on every path to `resolved`. A recipe test walks each meta recipe's graph and fails if any path to a `resolved` terminal skips a gate state (`GateDominanceTests`, DT-M); one eval per gate shows the gate's refusal path (§6) | A port that drops a gate is a regression, however tidy it looks |

## 2. Terms

- **Block recipe:** a recipe that does one job and is used as a child (`workflow.review-loop`).
- **Meta recipe:** a recipe made mostly of `waitFor: { child }` states over block recipes (`workflow.bugfix`).
- **Route state:** an `active` state whose Step is `step.judge.choose`. Its transitions are the branches.
- **Reconverge:** several branches lead into one state. A transition's `from` can list several states (WF §3.5), so the
  shared exits are written once.

## 3. The decision-tree mechanism

### 3.1 `step.judge.choose`

A Step block that asks the Judge one choice question and reports the answer as its outcome.

| Field | Value |
|---|---|
| Id | `step.judge.choose` |
| Kind | Step, on the **block runtime** (§8.2). It is `deskCapable`: until the block runtime ships, it runs at the desk and a human picks (WF W5, W20). Whenever it runs on the desk it is human-only, like its twin (§8.5) |
| Parameters | `prompt` (required, literal string): the question. `choices` (required, literal list): each entry `"<outcome>: <criterion>"` (§3.2). `facts` (required, literal list of fact keys): what the question reads. `threshold` (optional, capability field, literal or `{ profile }`): 0 < t ≤ 1, default 0.6. `providers` (capability field, `{ profile: workflow.judge.providers }` by default) |
| Inputs | The keys in `facts`, declared through `InputsFrom: facts` (§8.1), so `RCP-WIRE-001` checks each is produced on every path into the state |
| Outcomes | The names in `choices` plus `uncertain`, declared through `OutcomesFrom: choices` (§8.1) |
| Outputs | `fact:decision` on **every** outcome (§8.1): `{ choice, reason, confidence, confidenceKind, probabilities, provider, decisionId }`, where `confidenceKind` is `probability` (Jev) or `self-reported` (an agent CLI). It is **engine-filled**: the block runtime writes it from the Judge envelope, and on the desk the engine writes `{ choice, provider: "desk", actor }` from `--outcome`, so a human never types it (§8.1). Fact type `payload` (WF §3.3): no new fact type and no clash with UI's `decision` viewer for intent records |
| Capabilities | `Network` |

The block builds the Judge input from the named facts, redacted and capped as the Judge does, and runs the new
`choose` question through the `judge` CLI (§8.3). It maps the envelope:

| Judge envelope | Step result |
|---|---|
| Decided, `decision` is one of the choices | Outcome = that choice |
| Escalated: `below-threshold`, `no-provider-decided` or `rule-timeout` | Outcome `uncertain`, with `fact:decision.reason` set to the reason code and any probabilities the providers sent |
| Escalated: `invalid-input` or `question-disabled`, the Judge errored, or the run timed out | `step.failed` with that reason. A route state always routes `step.failed` to its desk twin (§3.3), so a human can still pick a branch |

Every call writes one decision row in the Judge's store, escalations included, and the envelope carries its `id`
(§8.3). `sindri workflow why <address>` explains any route from that row, and `sindri workflow events` shows the id on
the transition (§8.6).

### 3.2 Choices grammar and rules

An entry is `"<outcome>: <criterion>"`. The outcome is the text before the **first** `": "` and matches
`[a-z][A-Za-z0-9]{0,31}` (camelCase); the criterion is the rest, 1 to 400 characters, and may contain colons. Entries must be quoted
in YAML, since an unquoted `- ui: …` is a mapping. The validator adds (§8.1):

| Rule | Check | Message names |
|---|---|---|
| `RCP-CHOICE-001` | `choices` has 2 to 32 entries | the count found |
| `RCP-CHOICE-002` | Each entry is a string in the grammar. A one-key mapping gets its own message: "entry 2 is a YAML mapping; quote it: `"ui: …"`" | the entry's index and the part that failed: name, separator, criterion length |
| `RCP-CHOICE-003` | Outcome names are distinct, and none is `uncertain` (the block adds it) | the duplicate |
| `RCP-CHOICE-004` | `facts` lists at least one fact key | the state |
| `RCP-CHOICE-005` | Every `step.judge.choose` state routes `uncertain` and `step.failed` to a `step.desk.choose` state whose `from` names it, so every route has a desk twin | the state and what's missing |

The existing rules cover the rest. `RCP-OUTCOME-001` demands a transition for every outcome a Step reports, so a recipe
that forgets a branch or `uncertain` fails validation. `RCP-WHEN-001` refuses a `when: { outcome }` that isn't one of
them, and its message is extended to list the outcomes available there ("outcomes here: ui, api, db, all, other,
uncertain").

The choices live in the recipe, so they are part of its hash. Changing a criterion is a new hash and needs re-approval
(WF W8), like changing a branch.

### 3.3 The uncertain path and the desk choice

`step.desk.choose` is the desk twin. It takes one parameter, `from: <route state>`, and shows that state's `prompt`
and `choices`. There's nothing to copy and nothing to drift. Its outcomes are that state's choices, without
`uncertain`. It is desk-only and **human-only** (§8.5): a session's `complete` on it is refused with `SND-AUTH-002`.
Its task shows the prompt, each choice with its criterion and what the providers leaned to (from `fact:decision` on the
`uncertain` path), or the failed run's reason (from the instance view, on the `step.failed` path), and why the judge
didn't decide (§9). Its own `fact:decision` is engine-filled from `--outcome`.

Every route state has a desk twin (`RCP-CHOICE-005`): the evidence route here, and the routes in `workflow.plan`,
`workflow.reproduce` and `workflow.bugfix`'s `route-mode`. A route state lists its branches first and `uncertain` and `step.failed` last. The UI takes the first transition from
each state as the main path (UI §5.1), and `RCP-CHOICE-006` (§8.1) checks this order. The branch transitions list both
states in `from`, so the tree is written once:

```yaml
states:
  route-evidence:
    kind: active
    step:
      use: step.judge.choose
      with:
        prompt: "Which evidence proves this change works?"
        facts: [fact:phasePacket, fact:built]
        choices:
          - "ui: what a user sees or does in a web app changed"
          - "api: an HTTP or CLI contract changed: a route, a verb, an exit code or a response shape"
          - "db: what is persisted changed: a schema, a migration, or the rows a command writes"
          - "all: all three of ui, api and db changed"
          - "none: the always-run checks already prove it (the verify pack, or the frozen bug check)"
          - "other: none of these would prove it"
    timeout: { after: 2d, then: park }
    next: "Pick the evidence for {item}: run `sindri workflow status {instance}` for the choices."
  route-evidence-desk:
    kind: active
    step: { use: step.desk.choose, with: { from: route-evidence } }
    timeout: { after: 2d, then: park }
    next: "Pick the evidence that proves {item}. Each choice shows the model's lean, if it had one."
transitions:
  - { from: [route-evidence, route-evidence-desk], to: evidence-ui,    on: step.completed, when: { outcome: ui },    produces: [fact:decision] }
  - { from: [route-evidence, route-evidence-desk], to: evidence-api,   on: step.completed, when: { outcome: api },   produces: [fact:decision] }
  - { from: [route-evidence, route-evidence-desk], to: evidence-db,    on: step.completed, when: { outcome: db },    produces: [fact:decision] }
  - { from: [route-evidence, route-evidence-desk], to: evidence-all,   on: step.completed, when: { outcome: all },   produces: [fact:decision] }
  - { from: [route-evidence, route-evidence-desk], to: evidence-none,  on: step.completed, when: { outcome: none },  produces: [fact:decision] }
  - { from: [route-evidence, route-evidence-desk], to: evidence-other, on: step.completed, when: { outcome: other }, produces: [fact:decision] }
  - { from: route-evidence, to: route-evidence-desk, on: step.completed, when: { outcome: uncertain }, produces: [fact:decision] }
  - { from: route-evidence, to: route-evidence-desk, on: step.failed }
```

- Every branch transition `produces: [fact:decision]`, so a decided route records its decision as well as an
  uncertain one (§8.6).
- `none` goes to `evidence-none`, a block-runtime Step (`step.evidence.none`, pure) that writes a
  `fact:evidenceSummary` saying the always-run checks are the proof, then joins the other branches. No desk task is
  spent on it. A summary leaves the route on `resolved`, and from a failed leaf child. Every other end has none
  (`RCP-WIRE-003`):
  - **`failed`**: a single leaf ran and did not hold. For `other`, that means no evidence was found.
  - **`all-failed`**: the `all` branch ended unresolved. `evidence-all` stops at its first check that fails, so
    usually one of ui, api and db failed, not all three. A check cancelled *inside* `evidence-all` also ends here,
    because `evidence-all` reports it as its own unresolved `dropped` and a terminal can't declare `cancelled`.
  - **`dropped`**: the child run this instance waited on (a leaf, or `evidence-all` itself) was cancelled.

  All three declare the outcome `unresolved` (owner, 2026-10-17: `all-failed` is kept apart from `dropped`), so a
  parent waiting on `workflow.evidence` can't route on which one it was. The `workflow.evidence` child instance's
  state name shows it, in `workflow status`, the ledger and the app.
- The route state's `next` is written as an instruction, because before DT-J it runs at the desk and a human acts in
  it (REF "Voice"). Its timeout is the desk's 2 days for that reason. On the block runtime the call is bounded by
  `MaxDuration` (§8.2); owner q93, 2026-10-23: the judge Steps set none now, so the state's timer is the only bound (it retries once, then parks).
- The route's timeout parks (`then: park`). It never retries, so a hung provider can't loop. The park is the ordinary
  `parked (timeout)` (WF §3.8), with its standard text, answered with `answer --resume`. The desk twin parks the same
  way.
- Picking `other` at the desk is only the pick. The reason is asked once, in `workflow.evidence-other` (§5.4).

A sample eval pair for the uncertain path (WF §9.2):

```json
{"trigger":{"kind":"step.completed","outcome":"uncertain","outputs":{"fact:decision":{"reason":"below-threshold","confidenceKind":"probability","probabilities":{"api":0.55,"db":0.4}}}},"expect":{"state":"route-evidence-desk"}}
{"trigger":{"kind":"step.completed","outcome":"api"},"expect":{"state":"evidence-api"}}
```

`recipe eval` fills in a placeholder `fact:decision` when a line leaves it out, as the engine does at run time (§8.1).
Only a case about the decision's content needs to spell one out.

### 3.4 Providers and third-party text

The question runs the Judge's chain, filtered by the block's `providers`: Jev, then the agent CLIs in
`providers.agentClis` order, then rules (Judge "Providers"). The list is passed as the call's `--providers` allowlist.

- `workflow.judge.providers` is a new profile key, a list drawn from `jev`, `claude-cli`, `codex-cli`, `cursor-cli` and
  `rules`. It is resolved **per repo**: a `repos[]` entry may set its own list, which wins over the box-wide one for
  instances of that repo. **When neither is set, Sindri uses the chain without `jev`.** Jev is opt-in per repo (DT8).
- The effective list is the intersection of this key, the recipe's literal list if any, and the Judge's own
  `providers` allowlist in its `config.json`.
- Every block that sends repo text to the Judge takes the same `providers` capability field, defaulting to the
  profile key: `step.judge.choose` and the plan check in `workflow.plan`. Judge calls made inside child CLIs
  (`bugfix-state judge`, Evidence's Judge adapter for `ui-element-repair` and `visual-critique`) get it too: Sindri
  exports the effective list as `AW_JUDGE_PROVIDERS` into every CLI it runs, so no call falls back to the Judge's
  default chain.
- A literal `providers` list in a recipe is intersected with the profile's at run time. A recipe can narrow the list
  but never add `jev` to a profile that leaves it out. A generated recipe can't widen its base's list
  (`RCP-CAP-002`), and the value is a literal or `{ profile }` only (`RCP-CAP-007`).
- **The cost of leaving Jev out:** the agent CLIs answer with no confidence today, and the Judge reads that as 1.0
  (Judge "Providers"). So without Jev, `below-threshold` would never fire. The `choose` question therefore gives the CLI
  answer schema a `confidence` field and asks for it (§8.3), and a CLI answer below the threshold is `uncertain` like
  Jev's. A CLI's self-reported confidence is weaker than Jev's probabilities. Q1 records that trade-off.
- `rules` decides nothing for `choose` (no pre-rules). With only `rules`, every route is `uncertain` and goes to the
  desk. That's the offline mode.

### 3.5 Shapes the tree can take

All are schema 1, and none needs fan-out.

| Shape | How |
|---|---|
| Diverge for one step, reconverge | Each branch is one state, and their exits share a target: `{ from: [evidence-ui, evidence-api, evidence-db], to: review, on: wait.satisfied, when: { outcome: resolved } }` |
| Diverge into separate paths | A branch leads to its own states and its own terminal |
| A combined branch | One branch is a child that runs several blocks in sequence (`evidence-all`, §5.3) |
| Nested decisions | A branch's child recipe has its own route state. Children nest at most 5 deep (`RCP-CHILD-003`); §6's deepest chain is 3 below the root (meta → evidence → evidence-all → evidence-ui) |
| Loop back with what was learned | A branch's exit goes back under a declared loop: `{ from: evidence, to: implement, on: wait.satisfied, when: { all: [ { outcome: unresolved }, { loop: stage, below: cap } ] } }` |

Every child wait also reports `cancelled` (REF, `RCP-OUTCOME-001`). Each child state in §4 and §6 routes it with a
transition of its own, carrying no `produces`, to the parent's terminal for it (`outcome: unresolved`), as
`workflow.item` does. It is never merged into the child's `unresolved` transition, which may carry the child's outputs
(§8.4).

## 4. The block recipes

All recipes live in **`recipes/sindri/`**, beside `workflow.step` and `workflow.item`. `recipe validate` and `recipe
eval` resolve a child as `<id>.yaml` beside its parent (REF), so one directory keeps the whole tree checkable offline
with no engine change. Block recipes are told apart by their table here and by their `description`, not by their
location. Each declares its `inputs` (the first is its subject), its `outputs` (which flow to its parent, §8.4) and
its terminal outcomes.

| Recipe | Does | Steps (block ids; all new ones `deskCapable`) | Outputs | Terminal outcomes | From |
|---|---|---|---|---|---|
| `workflow.plan` | A stage plan, a verification plan written before code, and a judge check that it states an observable signal | `step.plan.write`, `step.plan.verification`, `step.judge.choose` (`ready`, `underspecified`) | `fact:plan`, `fact:verificationPlan` | `resolved`, `unresolved` | specToProvenPR 1–2 |
| `workflow.investigate` | Root cause to `diagnosed`, with a confirmed hypothesis | `step.rootcause.investigate` (`diagnosed`, `unfixed`) | `fact:rootCause` | `resolved`, `unresolved` | bugFix 2 |
| `workflow.reproduce` | A route between a UI check and a test check, then a check that fails on the base for the reported reason, frozen as the baseline | `step.judge.choose` (`uiCheck`, `testCheck`), `step.reproduce.ui-check` / `step.reproduce.test-check` (`reproduced`, `notReproduced`), each through `bugfix-state` | `fact:check` (kind, file, command), `fact:baseline` | `resolved`, `unresolved` (not reproduced) | bugFix 3 |
| `workflow.implement` | Test-first build in a per-run worktree, looping on the checks | `step.produce`, `step.checks` (the existing Steps) under `loop: revisions` | `fact:output` (the diff; no `branch` until a Step writes it) | `resolved`, `unresolved` | specToProvenPR 3; bugFix 4 (mode A) |
| `workflow.implement-split` | Mode C: one implementer per area, in sequence on one worktree, each given the previous diff. **Deferred to DT-B** (it needs an areas signal; DT-R2 doesn't ship it) | `step.produce` per area, under `loop: areas` | `fact:output` | `resolved`, `unresolved` | bugFix 4 (mode C) |
| `workflow.hand-test` | Use the change by hand in the running app; record what was done, expected and seen | `step.handtest.run` (`works`, `broken`) | `fact:manualTest` | `resolved`, `unresolved` | specToProvenPR 4 |
| `workflow.verify` | **Always runs.** The `verify-app` journey with its lenses and evidence pack, and `/design-verify` when screen baselines match | `step.verify.journey` (`pass`, `fail`), `step.verify.design` (`pass`, `fail`, `skipped`) | `fact:evidencePack` | `resolved`, `unresolved` | specToProvenPR 5 (the PROOF gate) |
| `workflow.evidence-ui` | A `ui-evidence` run: plan, run, lint, critique | `step.evidence.ui-plan`, `step.evidence.ui-run` (`passed`, `failed`, `broken`) | `fact:evidenceSummary` | `resolved`, `unresolved` | specToProvenPR 6 |
| `workflow.evidence-api` | An API or CLI contract run (§5.2) | `step.evidence.api-plan`, `step.evidence.api-run` (`passed`, `failed`) | `fact:evidenceSummary` | same | new |
| `workflow.evidence-db` | A persisted-state run (§5.2) | `step.evidence.db-plan`, `step.evidence.db-run` (`passed`, `failed`) | `fact:evidenceSummary` | same | new |
| `workflow.evidence-all` | The three above in sequence, as children, then one combine Step (§5.3) | `step.evidence.combine` (`done`; block runtime, pure) | `fact:evidenceSummary` (the three combined) | same | new |
| `workflow.evidence-other` | The evidence the human used, and why no block fitted | `step.evidence.desk` (`recorded`, `noneFound`; human-only) | `fact:evidenceSummary`, `fact:evidenceReason` | `resolved` (recorded), `unresolved` (none found) | DT5 |
| `workflow.evidence` | The route of §3.3 over the evidence recipes and `none`, reconverged | `step.judge.choose`, `step.desk.choose`, `step.evidence.none` | `fact:evidenceSummary`, `fact:decision` | `resolved`, `unresolved` | specToProvenPR 6; bugFix 5 |
| `workflow.review-loop` | `/review` to 0 blocking and 0 issue findings, cap 5, then one narrow re-check (§4.1) | `step.review.run`, `step.review.fix`, `step.review.recheck` | `fact:review` | `resolved`, `unresolved`, `failed` (behaviour wrong) | specToProvenPR 8 |
| `workflow.ready` | The GREEN gate (the full suite and CI on the PR head), the proof re-check if review changed non-test source, the PR body, `ready #N` to the merge orchestrator | `step.ready.green` (`green`, `red`), `step.ready.recheck` (`pass`, `fail`, `skipped`), `step.ready.body` (`done`); effect `effect.notify.desk` on the last transition | `fact:ready` (head sha, gate scopes, proof and review state; input `fact:pr` isn't re-declared, `RCP-OUT-003`) | `resolved`, `unresolved` | specToProvenPR 9 |
| `workflow.report` | The bugfix report: `resolution.md` and the ask-first offer to post it, for a fixed or an unfixed bug alike. No PR | `step.report.write` (`done`) | `fact:resolution` | `resolved` (the report is written; the parent's own terminal says whether the bug was fixed, §6.1) | bugFix 6 |

DT-R applied DTC2 to DTC9 to this table: the subject kinds `packet` and `payload`, the long-form child outputs, and the
inputs the shipped recipes declare (`phasePacket` and `built` for the evidence recipes, `change` and `phasePacket` for
`workflow.review-loop`). Where a row differs from the shipped recipe in `recipes/sindri/`, the shipped recipe is
current (`recipes/sindri/README.md` lists them).

Steps that the meta recipes use directly: `step.intake.ticket` (desk, `done`; writes `fact:issue`, `fact:expected`,
`fact:actual`), `step.pr.open-draft` (desk, `done`; writes `fact:pr`), and three bugfix Steps over `bugfix-state`:
`step.bugfix.start-attempt` (block, `single`, `split`, `capReached`: it passes the mode on, so the implement
branch follows it), `step.bugfix.record-candidate` (block, `recorded`), `step.bugfix.recheck` (block-then-agent, `passed`,
`failed`) and `step.bugfix.judge` (block, `resolved`, `partial`, `unresolved`, `needsHuman`). Every Step descriptor above lands in DT-R (§10),
descriptor-only and `deskCapable`, so every recipe validates, evals and runs at the desk before P8 gives its Steps an
agent.

### 4.1 `workflow.review-loop`

```yaml
schema: 1
id: workflow.review-loop
version: 1
description: "Block recipe: /review to zero blocking and issue findings, at most 5 runs, then one narrow re-check."
inputs:
  - { key: fact:change, label: "Change", type: payload, kind: payload }
  - { key: fact:phasePacket, label: "Phase packet", type: packet, kind: packet }
outputs:
  - { key: fact:review, label: "Last review", type: payload, from: review }
initial: review
states:
  review:  { kind: active, step: step.review.run, loop: rounds, timeout: { after: 2h, then: park },
             next: "Run review {loop.rounds}/{loop.rounds.cap} on {item}'s change." }
  fix:     { kind: active, step: step.review.fix, timeout: { after: 4h, then: park },
             next: "Fix the blocking and issue findings of run {loop.rounds}; answer the rest in writing." }
  recheck: { kind: active, step: step.review.recheck, timeout: { after: 1h, then: park },
             next: "Run cap reached: have one reviewer re-check only the fixes." }
  clean:   { kind: terminal, outcome: resolved }
  stuck:   { kind: terminal, outcome: unresolved }
  wrong:   { kind: terminal, outcome: failed }
transitions:
  - { from: [review, recheck], to: clean, on: step.completed, when: { outcome: clean },           produces: [fact:review] }
  - { from: review,  to: fix,     on: step.completed, when: { outcome: findings },        produces: [fact:review] }
  - { from: [review, recheck], to: wrong, on: step.completed, when: { outcome: behaviourWrong }, produces: [fact:review] }
  - { from: fix,     to: review,  on: step.completed, when: { all: [ { outcome: fixed }, { loop: rounds, below: cap } ] }, produces: [fact:change] }
  - { from: fix,     to: recheck, on: step.completed, when: { all: [ { outcome: fixed }, { not: { loop: rounds, below: cap } } ] }, produces: [fact:change] }
  - { from: recheck, to: stuck,   on: step.completed, when: { outcome: findings },        produces: [fact:review] }
loops: { rounds: { cap: 5 } }
evals:
  - evals/workflow.review-loop/happy.jsonl
  - evals/workflow.review-loop/cap-then-recheck.jsonl
  - evals/workflow.review-loop/behaviour-wrong.jsonl
  - evals/workflow.review-loop/recheck-clean.jsonl
  - evals/workflow.review-loop/recheck-wrong.jsonl
```

- `step.review.run` reports `clean` (0 blocking and 0 issue findings, classified by substance as specToProvenPR's
  "Blocking or not" says), `findings`, or `behaviourWrong` (a finding that shows the behaviour itself is wrong,
  specToProvenPR step 8). Suggestions and nits are answered in writing inside the Step and never count.
- The cap and the narrow re-check make the #184 precedent explicit. `stuck` is `unresolved` and `wrong` is `failed`,
  so the parent branches on the outcome: `failed` goes back to implement, and `unresolved` parks for the owner (§6.2).

## 5. Evidence

### 5.1 `workflow.evidence-ui`

Wraps the C# Evidence package's commands (Evidence): `doctor`, a plan from the diff, `run <script.json>`, the lint, and
the `visual-critique` question on the screenshots. `ui-run` reports `passed`, `failed` or `broken`. `broken` is a
selector problem: one repair through `ui-element-repair`, then the desk. Two of these depend on Evidence's Judge
adapter, which is `UnavailableJudge` in the C# build today (Evidence "Judge"); the adapter lands with DT-B. Publishing
stays ask-first and outside the recipe.

### 5.2 `workflow.evidence-api` and `workflow.evidence-db` (new)

Both are new commands in `Aw.Evidence`, built test-first under its host guard and locks. Each writes a `summary.json`
in the UI run's shape, so one reader and one judge input serve all three. Phase DT-E starts with a contract section in
`docs/csharp/evidence.md`, reviewed before code. The rules below are its floor:

| | `evidence-api` | `evidence-db` |
|---|---|---|
| Script | `evidence.api.run/1`: ordered requests (method, path, headers, body), or CLI calls (argv), each with expectations: status or exit code, JSON-pointer assertions on the body or stdout, a golden file | `evidence.db.run/1`: a named connection, then ordered queries, each with expected rows, a row count or a JSON-pointer assertion over the result |
| Where it may run | The host the script's run names (`--host`, required when the script has requests, no default) under the existing host guard; any method, writes included (owner, 2026-10-15). No redirect is followed off the base origin | SQLite files under the repo or the run directory. Postgres only through an entry in the target repo's `.agentic-workflow/evidence.json` (owner, 2026-10-15; `--config` names another file explicitly), which names the database and marks it `disposable: true` (a fixture or container database) |
| Read-only | — | **At the connection**, not by parsing statements: SQLite opened `Mode=ReadOnly` (or a temp copy), Postgres with `default_transaction_read_only=on`, `BEGIN READ ONLY` and a required non-superuser read-only role (a read-only transaction alone doesn't stop `COPY … TO`). A pre-run refusal of write statements stays, for a clear early error. Contract tests cover a writing CTE, a side-effecting function, `nextval`, `COPY … TO` and a SQLite `PRAGMA` |
| Identity | — | Before any query, the server's identity (`inet_server_addr()`, `current_database()`) must match the profile entry, or the run refuses. A loopback port can be a tunnel to a shared database |
| CLI calls | Only the binary under test: named in the script and resolved against the repo's build output or the allowlist in the repo's `.agentic-workflow/evidence.json`. Run in a temp directory with a scrubbed environment (no `GH_TOKEN`, cloud credentials, `DATABASE_URL`, `TYPESAFE_API_KEY`; the Judge's `SecretNames` style, with a declared pass-through list). Write-like verbs (`delete`, `merge`, `push`, `drop` …) refused unless the script declares them, and a declaration is shown in the report | — |
| Secrets | Header values come from named environment variables, never literals; only their names are recorded. Requests and responses are redacted through `Aw.Redaction` and capped | Rows are capped and redacted |
| Seeds | — | Seeding writes only into a throwaway database or temp copy the run creates, never through the evidence connection. The seed is recorded as provenance (the `ui-evidence` "seeded" rule) |
| Before and after | Run at `base` and at `head` (§5.5) | Same |
| Judge | `resolution-check` with `checkKind: api-evidence` or `db-evidence` (§8.3) | |

### 5.3 `workflow.evidence-all`

Three child waits in sequence: ui, then api, then db. Each child's summary arrives under its own key
(`fact:evidenceUi`, `fact:evidenceApi`, `fact:evidenceDb`, mapped from the child's `fact:evidenceSummary` by the parent's `produces`, §8.4), so
none overwrites another. The first that ends `unresolved` ends the recipe the same way, and the rest don't run and
show as skipped (§9). After all three, `step.evidence.combine` (block runtime, deterministic) writes the combined
`fact:evidenceSummary`.
There's no parallel split (DT3). When schema v2 adds fan-out, this recipe can become one without its parents changing.

### 5.4 The `other` branch

`workflow.evidence-other` is one human-only desk Step, where the reason is asked once. It records the evidence used (`fact:evidenceSummary`) and the reason no
block fit (`fact:evidenceReason`). DT-P adds the route's outcome counts, `other` and `uncertain` among them, to the recipe stats (API P12, `RecipeStats`), with the `other` reasons listed. A reason that keeps
appearing becomes a proposal for a new evidence block and a new choice: a reviewed recipe change, never automatic.

### 5.5 Before and after

`/bugFixOrchestrator` requires the same check to fail before the fix and pass after it. `bugfix-state` already owns
that rule: it freezes the check and records each run (BugFix). The recipes call it rather than re-implement it:

- `step.reproduce.*-check` runs the check through `bugfix-state` at the base and reports `reproduced` (it failed for
  the reported reason) or `notReproduced` (it passed). Those are Step outcomes, not a transition's evidence: a failing
  evidence clause is a refusal, and a refusal parks `needs-answer` instead of ending `unresolved` (WF §5.8).
- After the fix, `step.bugfix.recheck` runs the frozen check on the candidate through `bugfix-state` and reports
  `passed` or `failed`.
- Transition `evidence: [{ kind: check, at: head, expect: pass }]` on `workflow.bugfix`'s exit to `judge` is
  a second guard over the same frozen check. The `evidence.check` provider lands as a descriptor in DT-R and runs in
  DT-B, together with the interpreter's evidence-check activity (§8.7).

## 6. The meta recipes

Each meta recipe has one eval per gate it inherits (DT10), showing that a path skipping that gate doesn't exist.

### 6.1 `workflow.bugfix`

```
intake (desk) ─► investigate ─► reproduce ─► route-mode ─► attempt ─┬─ single ─► implement ──────┐
                                  │ unresolved                      └─ split ──► implement-split ─┤
                                  ▼                                                              ▼
                               report                                                recheck (frozen check)
                                                                                      │ passed       │ failed
                                                                                      ▼              ▼
              ┌──── partial / unresolved, attempts < cap ◄── judge ◄── evidence             back to route-mode
              ▼                                       │ resolved   │ needsHuman          (attempts < cap)
          route-mode ─► attempt                       ▼            ▼
                                             report ─► fixed   owner (desk)
          attempts at cap, or not reproduced ─► report ─► not-fixed
```

- `route-mode` is `step.judge.choose` over `single` (mode A) and `split` (mode C), with the confirmed root cause as
  input. Mode B, two competing candidates, needs fan-out and stays in `bugfix-state` until schema v2 (WF §13, "Bugfix
  mode B").
- `evidence` is the child `workflow.evidence`, so the evidence tree is shared with the other harness. The frozen check
  is always run; the route adds evidence the check alone doesn't give (an api diff behind a UI bug, say).
- `judge` is `step.bugfix.judge`, which runs `bugfix-state judge`: the helper builds the `resolution-check` input from
  its own state and records the verdict once per candidate, its guard against re-rolled verdicts (BugFix). Outcomes:
  `resolved`, `partial`, `unresolved`, and `needsHuman` (the Judge escalated), which goes to the human-only
  `step.owner.decide`. The evidence summary reaches it through the children's outputs (§8.4).
- **One attempt counter.** `bugfix-state`'s attempt count (at most 3) is authoritative. `attempt`
  (`step.bugfix.start-attempt`) runs after `route-mode`, reads the mode from its `fact:decision`, and calls
  `start-attempt --mode` before each round; a failed recheck or a `partial` verdict goes back to `route-mode` and so
  opens a new attempt, so the shared `workflow.implement` stays
  bugfix-agnostic. The recipe's `loop: attempts`, cap 3, mirrors it, and `capReached` goes to the unfixed report.
- It ends in `workflow.report`, never in `workflow.ready`: the skill doesn't open a PR, and neither does the recipe.
  `step.bugfix.record-candidate` runs after each implement, and `step.report.write` calls `bugfix-state advance
  report` (with `--unresolved` on the unfixed path), so `bugfix-state`'s own state matches the instance's. Two
  terminals tell the outcomes apart: `fixed` (`resolved`) after a `resolved` verdict, and `not-fixed`
  (`unresolved`) after the attempt cap or a bug that didn't reproduce. The recipe stats and Q5's count read them.

### 6.2 `workflow.spec-to-proven-pr`

One instance per stage. The epic layer (the stage map, the autonomy and git contracts, starting the next stage after a
merge) stays in the skill for now (Q8).

```
plan ─► implement ─► hand-test ─► verify ─► evidence ─► open-draft (desk) ─► review-loop ─► ready ─► done
 │          ▲            │ unres.   │ unres.  │ unres.                          │ failed      │ unres.
 │          └────────────┴──────────┴─────────┴─────────────── (loop: stage) ◄─┘             │
 │ unresolved (underspecified)                                    │ unresolved               │
 └──────► parked for the owner ◄──────────────────────────────────┴──────────────────────────┘
```

- The skill's "function first, review last" order is the state order, and its gates are states: PROOF is
  `hand-test`, `verify` and `evidence`; REVIEW=0 is `review-loop`; GREEN is `step.ready.green` in `ready` (DT10).
- A failed hand test, verify run or evidence run, and a review loop that ends `failed`, go back to `implement` under
  `loop: stage`, cap 3. The cap parks `rounds` for the owner (WF §3.8).
- "Parked for the owner" is a desk Step `step.owner.decide` (`retry`, `stop`), human-only, so the owner sees why.
- iOS stages: `workflow.verify`'s journey runs `verify-ios` when the repo is iOS, as the skill's step 5 does. CLI and
  API stages have no special case: the evidence route's `api` branch covers them.

## 7. Porting the skill prompts with /enhancePrompt

Each new Step block has a **seed prompt**: the text an agent session starts with under P8 (WF §6.5), and the
instructions a desk task shows before P8. Seeds come from the two skills' sections, ported once per block and
reviewed:

1. Take the skill section the Step replaces (specToProvenPR step 4 for `step.handtest.run`, and so on).
2. Run /enhancePrompt on it with the seed template below as the target shape, plus the Step's descriptor and this
   doc. /enhancePrompt's own Goal / Constraints / Done-when layout maps onto the template's sections.
3. Review it by hand and commit it as `src/Sindri/Aw.Sindri.Features/Blocks/Step/Seeds/<block id>.md`, an embedded
   resource of `Aw.Sindri.Features`.

Seed template: **Goal** (one sentence) · **Reads** (the fact keys, by name; a seed refers to run-time data only by fact
key, never by template variable) · **Do** (the steps) · **Report** (each outcome, and the facts it must write) ·
**Never** (the forbidden actions: merging, pushing the base branch, editing a frozen check, and so on).

**Pinning.** Once a block has a runtime that reads its seed (P8 for agent Steps), the descriptor carries the seed's
hash (§8.1), so a seed edit is a block `Version` bump and re-pins approvals (WF W11). Before that, a seed is only
desk-task text: an edit needs a review, not a re-approval.

/enhancePrompt never runs inside a recipe, and no Step picks a skill at run time (DT7).

## 8. Engine, Judge and Evidence changes

These are the engine, Judge and Evidence changes, each with its phase (§10). The only recipe-format change is §8.4's
`produces` long form.

### 8.1 Parameter-declared outcomes and inputs (engine; DT-V1, with the views in DT-V2)

Today a Step's outcomes and inputs are fixed by its descriptor (`BlockDescriptor.Outcomes`, `Inputs`; WF §2).

- `BlockDescriptor` gains `OutcomesFrom` and `InputsFrom`: null, or the name of a list parameter. The effective
  outcomes are the entries' names (before the first `": "`) plus the descriptor's fixed `Outcomes` (`uncertain` for
  the judge Step). The effective inputs are the listed fact keys plus the fixed `Inputs`. `step.desk.choose`'s `from`
  is resolved the same way, through the named state's parameters.
- **One resolver**, `StepOutcomes.Of(descriptor, stepUse, recipe)` and `StepInputs.Of(…)`, replaces every direct read
  of `descriptor.Outcomes` and `Inputs`: the validator's `StepSource` and `BlockNeeds` (`RCP-WIRE-001`), the run-time
  and eval report check (`TransitionDecision.Validation`), `recipe eval --scaffold` (which walks each declared outcome,
  `uncertain` included), and the desk task views (`InstanceViews`, so `status`, `allowedCommands` and the app list
  the choices).
- `OutputSpec.requiredOn` gains an "every outcome" form, for `fact:decision`, and `OutputSpec` gains `EngineFilled`:
  when a report omits such an output, the engine writes it from the report (for the choice Steps:
  `{ choice: <outcome>, provider: "desk", actor }`), so report validation never rejects a desk completion for it.
  `recipe eval` fills it the same way (`provider: "eval"`).
- `choices` and `facts` must be literal lists (a `{ fact }` or `{ profile }` value is refused, `RCP-CHOICE-002` and
  `-004`), so the outcome set is fixed by the approved recipe.
- `recipe eval --scaffold` writes one case per path from `initial` to a terminal, taking each route's choices in
  order; the approve preview still marks them as scaffolds.
- New rules `RCP-CHOICE-001` to `-005` (§3.2), and `RCP-CHOICE-006`: a route state's `uncertain` and `step.failed`
  transitions come after its branch transitions. `RCP-WHEN-001`'s message lists the outcomes available.
- **Descriptor hashes don't move.** The new fields, and the runtime and seed fields of §8.2 and §7, are nullable and
  written only when set (`JsonIgnore(WhenWritingNull)`; `HumanOnly` is a `bool?`, absent when not true), so every shipped descriptor hashes as before and no approved recipe
  drift-parks. A test over `ShippedBlocksTests` pins each shipped descriptor's hash.
- The app's catalog and goldens change (the desk task's choices, §9), so DT-V2's gate runs `Aw.Sindri.Ui.Tests`.

### 8.2 The block runtime (engine; DT-J)

A judge call is neither a desk task (P4) nor an agent session (P8). The engine runs it itself, in seconds.

- `BlockDescriptor` gains `Runtime: desk | block | agent` (absent means today's rule: desk if `deskCapable`, else
  `SND-WF-010`) and, for `block`, `MaxDuration`.
- New interface `IBlockStep : IStep` with `Run(StepContext, CancellationToken) → StepResult`. A block-runtime Step runs
  inside its `RunStep` activity with start-to-close timeout = min(the state's `timeout.after`, `MaxDuration`). The
  result goes through the same report validation as a desk report (WF §5.3), and the event records `via: block`.
- **An activity timeout is `step.failed(timeout)`**, not a timer park, so the route's `step.failed` transition sends it
  to the desk. `then: retry` doesn't apply on the block runtime; the validator refuses it on a block-runtime state
  (`RCP-TIME-003`, with its invalid fixture in DT-J). The activity's retry policy is `MaximumAttempts = 1` (today's
  `RunStep` uses Temporal's unlimited default), so a failed Judge call isn't silently repeated.
- `MaxDuration` was 2 minutes for the judge Steps (owner q93, 2026-10-23: removed; a Step with none has no fixed limit, the state timer bounds it). The evidence run Steps (§5) are long, take machine locks and can
  outlast 10 minutes, so they run on the agent runtime's activity shape (heartbeating, P8). Until then they run at
  the desk. The block runtime is for short calls only.
- A block-runtime Step that is also `deskCapable` falls back to the desk when its runtime isn't registered (W5).
- `via: block` is a new value in the event and the API's instance views; API and UI goldens change, and DT-J's gate
  runs `Aw.Sindri.Ui.Tests`.

### 8.3 Judge: the `choose` question and two check kinds (Judge; DT-J)

- **`choose`**, a ninth question, and the first whose outputs come from its input. Judge's `QuestionDefinition`
  holds outputs and criteria fixed today, so it gains a per-call form: outputs, criteria and threshold taken from the
  input, validated (2 to 32 names, Jev's limit is 255). Input `{ prompt, state: { <fact>: <value> }, choices:
  [{ name, criterion }], threshold? }`. Class `brief`. Threshold: a `choose` entry in the Judge's `config.json` wins
  (the owner's tuning), else the call's, else 0.6. Budget 15 s, no pre-rules, no fallback (it escalates). Jev gets `{ type: "choice", criteria }`. The
  agent CLIs get the choices with their criteria inside the prompt's untrusted-text tags, and a `choose` answer schema
  with a required `confidence` (0–1), so their answers can fall below the threshold too (§3.4).
- **The envelope**, for `choose`: decided adds `probabilities` when the provider sent them. Escalated is
  `{ escalated, reasonCode, id, probabilities? }`. The `id` and any probabilities are added only for `choose`, so the
  hook contract's three question envelopes (`wake-gate`, `brief-scope`, `ask-check`) don't change (CLAUDE.md "hook
  contract").
- **`resolution-check`**: `checkKind` gains `api-evidence` and `db-evidence`, besides `ui-evidence` and `test`.
- **How Sindri reaches it:** a `cli` edge, `Sindri → Judge kind: cli`, running `choose < input.json --providers …`,
  the pattern BugFix and Evidence use. The Judge keeps its own store and ids, as the current compose edge's note
  requires (Judge's stores never join Sindri's container). `deps.yaml` gets the edge (`status: planned` until DT-J
  merges), and README X42's row (P8 owns the Sindri → Judge edge) is updated to say DT-J adds it.
- **Which judge, and whose state.** Until the cutover, `judge` on PATH is the TypeScript build the hooks use: it has
  no `choose`, and its `~/.agentic-workflow/judge/decisions.sqlite` is the live hook store. Sindri therefore never runs
  `judge` from PATH. It runs the C# binary it ships with (`AW_SINDRI_JUDGE`, default the `judge` published next to
  `sindri`), with `AW_JUDGE_STATE_DIR=$AW_SINDRI_HOME/judge/`, a new judge-only variable (DT-J) that moves only the Judge's
  own store, so the C# Judge's migrations and decision rows never touch the TS store. `AW_STATE_DIR` is left alone:
  Evidence and BugFix keep the box-wide `heavy-job.lock` and `qa-stack.lock` under it, and moving it would break the
  one-build-slot rule. Every CLI Sindri runs that calls the Judge (`bugfix-state`, Evidence) gets the same binary
  (`AW_JUDGE_BIN`), `AW_JUDGE_STATE_DIR` and `AW_JUDGE_PROVIDERS` (§3.4). `sindri workflow why <address>` reads a route's decision
  through that binary and root, in place of a bare `judge why`. It is an eleventh `workflow` verb: DT-J adds its API
  route (`GET /sindri/workflow/instances/{id}/decisions`), its catalog row and its UI catalog entry, so DT-J's gate
  runs `Aw.Sindri.Ui.Tests`. At the cutover the two stores become one, a step for
  the switch plan.

### 8.4 Child outputs flow to the parent (engine; DT-C, with its interpreter half in P7)

Today a child wait carries only the child's terminal outcome (WF §5.8). The validator gives it no facts
(`WiringRules`), and a recipe's `outputs` are "declarations only" (WF §3.2). The meta recipes need the children's
results: the diff, the root cause, the check, the evidence summary.

- A child's declared `outputs` become facts of the parent's `wait.satisfied` from that child. The parent may
  `produces` them, and `RCP-WIRE-002` accepts them. Only declared outputs flow; a child's other facts stay its own.
- The interpreter carries the child's output facts in the child's result. `recipe eval` child lines may give them in
  `outputs`, like any trigger.
- **When an output is present.** The validator checks, inside the child, which outputs are produced on every path to
  each terminal outcome. A parent may `produces` a child output only on a transition whose `when: { outcome }` names
  terminal outcomes on all of whose paths the child produces it (`RCP-WIRE-003`), and never on `cancelled`. An
  output's `from` stays the UI's placement hint; the flow follows the wiring, so an output produced in several states
  is fine.
- A parent may rename what it receives (`produces: [{ fact: fact:evidenceUi, from: fact:evidenceSummary }]`), so two
  children's outputs with one key don't overwrite each other (§5.3). That is the long form of a `produces` entry; the
  short form (a bare key) keeps its meaning.
- This amends WF §3.2 ("declarations only" becomes "declarations, and what a parent receives") and §5.8. It changes no
  existing recipe's hash: `outputs` already exists and is hashed, and a bare `produces` key stays a plain string in the
  normalized form; only a renamed entry is an object. *Flag:* the `produces` long form is the one schema addition in
  this doc, and a staleness test pins the shipped recipes' hashes.
- DT-C lands the validator and eval half. The interpreter half is one more unit of engine P7, which builds children.

### 8.5 Human-only desk Steps (engine; DT-R)

> Owner q93, 2026-10-23, reworked by q100, 2026-10-24: `HumanOnly` is no longer an authorization rule. A plain agent session (one the owner started, with no launch secret) may report every desk Step, including `step.interview.answer`, `step.scope.review` and the merge decision; a session Sindri launched is refused on a desk run by `AgentReportAuthority` (SND-AUTH-002, a launch secret on a run no agent runs). The flag still drives the "only you" label, `RCP-HUMAN-001` and the recommendation filter.

`BlockDescriptor` gains `HumanOnly` (written only when true, §8.1). It marks a Step the owner decides: the app labels it "only you", `RCP-HUMAN-001` counts it as a human act, and a recommendation on it is dropped when it isn't one of the outcomes. It does not gate who may report the Step (owner q100, above). `complete` on a desk run is open to a human and to a plain agent session; a session Sindri launched reports with its launch secret, and `AgentReportAuthority` refuses that secret on a desk run with `SND-AUTH-002`, before the nonce claim, so nothing is consumed. A session report on an instance that can't be read is refused with that read error. Every desk completion event records its actor, which `workflow events` shows. This covers `CompleteStep` only: a Sindri child answering through another command (a park answer, a decision answer) carries no launch secret and still passes until the `X-Sindri-Launch` / `CallerContext.Launch` change extends the same refusal.

### 8.6 Tracing a route (engine; DT-J)

The transition out of a block-runtime Step records `fact:decision`, and `workflow events` and `status` show it as
`via block · decision <id> (sindri workflow why <address>)`. REF gets a "Debugging a route" paragraph: `workflow
events`, then `workflow why`.

### 8.7 The evidence-check activity and providers (engine; DT-B)

The interpreter passes `GateAnswers.None` for transition evidence today, so a transition with `evidence` always
refuses. DT-B adds the evidence-check activity, which runs the transition's evidence providers before the commit as
gates do, and registers `evidence.check` over `bugfix-state`. Until then, recipes that name `evidence.check` validate
(it ships as a descriptor in DT-R) but can't take that transition on a live serve.

### 8.8 Evidence (Evidence; DT-E and DT-B)

- DT-E: the two new commands and their contract (§5.2).
- DT-B: Evidence's Judge adapter (a `judge` CLI adapter in place of `UnavailableJudge`), so `ui-element-repair` and
  `visual-critique` run.
- DT-B: BugFix's two blocked adapters, `run-ui` over Evidence and `judge` over the C# Judge (AW-BUGFIX-016/017 today,
  BugFix), so `bugfix-state` can run a UI check and record a verdict.
- Edges: `Sindri → Evidence kind: cli` and `Sindri → BugFix kind: cli`, declared in `deps.yaml` (`status: planned`)
  in DT-B.

## 9. Status and UI

The new human task and the branching states need these from the API and UI lanes. Each is a change request against the
UI spec. The API fields land with the engine phase named; the app's rendering is the UI lane's, filed as rows
`DT-UI-1`…`5` in the UI spec's change list (its §6 table) by DT-V, and built after DT-J.

- **DT-UI-1, the desk choice (API fields in DT-V2; `reason` values in DT-J).** The desk task carries `choices: [{ name,
  criterion, probability?, confidenceKind?, lean? }]` and `reason` (`below-threshold`, `no-provider-decided`, `rule-timeout`, `failed`, or none
  before DT-J). The CLI `status` lists every choice with its criterion and probability, and one `complete` line per
  choice. Nothing is pre-filled: today `status` pre-fills the first declared outcome (REF), and for a choice that
  would nudge the human toward the first one.
- **DT-UI-2, empty states.** No probabilities (offline `rules`, `no-provider-decided`, before DT-J): the task says "No model
  answered; pick from the criteria." A `failed` reason says what failed. A CLI's
  self-reported confidence is labelled "self-reported", never shown as a probability.
- **DT-UI-3, the app's choice control.** A list with no initial selection, focus on the prompt first (UI R8-13's
  read-first rule), arrow keys and number keys, Enter to confirm. A probability is shown as text, never by colour or
  bar alone. Screen-reader order: name, probability, "model's lean" when it is the top one, criterion.
- **DT-UI-4, the states band and the Workflows list (API: `states[].routeOf` and `skipped` in DT-V2's views).** A route
  state and its branches render as one **route column**:
  before the choice it reads "choosing"; after it, the branch taken, with the chooser (judge or you) and the
  confidence. Branches not taken fold away, and children that never ran (`evidence-all`'s skipped ones) render as
  skipped, not as ○ "not started". The desk twin folds under the route column (it's after the branches, §3.3).
- **A running block Step** shows the existing running cell; a `via: block` result shows the settled route cell above.
- **DT-UI-5, deep children (API: the instance view's `deepest` descendant, in DT-C's interpreter unit).** The
  Workflows list's NOW cell shows the `next` of the deepest active descendant, prefixed with its
  path ("review-loop › fix: …"), so a stuck grandchild is visible from the root.

## 10. Phases

Each phase is one PR, driven by /specToProvenPR in the engine lane unless it says otherwise. **(owner)** steps run
through `scripts/sindri-prove.sh human`. Sizes: S is a few files, M one subsystem slice.

| Phase | Goal | Needs | Size | Signal | Docs |
|---|---|---|---|---|---|
| **DT-V1** | §8.1 (resolver, `OutcomesFrom`, `InputsFrom`, every-outcome and engine-filled outputs, `RCP-CHOICE-001`–`006`, null-ignored descriptor fields); the `step.judge.choose` and `step.desk.choose` descriptors (descriptor-only, `deskCapable`); `recipes/dev/workflow.dev-evidence-route.yaml` with evals; `recipe eval --scaffold` through a route (`--out <dir>`) | Engine P1, P3 (merged) | M | `recipe validate` on the dev route exits 0; one invalid fixture per `RCP-CHOICE` rule exits 1 with its message, including the unquoted-mapping one; a missing `uncertain` transition exits 1 with `RCP-OUTCOME-001`; `recipe eval` passes a case per outcome, `uncertain` and `step.failed` among them; `--scaffold` walks every choice; `ShippedBlocksTests` shows every shipped descriptor hash unchanged | REF (rules table, "How to add a block", a "Writing a decision tree" section); WF §2 note |
| **DT-V2** | The desk task's choices in the views (`InstanceViews`, `status`, `allowedCommands`, the app list; §9), `DT-UI-1` and `DT-UI-4`'s API fields (`choices`, `states[].routeOf`, `skipped`) | DT-V1 | S | `status` on a `step.judge.choose` or `step.desk.choose` state lists each choice with its criterion and pre-fills none; the app's catalog and goldens move with it, so the gate runs `Aw.Sindri.Ui.Tests` | REF; API spec fields |
| **DT-E** | `evidence-api` and `evidence-db` in `Aw.Evidence` (§5.2), contract section first | None (the Evidence package) | M | Against the fixture app: an api script fails at `base` and passes at `head` for a changed route; a db script asserts the rows a command wrote; each read-only bypass in §5.2 is refused (`COPY … TO` by the required non-superuser read-only role, the rest at the connection); a server-identity mismatch refuses; a CLI call outside the allowlist refuses; both `summary.json` files validate against their schemas | `docs/csharp/evidence.md` |
| **DT-R** | The block recipes of §4 with evals, all in `recipes/sindri/`; every new Step descriptor, `evidence.check` (descriptor-only) and `HumanOnly` (§8.5); seed prompts ported with /enhancePrompt (§7) | DT-V | M | `recipe validate` exits 0 on every block recipe; `recipe eval` passes each one's happy case and its loop or cap case; a session's `complete` on a `step.desk.choose` run, and on a `step.judge.choose` run while it is on the desk, exits 2 with `SND-AUTH-002` | REF (block catalog); `recipes/sindri/README` |
| **DT-C** | §8.4's validator and eval half: child outputs as facts of `wait.satisfied` | DT-V | S | A parent that `produces` a child's declared output validates; one that `produces` an undeclared child fact fails `RCP-WIRE-002`; `recipe eval` carries a child line's `outputs` into the parent | WF §3.2, §5.8 amendments; REF |
| **DT-J** | §8.2 block runtime, §8.3 Judge `choose` and check kinds, the `cli` edge, §8.6 tracing, the `workflow.judge.providers` profile key (Jev opt-in) | DT-V; engine P6 (#232) merged, to avoid `RecipeWorkflow` conflicts | M | **(owner)** on a real `sindri serve`: the dev route with a UI-only diff fact ends in `evidence-ui`, the event says `via: block` and a decision id that `sindri workflow why` explains; with the profile at `[rules]` the same start goes to `route-evidence-desk`, `status` lists the five choices with none pre-filled, and `complete --outcome api` moves it to `evidence-api`; with the key absent the decision row names a CLI provider and nothing is sent to Typesafe; a judge call killed past `MaxDuration` lands at the desk with reason `failed` | `docs/csharp/judge.md`; REF; `docs/sindri/profile.md`; README X42 |
| **DT-M** | The meta recipes `workflow.bugfix` and `workflow.spec-to-proven-pr`, with evals: child lines, one per inherited gate (DT10) | DT-R, DT-C | S | `recipe validate` exits 0 over the whole tree (`RCP-CHILD-001`…`003`); `recipe eval` walks a bugfix through `split`, an `uncertain` route, a failed recheck, a `partial` resolution and a resolve; each gate eval shows its gate can't be skipped | REF |
| **DT-B** | The evidence Step runtimes, the evidence-check activity and `evidence.check` (§8.7), the bugfix Steps' runtimes, Evidence's Judge adapter, BugFix's `run-ui` and `judge` adapters, `AW_JUDGE_PROVIDERS` into child CLIs, and the edges (§8.8) | DT-J, DT-E, engine P7 (children on a live serve) | M | **(owner)** `workflow.evidence` on a fixture item with an api diff runs `evidence-api` and ends `resolved`; the same with a broken route ends `unresolved` with the failing assertion in the parent's `fact:evidenceSummary`; `bugfix-state judge` and an Evidence `ui-element-repair` call under a repo without `jev` both write decision rows naming a CLI provider, in `$AW_SINDRI_HOME/judge/`, and the TS hook store is untouched | `docs/csharp/evidence.md`; REF; `deps.yaml` |
| **DT-P** | End to end: both meta recipes on a real serve, desk Steps first and agent Steps once P8 is in; the route's outcome counts in the recipe stats (API P12) | DT-M, DT-B, engine P7 (with §8.4's interpreter unit); API P12 for the stats; P8 for the agent half | S | **(owner)** a `workflow.bugfix` instance on a fixture bug reaches `fixed`, and one on a bug that doesn't reproduce reaches `not-fixed`, with its children's history in `sindri workflow events`; one `workflow.spec-to-proven-pr` stage reaches `ready`; with P8, the implement Step launches an agent session (`via: agent`); `GET /workflow/recipes/workflow.evidence/stats` shows the route's `uncertain` and `other` counts | REF; `docs/sindri/README.md` |

**Approving the tree.** A meta recipe runs only when every recipe below it is approved, leaf first (`SND-RECIPE-003`).
DT-P's owner step approves the ~20 recipes in that order. REF's runbook lists the order, and a recipe test in DT-M
walks the tree and fails when the list is out of date. A batch approval for a tree is Q6.

**The cost of a block fix.** Parents pin their children's hashes (WF W11), so a fix to a block recipe makes every
recipe above it stale until each is re-approved, leaf first. "A fix reaches every harness" (DT1) is true after those
re-approvals, not before. That makes Q6 the first follow-up once DT-P runs.

### 10.1 Parallelization plan

The engine lane keeps one builder at a time (the box rule). The phases are cut so something is always ready to build,
and most of the work doesn't wait for P7 or P8.

```
now ──► DT-V ──┬──► DT-R ──┬──► DT-M ───────────────────────────────┐
               └──► DT-C ──┘                                        ├──► DT-P (engine P7; P8 for agents)
  └──► DT-E (Evidence; any lane with a free builder) ──┐            │
engine P6 merged + DT-V ──► DT-J ──────────────────────┴─► DT-B ────┘ (DT-B also needs engine P7)
```

`DT-V` in this diagram and table is DT-V1 and DT-V2 together. DT-R, DT-C and DT-J need the resolver and the descriptors, so they start once DT-V1 merges; DT-V2 (the views) runs beside them.

| Wave | Runs | Starts when |
|---|---|---|
| 0 | DT-V ∥ DT-E | Now. DT-V touches the validator, descriptors and views; DT-E only `Aw.Evidence` |
| 1 | DT-R ∥ DT-C ∥ DT-J | DT-V merged; DT-J also needs engine P6 (#232) |
| 2 | DT-M ∥ DT-B | DT-M: DT-R and DT-C. DT-B: DT-J, DT-E and engine P7 |
| 3 | DT-P | DT-M, DT-B, and engine P7 with §8.4's interpreter unit. Its agent half follows P8 |

- **With the engine's own phases.** The lane's order stays #227 (P4b), then #232 (P6). DT-V is the lane's next unit
  while those two are in review. After P6, the lane alternates: P7 (with §8.4's unit) and DT-J, then P8 and DT-B.
  P7 and P8 stay on the critical path for DT-P, and nothing here delays them.
- **Outside the engine lane.** DT-E is independent of Sindri, and DT-R and DT-M are mostly recipe and seed text. The
  merge orchestrator may give any of them to a lane with a free builder slot (Q2).
- **Units per phase.** DT-R fans out one unit per block recipe after a contract unit (the shared Step descriptors and
  the seed layout, committed red). DT-B fans out one unit per evidence kind. Units append to the hotspots (WF
  "Hotspots").

## 11. Open questions

Each has a recommended default; all are reversible unless marked.

| # | Question | Recommended default |
|---|---|---|
| Q1 | Jev opt-in: which profiles turn it on? | *Provisional owner default (2026-10-15):* with no `workflow.judge.providers` key, Jev is off, and the owner's personal profile sets `[jev, claude-cli, codex-cli, cursor-cli, rules]`. The trade-off: without Jev, uncertainty rests on the CLIs' self-reported confidence (§3.4). *Security-relevant: the owner should confirm* |
| Q2 | Who builds DT-E, DT-R and DT-M? | Whichever lane has a free builder first, assigned by the merge orchestrator; otherwise the engine lane |
| Q3 | Eval lines that say an evidence check fails? | Not now. Evidence refusals are covered by DT-B's unit and integration tests. The before-and-after *outcomes* are Step outcomes (§5.5), and evals already cover them |
| Q4 | `next` variables for facts (`{fact:pr}`)? | Not now. Use `{item}`, `{instance}` and `{child}`; revisit if three recipes need it |
| Q5 | The adoption bar for switching a skill to its recipe? | Measured after P8, when Steps run as agents: three real bug fixes ending `fixed` through `workflow.bugfix` and two real stages through `workflow.spec-to-proven-pr`, each needing no more human desk completions (counted from `workflow events`' actors) than the route desk tasks, the owner decisions and the skill's own ask-the-user points, and every gate eval green. The owner decides the switch (DT9) |
| Q6 | A batch approval for a recipe tree? | Not in these phases, but first after them: every block fix re-approves the tree above it (§10). Batch approval is a WF §4 change for the owner to decide |
| Q7 | The review loop's cap: the recipe's or the profile's? | The recipe's. A cap is a reviewed property of the recipe, and `RCP-CAP-*` already treats loops that way |
| Q8 | The epic layer of specToProvenPR (stage map, autonomy contract, next stage after merge)? | Stays in the skill until DT-P is proven; then a `workflow.spec-epic` recipe over `workflow.spec-to-proven-pr` children is designed separately |

## 12. Review notes (/autoplan pass 1, 2026-10-15)

Five lenses ran on revision 1 (product, architecture, design, devex, security). Every lens was NEEDS_WORK, and none
found anything CRITICAL. Revision 2 fixes every HIGH and MEDIUM finding as follows.

- **Product:** specToProvenPR's PROOF and GREEN gates are now `workflow.verify` and `step.ready.green` (DT10, §6.2).
  The bugfix ends in `workflow.report`, not a PR. The UI-or-test repro choice moved into `workflow.reproduce`. The
  adoption bar is Q5, the epic layer is Q8, and the `uncertain` and `other` rates are measured in DT-P.
- **Architecture:** child outputs flow to the parent (§8.4, DT-C). The resolver covers the report check, evals, the
  scaffold and the views (§8.1). Parameter inputs are wired (`InputsFrom`). The block runtime contract is spelled out
  (§8.2). Sindri reaches Judge through a `cli` edge (§8.3). Descriptor hashes stay put (§8.1). The phase graph is
  rebuilt: descriptors land in DT-V and DT-R, and DT-B needs P7 and the evidence activity. All recipes live in one
  directory (§4). `behaviourWrong` is in §4.1. Reproduce reports Step outcomes (§5.5), and there is one attempt
  counter (§6.1). Child `cancelled` is handled (§3.5). The edges and the Evidence Judge adapter are declared (§8.8).
  The wrong rule id is fixed (`RCP-CAP-002`), and the CLIs get a `confidence` field (§8.3).
- **Design:** §9 is new: the desk choice, empty states, the choice control, the route column, and the deep-child NOW
  cell. The route lists `uncertain` last (`RCP-CHOICE-006`). `fact:decision` is type `payload`. Judge failures go to
  the desk. The route parks instead of retrying. The `next` texts are instructions. `other` asks once.
- **Devex:** the desk twin copies nothing (`from:`). The choices grammar handles a colon in the criterion, and an
  unquoted entry gets its own message. There is a sample `uncertain` eval and a placeholder `fact:decision`. Each
  phase lists its docs, and the approval order is written down. The seed template and the pinning rule are in §7.
- **Security:** Jev is opt-in, and every Judge-calling block takes the profile's providers (DT8, §3.4). `evidence-db`
  is read-only at the connection, checks the server's identity, and seeds only into throwaway copies. The CLI target
  is allowlisted with a scrubbed environment. Request secrets are named, never literal. No off-origin redirect is
  followed. Desk choices are human-only (§8.5).
- **Answered in writing, not changed:** route steering by issue text (security LOW) is bounded by approved branches,
  and the bugfix `judge` stays on every path after the route. A threshold as a capability field is taken (§3.1). An
  eval evidence map is deferred (Q3).

### 12.1 Review notes (/autoplan pass 2, 2026-10-15)

Pass 2 on revision 2: product, design, devex and security PASS; architecture NEEDS_WORK on one new HIGH. Revision 3
fixes the HIGH and every MEDIUM:

- **Architecture:** Sindri runs its own C# `judge` binary with a Sindri-owned state root, never PATH's TS judge or its
  hook store (§8.3). Every branch records `fact:decision` (§3.3). Child-output presence is checked per terminal, and a
  parent may rename what it receives (§8.4; flagged as the one additive recipe form). `evidence-all` keys each child's
  summary and combines them (§5.3). BugFix's blocked adapters land in DT-B, and the verdict goes through
  `bugfix-state judge` (§6.1). The Judge's `choose` takes outputs, criteria and threshold per call, with the threshold
  precedence stated (§8.3). `start-attempt` is its own bugfix Step. `fixed` and `not-fixed` are separate terminals.
  The activity runs once (`MaximumAttempts = 1`), the edges are `planned`, `choices` and `facts` are literal-only,
  `HumanOnly` doesn't move hashes, the chain depth and `workflow.ready`'s output are fixed, and `evidence-other`'s
  `unresolved` is reachable.
- **Product:** a recipe test enforces DT10's gate dominance. The route has a `none` choice. The cost of a block fix
  is stated and Q6 is raised. Unfixed bugs end `not-fixed`. Q5 is measurable and dated after P8.
- **Design:** the route's timeout is the desk's 2 days. The UI requests are rows `DT-UI-1`…`5` with their API fields
  and phases (§9). The `step.failed` path shows the run's reason. The reason for `other` is asked once. Every route
  has a desk twin (`RCP-CHOICE-005`). The bugfix verdict's escalation goes to the owner. A CLI's confidence is labelled
  self-reported.
- **Devex:** `fact:decision` is engine-filled, so a desk completion needs only `--outcome`. The undeclared
  `--output` is gone. The approval order is pinned by a test, not `recipe list --stale`. `--scaffold`'s output is
  defined, and `RCP-TIME-003` gets a fixture.
- **Security:** a judge Step on the desk is human-only. Child CLIs get `AW_JUDGE_PROVIDERS`. The key resolves per repo.
  The effective list intersects the Judge's own allowlist.
- **Answered in writing, not changed (LOW):** `COPY … TO` and the Unix-socket identity check are left to DT-E's
  contract section, which must require a non-superuser read-only role for Postgres entries and identify a socket by
  its path and `current_database()`. A third user of the blocks isn't named yet; the scoping recipe (WF §12) is the
  likely one.

### 12.2 Narrow re-check (2026-10-15)

One architecture reviewer re-checked revision 3's edits only: PASS, every pass-2 finding resolved or partly resolved,
and three new MEDIUMs, fixed in revision 4. The Judge's store moves through a judge-only `AW_JUDGE_STATE_DIR`, so
Evidence's box-wide locks stay put (§8.3). A child's `cancelled` gets its own transition, apart from the `unresolved`
one that carries outputs (§3.5). `start-attempt` runs after `route-mode` with the mode, and `record-candidate` and
`advance report` are named Steps (§6.1). Two LOWs are fixed too: the `produces` hash wording, and `workflow why`'s
route and catalog rows.
