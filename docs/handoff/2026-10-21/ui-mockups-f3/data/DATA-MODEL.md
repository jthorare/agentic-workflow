# Sindri workflow data model (what the UI can really show)

Source: repo `/home/thor/repos/agentic-workflow`, **read from git branch `batch/2026-10-19a` (7626e709a)**, extracted to
`data/_src/` (read-only copy; paths below are repo-relative and exist in that copy). The detached HEAD of the working tree
(8597ecc92) is older: it has no `workflow.line`, `design`, `plan`, `phase`, `build`, `prove`, `ship` recipes. All of them
are shipped on the batch branches (`recipes/sindri/`).

Companion files: `sample.json` (dataset, built by `tools/build_sample.py`), `steps-catalog.json` (every Step descriptor),
`PLAIN-WORDS.md`.

## 0. The one thing to know first

Sindri has **no "step list" on an instance.** A recipe is a **state machine**: a recipe has *states*; an *active* state
runs exactly one **Step** (a block with a descriptor); a *wait* state waits for an event, an answer, a resource or a
**child instance**; a *terminal* state ends it. For the mockup, "a step in the graph" = **a state**, and "step kind" =
the **runtime of the Step that state runs** (or "waits for a child" / "waits for an event/answer"). `workflow.line` is
five states, each waiting on a child recipe (the stations).

The graph a user sees is therefore: nodes = states, edges = transitions labelled by the **outcome** the Step reported.
Edges can loop back (rounds), and every loop has a cap.

## 1. Recipe (the template; what the graph is drawn from)

`src/Sindri/Aw.Sindri.Core/Workflow/Recipes/Recipe.cs:28` (`record Recipe`), YAML in `recipes/sindri/workflow.<name>.yaml`,
reference `docs/csharp/sindri-workflow.md:266` (States), `:287` (Transitions).

| Field | Type | Meaning |
|---|---|---|
| `schema` | int | `1` |
| `id` | string | `workflow.<name>` (e.g. `workflow.line`) |
| `version` | int | author's version; same id+version with other content is refused (`RCP-VERSION-001`) |
| `description` | string? | what the recipe is for (the only recipe-level prose; used in sample as `description`) |
| `urgency` | `incident` \| `triage` \| `work` | list ordering only; the engine never reads it |
| `name` | string? | template for the instance name (`{fact:<input>}`, `{start:date}`); the name is the subject label |
| `inputs[]` | `{key, label?, type?, kind, generate?}` | the first input is the instance's **subject** |
| `outputs[]` | `{key, label?, type?, from}` | what it produces for a human; `from` = the state that produces it |
| `initial` | state id | an active or wait state |
| `states{}` | id -> state | see below, in file order |
| `transitions[]` | see below | first one whose `when` holds is taken |
| `loops{}` | name -> `{cap>=1, noProgress[]}` | `noProgress` defaults `[findings, emptyDiff, sameFailure]` |
| `budget` | `{tokens}` | recipe-wide token budget |
| `evals[]` | paths | eval case files (a recipe cannot be approved without them) |
| `hash` | `sha256:` | JCS hash of the normalized form; listings show 12 hex digits |

Recipe standing (from `recipe list`): `approved` \| `unapproved` \| `revoked`; stale marks `pin-drift`, `child-stale`,
`legacy-shape` (`docs/csharp/sindri-workflow.md:151` area). Only approved recipes can start instances.

**What the API actually serves of a recipe** (`Views/RecipeView.cs`, goldens `Golden/Api/workflow-recipe/*.json`): `{id, hash, version, name, inputs[], outputs[], urgency, orderedBy, states[{id, order, branchOf}], loops[{name, from, to, cap}]}`. **No transitions (edges), no Step per state, no state kind, no timeouts, no description.** The graph in `sample.json.recipes` is built from the YAML and its normalized long form; to draw edges in the real app, a new read (the full normalized recipe, `recipe validate --json` `normalized`) must be served.

### State (`Recipe/RecipeState.cs:22`, `StateKind.cs:7`)

| Field | Values / shape |
|---|---|
| `kind` | `active` \| `wait` \| `terminal` |
| `step` (active) | a block id `step.x.y`, or `{use, with?, inputs?}`; `inputs` are `input.context.lenses/code/history` blocks run before the Step |
| `waitFor` (wait) | exactly one of `{event: <kind>}`, `{answer:{id,question}}`, `{resource: heavy\|slot}`, `{child:{recipe, inputs, onParked: propagate\|ignore}}` |
| `timeout` | `{after: "<n>m\|h\|d", then: park\|escalate\|retry}` (required on active and wait) |
| `loop` | name of a declared loop (the state counts rounds) |
| `budget` | `{tokens}` per state |
| `next` | the sentence a human reads: variables `{item} {instance} {state} {user} {child} {loop.<n>} {loop.<n>.cap} {budget.tokens.left}`. Waits read "Waiting for ..."; human states read as instructions |
| `outcome` (terminal) | `resolved` \| `unresolved` \| `alreadySatisfied` \| `failed` |

**Engine-owned states** (added to every recipe, never declared; `docs/csharp/sindri-workflow.md:331`): `parked`,
`escalated`, `taken-over`, `cancelled`. They show as a **badge** on the current state, not as a node (section 4).

### Transition (`Recipe/RecipeTransition.cs:15`)

`{from: state|[states], to, on, when?, gates?[], evidence?, effects?[], produces?[]}`

- `on`: `step.completed` / `step.failed` (from an active state), `wait.satisfied` (from a wait state), `command.<verb>` (from any non-terminal state).
- `when` (the **route**): `{outcome: X}`, `{fact, is: present|absent}`, `{loop, below: cap}`, `{all:[..]}`, `{any:[..]}`, `{not:..}`. An outcome comes from the Step's declared outcomes, the event, or a child's terminal outcome (plus `cancelled`).
- `gates` (`gate.budget.available`, judge/model gates have a `threshold` in (0,1]); `effects` (`effect.notify.desk`, `effect.claim.ref`); `produces` = facts recorded on the transition.
- In `sample.json` each edge is `{from, to, on, outcomes[], condition, produces[], gates?, effects?, back}`; `back` is computed (target not later on the main path = a loop edge).

### Loops, budgets, timers

- Loop: `rounds {cap}`; exhausting it **parks** the instance (reason `rounds`). Shown as `round {n, max}` on a cell (`Views/Round.cs`).
- Budget: `{tokens}` per state and per recipe; exhausting parks (`budget`). Status shows `used / limit` for the state and the instance (`Views/StatusBudget.cs:11`).
- Timeout: `then: park` parks (`timeout`), `escalate` escalates, `retry` re-arms.

## 2. Step (the block a state runs)

`BlockDescriptor` (`Core/Workflow/Blocks/BlockDescriptor.cs:26`). The mockup-relevant fields (all 91 in `steps-catalog.json`):

| Field | Values |
|---|---|
| `id` | `step.<area>.<name>` (kebab-case, <= 64) |
| `runtime` | **`agent`** (an agent session launched for the run) \| **`block`** (the engine runs it: a Judge call or deterministic code) \| **`desk`** (a human reports it with `sindri workflow complete`). Enum `BlockRuntime`, `Blocks/BlockRuntime.cs:10` |
| `humanOnly` | true for `HumanDeskStep`: a session's `complete` is refused (`SND-AUTH-002`); only the owner can. These are the **owner stops** |
| `capabilities` | flags `None, Tools, Effects, AutonomyTier, WriteScope, Network, Budget` (`Blocks/Capability.cs:8`). **WriteScope** = may change files; **Network**; **Tools** = runs tools |
| `reads` / `writes` | fact keys in / out. A write is `requiredOn: [outcomes]` or `every` |
| `outcomes` | the closed list the Step may report; each must be routed by a transition (`RCP-OUTCOME-001`) |
| `maxDuration` | block Steps only (default 2 min) |

Builder factories: `AgentStep`, `BlockStep`, `DeskStep`, `HumanDeskStep` in `src/Sindri/Aw.Sindri.Features/Blocks/Step/StepSpec.cs:32/38/44/50`.
So the lead's three kinds are right, plus a plain `DeskStep` (1 of them: `step.checks`/`step.verify` style, rare).

Counts across the scraped catalog (91 Steps): 52 block, 27 agent, 11 human-only desk, 1 plain desk (+ the helper-built ones `step.produce` agent, `step.checks`/`step.verify` desk, `step.judge.choose` block, `step.desk.choose` human-only desk).

**Special Steps.** `step.judge.choose` is a *route*: a model chooses one of the recipe's `choices` (`"<outcome>: <criterion>"`) or says `uncertain`; its **desk twin** `step.desk.choose` lets a human pick, and a failed route goes there (`Features/Blocks/Step/ChoiceSteps.cs:14`; rules `RCP-CHOICE-001..006`).

**Facts.** The data that flows between states. Key `fact:<camelCase>` (or `evidence:<kind>@<base|head>`). Types (`Recipes/FactTypes.cs:4`): `text, file, item, link, scope-map, packet, evidence, decision, payload`. The type only picks a viewer.

**Inputs/outputs per Step:** `reads[]`, `writes[]` above. **Per instance** the facts are `FactRow`s (section 3).

## 3. Instance (a running recipe)

Address and ids: `address` = `<ITEM>/<recipe short>/<n>` for a root (golden `ITM-01JK3/project/1`), child = `<parent address>/<recipe short>.<n>` (golden `ITM-01JK3/project/1/step.5`); `id` = `WFI-<32 hex>`.
**Display name**: there is no separate name field; the title is the item's title from the `items` view, else the address (`docs/csharp/sindri.md:788`); the recipe's optional `name:` template renders once at start as the **subject label** (`SubjectRef.label`). The recipe has **no display name** (id only) and states have **no display name** (id + `next` sentence).

Two read shapes, both served under `/sindri/workflow/`:

**List row `InstanceSummary`** (`Core/Workflow/Views/InstanceSummary.cs:23`; golden `tests/Aw.Sindri.Tests/Golden/Api/workflow-instances/open.json`):
`address, id, itemId, recipe{id,hash,version,urgency}, state, chip{word,tone}, since, engineOwned{kind,reason}|null, subject{kind,key,label,href}, inputs[], states[StateCell], now (<=80 chars), answer|null, actions[]`.

**Detail `InstanceDetail`** (`Views/InstanceDetail.cs:27`; goldens `Golden/Api/workflow-instance/*.json`):
`address, id, parent{id,address,title,task,workstream}|null, itemId, title, recipe, state, since, engineOwned, subject, orderedBy ("path"|"transition-list"), states[StateCell], current, facts[FactRow], children[ChildGroup], counts{timeline,decisions,requests,context}, actions[], asOf`.

### StateCell = the per-step status the graph needs (`Views/StateCell.cs:19`)

`{id, order, branchOf, glyph, badge, round{n,max}|null, loops[{with,n,max}], desk, child, children{done,total}|null, outcome|null, terminal}`

- **`glyph`**: `done` \| `active` \| `pending` (`StateGlyph.cs:7`). **That is the complete per-step status set.** A terminal state is `done` once reached; its `outcome` then holds `resolved/unresolved/failed`.
- **`badge`** replaces the glyph on the interrupted state: `{kind: parked|escalated|taken-over, reason}` (`EngineOwnedKind.cs:7`).
- `desk: true` = the state's Step runs on the desk (a human reports it); `child: true` = waits on child instances; `round` = loop round of this state.
- **Not in the API: start/end timestamps per state, who ran it, tokens per state.** Only `InstanceDetail.since` (entered the *current* state). Per-state history exists only as ledger events (below). `sample.json` adds `x_stepRuns` derived from the events; treat it as a UI-side projection.

### Instance chip (`Views/StateChip.cs:9`) - the one word + colour per instance

`PARKED(attn)`, `ESCALATED(attn)`, `WITH YOU(neutral)`, `DECISION(attn)` = an open desk answer, `QUEUED(neutral)`, `DONE(neutral)`, `RUNNING(ok)`. Tones: `attn, fail, ok, neutral`. **`fail` is reserved and never produced**: a terminal `failed`/`unresolved` instance reads `DONE`.

### Current state block (`Views/CurrentState.cs`)

`{state, step{kind: active|wait, ref, declaration}|null, gates[{id,on,threshold,description}], desk{run,outputs[{key,type,required}],checks[],completeAction}|null, status}`

`status` (`Views/StatusFields.cs:19`): `header{address,recipe,hash12,version,state,detail,since,ageSeconds}`, `next` (the recipe's sentence, filled), `answer`, `child`, `drift`, `inputs[{key,name,hash}]`, `timeout{kind: parks|escalates, inSeconds, on}`, `budget{scope, used, limit, instance{used,limit}}`, `paused{timeoutLeftSeconds}`, `commands[{verb, actionId, class, cli, confirm}]`, `text` (the CLI text), `injection[]`, optional `run`.

**Caveat on `step.declaration`:** the goldens show prose ("produce the output for the item"), but the real code (`src/Sindri/Aw.Sindri.Core/Workflow/Engine/Runtime/CurrentStates.cs:13-28`) fills it with the Step id itself for an active state, the constant "one child per packet task" for a child wait and "waits for an answer" for the rest. A Step descriptor has **no description field** (`BlockDescriptor.cs:26`); the prose in `steps-catalog.json[].doc` comes from C# XML doc comments and is not served at run time. The only served prose per state is the recipe's `next` sentence, and per recipe the `description`.

`status.header.detail` is e.g. `desk task`, `needs-answer`, `taken over`, null.

### Facts (`Views/FactRow.cs:19`)
`{key, label, type, direction: input|output, from (producing state), status: produced|in-progress|pending, summary, at, by, ref{name,size,hash,path,itemId,url,host,title,factKey}, progress{done,total}, pending{required,checks,completeAction}}`.
Facts are the "inputs/outputs" a user sees of an instance (e.g. Scope packet, Design packet).

### Children (`Views/ChildGroup.cs:15`, `ChildRow.cs:22`)
`ChildGroup{state, recipe, done, total, workstreams[{id,label,rows}], rows[ChildRow], more}`; `ChildRow{id,address,itemId,title,glyph,state,chip,round,agent,ageSeconds,engineOwned,mirroredPark,actions[]}`. `mirroredPark` = a park mirrored from a child onto its parent row.

## 4. Engine-owned states: park, escalate, takeover, cancel

| Badge / state | When | Resume / who acts |
|---|---|---|
| `parked` + reason | `rounds` (loop cap), `no-progress`, `budget`, `needs-answer` (gate refusal or `step.failed`), `timeout`, `spec-conflict` (recipe revoked / block drifted / effect refused), `SND-WF-010` (no runtime for a block or resource in this build). Enum `Engine/ParkReason.cs:7`; kind `loop, refusal, step, wait, revoked, child-revoked` (`ParkKind.cs:7`) | answer: `--grant n` (rounds), `--grant-tokens n` (budget, protected: needs a terminal), `--text`, `--resume`, `--accept`; `StatusAnswer{kind: text\|resume\|grant\|grantTokens\|accept\|cancel-only, actionId, class: desk\|protected, cli, atTerminal, via, child}` |
| `escalated` | a top-rung gate below its threshold, `then: escalate` timeout, or an escalated child | a human decides |
| `taken-over` | a human ran `takeover` | `handback` |
| `cancelled` | `cancel` | ends it |

Commands (`StatusVerb`, `StatusCommand`): `answer, complete, handback, takeover, cancel, approve`; classes `desk`, `protected` (human at a terminal only), `pause` (takeover; a session may run it).

### Takeover and handback (`docs/csharp/sindri-workflow.md:1176`)

- `sindri workflow takeover <address>` / `handback <address>` (also `sindri handback <item-or-address>`). Takeover allowed on any open, not-yet-taken-over instance; handback only on one taken over. Else `SND-WF-003`.
- **Timers:** takeover cancels the state timer's scope and keeps what was left (`paused.timeoutLeftSeconds`, shown "timeout 40s left"); **no state timer fires while taken over**; handback re-arms the timer with the remainder. A park that arrives while taken over waits and takes effect on handback. Cancel stops every timer.
- **takeoverIdle:** a 2 h timer; when it fires while still taken over the engine records one `workflow.notified` event ("hand back {item}?") and never hands back by itself.
- The instance shows badge `taken-over`, chip `WITH YOU`, commands `handback, cancel`, `next`: "With you since 14:05. Run `sindri workflow handback <addr>` when you're done." (golden `tests/Aw.Sindri.Tests/GetInstance/Golden/taken-over.txt`).
- An instance can also *start* taken over (an "open in agent" launch is pending).

## 5. Approvals and pending decisions (four different things; the UI must not merge them)

1. **Owner stops = human-only desk Steps.** The pending item is an open **desk task**: `current.desk{run, outputs, checks, completeAction:"CompleteStep"}`; options are the Step's **outcomes**; the answer is `sindri workflow complete <addr> --run <token> --outcome <o> [--output fact:answers=<file>]`. There are three per line (spec `docs/superpowers/specs/2026-10-15-sindri-spec-to-proven-pr-line-design.md` section 3.4):
   - **Stop 1 after scoping**: `workflow.scoping` state `stop`, Step `step.scope.review`: `approve | revise | drop` (revise needs `fact:answers` notes). Shows the **stop view** (`ScopingView`, section 7).
   - **Stop 2 after design**: `workflow.plan` states `decide` (`step.interview.answer`: the decisions interview) then `stop` (`step.plan.review`): `approve | reviseDesign | revisePlan | drop`.
   - **Stop 3 ready-to-merge**, per phase: `workflow.pr-babysitter` (a stub in this snapshot: `step.babysit.watch` human-only: `merged | rework | stuck`).
   - Other human-only Steps: `step.interview.answer` (`answered | doneAsking`), `step.design.lens-desk` (accept / revise / drop), `step.build.scope-desk` (`allow | revert | stop`), `step.phase.decide` (`rebuild | stop`), `step.desk.choose` (pick a route choice), `step.ship.open`.
   - The **question + options** of the interview come from `fact:questions` (`schemas/scoping/questions.json`): per question `{id Q<n>, surfaces[], dimension, impact 1-3, blocks[AC-n], trust, question, options[2-4]{id Q<n>.<a>, answer, rationale, cites[], recommended?, yields?}, other:bool, skip:bool}`, at most 5 per round, at most one `recommended`. See `sample.json` `interviewQuestions` (wording mockup-authored; the chosen options Q1.a..Q4.a are the real ones from the Link expiry golden).
2. **Park answers** (section 4): a question the *engine* asks ("rounds used up: grant more?").
3. **Decisions (`DEC-...`)** for requests that agent sessions file (`rescope, add-task, note, handback` request kinds; decision kind `accept-request`): `{id, kind, class, label, item, question, options[{id,label,rejectLike,cli}], recommended, state, filedBy, filedAt, decidedBy, choice, note, recordedOnly, actions}`; answered with `sindri decide <id> <option>`. Golden `Golden/Api/decisions/pending.json`. "recordedOnly" decisions are recorded but not yet acted on by the engine.
4. **Recipe / profile approval** (`recipe approve`, `recipe revoke`, `profile approve`): protected; the human types the action text (`approve workflow.step 1`) after a preview (diff, capabilities, effects, pinned blocks). Not an instance state.

**Needs-you rows** (`Golden/Api/needs-you/*.json`) merge all of these for Home: `{id, kind: decision|failure|profile|park|..., label, subject{kind,id}, item, title, reason, repo, urgency: pulse|colour|calm, since, recordedOnly, notice, answerKind, primaryAction, actions[], cli}`.

`sample.json` `approvals[]` is a UI-side projection of (1): `{instance, state, step, humanOnly, run, question, shows, options[{id,outcome,rejectLike,requires,goesTo,cli}]}`. `recommended` on a stop option is **not in the real data**; it is only a mockup hint.

## 6. Events stream

- Per instance: `GET /sindri/workflow/instances/{id}/events?after=N` -> `{events:[{sequence, type, occurredAt, actor, transition{from,to,trigger,outcome,run,via?,decision?,tokens?,reason?}}], cursor}` (`Features/GetInstanceEvents/InstanceEventView.cs`, `InstanceEventFields.cs:16`; golden `Golden/Events/block-and-desk.json`).
- Types (`Engine/Events/WorkflowEventType.cs:7`): `workflow.started`, `workflow.transition` (any state change incl. engine-owned), `workflow.refused` (gate results, then re-run/park/escalate), `workflow.shifted` (sleep/downtime shift), `workflow.notified` (takeoverIdle), `workflow.expired` (held event timed out), `workflow.effect-refused`.
- `trigger`: `start, step.completed, step.failed, wait.satisfied, answer` (+ `takeover, handback, cancel, deadline`, `command.<verb>`). `via`: `block` (engine ran it) or `agent` (agent session). `actor`: open string (`host:sindri`, `human:cli`, `human:ui`, `service:block:<step>`, `session:cli`). `decision` = Judge decision id (`JD-7`) of a block result. `reason` for `step.failed`: `launch, lost, exit, limit` or text.
- Live: the SSE stream (`GET /sindri/events?after=N[&item=][&types=][&instance=<address>]`, `docs/csharp/sindri.md:236`; types catalog `Golden/Api/events-catalog/catalog.json`, key shape e.g. `item.state-changed:<itemId>:v<n>`). **In this snapshot the catalog lists no `workflow.*` type** (25 types: item, job, launch, session, service, profile, request, decision, fleet, process): the workflow stream is only the per-instance poll above (the stream can be filtered with `&instance=<address>`, but no workflow event type is declared to invalidate a view). Gap, see report.
- Ledger sequence numbers are even in the golden (each transition mirrors 2 rows); the mockup events follow that.

## 7. Context map: scope map, lenses, code, history

Served by `docs/csharp/sindri-context.md`; goldens `Golden/Api/context-*`. Graph (context retrieval spec 6.1):

- **Node kinds** (key = `<kind>:<path>`): `feature` (nested by `part-of`), `surface` (`kind`: screen, pane, dialog, command, keybinding, setting, menu, route, api, job, notification, permission, report, flag), `action`, `thing` (data/state), `flow`, `decision` (`decision:<sha256>`), `record` (`record:linear:issue/<id>`), `code` (`code:src/x.ts` or `#symbol`).
- **Edge types**: `part-of` (feature/surface -> feature), `on` (action -> surface), `moves-to`, `reads`/`writes` (surface/action -> thing), `depends-on`, `step-of` (-> flow), `implements`/`tests` (code -> feature/surface/action), `decides` (decision -> node), `mentions` (record -> node), `quotes` (decision -> record), `supersedes`.
- Each node/edge: `provenance: human|model|deterministic`, `confirmed:bool`, `confidence 0..1`, `trust: trusted|untrusted`; edges carry `link: edge:<hash>`. Model-suggested links are `confirmed:false` / `suggested:true`.
- **LensesView** (`GET /sindri/context/lenses?anchor=…`; golden `context-lenses/map-tabs.json`): `{anchor{key,label,kind,trust}, depth, mapVersion, draft, bands{product{own[],neighbors[],affects[],counts,edges[]}, intent{records[],decisions[],counts}, code{files[],symbols{state,reason,items},counts}}, coverage{scope, intent{covered,of}, product{confirmed,of}, code{implemented,of,level,suggested}, gaps[{node,missing[intent|code],note}], map{paths,files,unmapped,suggested,ageDays}}, conflicts[{kind:"supersedes",node,decision,other,summary}], edges[], cites{}, degraded[], searched{anchors,depth,hops[state|flow|dependency|component],sources[{id,state,reason}],bands}}`. Several anchors -> `LensesSetView` (up to 20).
- **CodeView** (`context-code`): `files[{path,implements[],tests[],provenance,confidence,confirmed,suggested,linkId,kind,trust}]`, `tests`, `data{things,files}`, `callers`/`callees` (`unavailable` + `index-not-built` until the code index exists), `lexical[]`, `counts`, `coverage`, `searched`, `degraded`, `cites`, `edges`, `truncated`.
- **HistoryView** (`context-history`): `window{since,before}`, `records[]`, `decisions[]`, `changes[{key:"commit:<sha>", repo, sha, at, subject, ...}]`, `incidents{state,reason,items}`, `git[{repo,head,files,changes}]`, `counts`, `coverage`, `edges`, `truncated`.
- **Node view** (`context-node`): `{node, aliases, in[], out[], mapVersion}`; file map: `context-filemap`; subgraph: `context-subgraph` (`nodes[], bands, edges`).
- **ScopingView** (the owner's stop 1 view; `workflow-instance-scoping`, golden = a *Link expiry* item): `{stage, coverageHeader ("1 of 4 lenses limited"), openInContext{enabled,reason}, sections[{order,name,title,state: ready|empty|not-yet, note, rows[{key,text,status,lines[]}]}]}`. Section names in order: `checklist, flagged, excluded, acceptance, questions, answers, challenge, rounds, map`. Row `status`: `pass, limit` (checklist), `excluded, unmapped`, `failing` (acceptance not yet proven), `found, nothingNew` (challenger rounds), `mapped, new, found` (map). Checklist keys are `SCP-*` rule ids (`SCP-CITE-001`, `SCP-COV-003`, `SCP-ACC-002` ...), plus one row per lens (`code: not searched: index-not-built`). Real data, embedded in `sample.json` as `scopingStopView` (rows trimmed).
- `sample.json` `contextMap` is a coherent subset for the Link expiry item (23 nodes, 23 edges): node keys/kinds/status words (`mapped`, `new`) are the golden's; edges are plausible additions (the stop view lists keys only, not edges); flagged in `x_note`.

## 8. The line, the stations, the packets (the "workflow.line" story)

Recipes and their states (all in `sample.json.recipes`; counts n states / e transitions):

| Recipe | Role | Key states (wait on child in **bold**) |
|---|---|---|
| `workflow.line` v1 | meta recipe; sequential stations | **scoping**, **design**, **redesign** (loop cap 2), **plan**, **phase**; ends `done` / `dropped` / `stuck` |
| `workflow.scoping` | station 1 | intake, read-brief/read-tracker/read-change, **idea** (child idea-to-brief), gather, draft, check, questions, interview (human), record, apply, reanchor, challenge, challenge-log, size (route) / size-desk, propose, evidence, **stop (human)**, freeze, withdraw; ends `scoped` / `dropped` |
| `workflow.design` | station 2 | intake, draft, apply, check, round, lens, lens-log, revise, lens-desk (human), evidence; ends `designed` / `dropped` |
| `workflow.plan` | station 3 | intake, graph, check, exec-review, holdout, evidence, questions, decide (human), record, settle, **stop (human)**, freeze; ends `planned` / `redesign` (unresolved) / `dropped` (failed) |
| `workflow.phase` | one PR | seed, **build**, **prove**, **review**, drift, **ship**, **babysit**, owner (human); ends `merged` / `dropped` |
| `workflow.build`, `.prove`, `.ship`, `.review-loop` v2, `.pr-babysitter` (stub) | phase children | see `sample.json` |

The **packets** (`aw-packets`) chain: scope packet -> design packet -> plan graph -> phase packets -> proof pack -> review result -> PR hand-off; each is hashed and **frozen** by exactly one block; owner-frozen ones are frozen only by a human-only act (section 3.2/3.3 of the line spec). In the data they are facts of type `packet`.

Sample caveat: the line's own `description` says "Sequential stations ... parallel phase dispatch comes with LN-WF1": the shipped `workflow.line` runs ONE phase child, not a dispatcher fan-out.

## 9. Error / reference codes the UI may meet

`SND-WF-001` not found, `-003` trigger not allowed here, `-004` a gate refused, `-005` stale run token, `-008` malformed desk report, `-009` nonce used, `-010` no runtime for a block in this build (parks), `-011` effect not on allowlist, `-012` earlier attempt live, `-015` effect could not be sent, `SND-AUTH-002` human-only action refused to a session, `SND-RECIPE-001..010`, `RCP-*` validator rules (`docs/csharp/sindri.md:725-760`).
