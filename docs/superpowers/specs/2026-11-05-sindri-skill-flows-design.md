# Sindri skill flows: chain repo skills from one trigger, and jump into any running one

Status: DRAFT (2026-11-05). Engine wrote §0-§5 and §7-§9; ui writes §6. No ready until 3 /autoplan rounds, meta's UX check and the owner's read. Lane: engine (lead) + ui. Related: the engine spec (2026-10-10), the session launcher (2026-10-15), the sandbox (2026-10-18), the Linear hub (#2077, context).

Research behind this note (file:line refs): `~/repos/aw-wt/briefs-engine/flow/flow-internal.md` (Sindri today), `flow-external.md` (n8n, Node-RED, Windmill, Kestra; Avalonia node editors; reference languages), `flow-decisions.md` (canvas and draft rules agreed with ui). Paths below are relative to the repo root unless they start with `~` or `$`.

## 0. The owner's ask (2026-11-05, via meta)

"I was trying to take the types of skills that I have in my repo and wire them together to create workflows that require one trigger from me and I'm able to interject myself into an agent session whenever I need to." First release:
1. Skills as blocks: each repo skill (officeHours, system-design, autoplan, specToProvenPR, bugFixOrchestrator, review, formal-verify...) is a block that runs as an agent session with typed inputs and outputs (the artifacts it writes).
2. Wire them into a chain that starts from one owner trigger (a button, a CLI verb; a Linear issue later).
3. Jump in anytime: open a running block's live agent session (Warp + the agent CLI), work in it, hand it back. Built on the existing takeover and handback.
The n8n-style palette (Slack, cron, data and code blocks) is a later phase.

## 1. Side by side (one page)

| | What the owner wants | What Sindri has today | The n8n-style pattern (Windmill, Kestra where they differ) |
|---|---|---|---|
| **Block** | A repo skill, run as an agent session | 163 C# blocks (`ShippedBlockSentences.cs`); agent Steps run Sindri's own instruction classes (`Features/BugFix/ReproduceInstructions.cs` ...). No block runs a skill: Sindri's recipes re-implement them (scoping ~ officeHours, line ~ specToProvenPR, bugfix ~ bugFixOrchestrator) | A node type in code (`n8n-nodes-base.httpRequest`), or a script (Windmill) |
| **Typed IO** | Inputs (an idea, a plan folder, a PR) and outputs (the files it wrote, its verdict) | Facts are untyped blobs: "type picks a viewer and nothing else" (`docs/csharp/sindri-workflow.md:306`). Skills already write a result JSON under `AW_RESULT=1` (`skills/_shared/output-contract.md`: `skill`, `artifacts`, `summary`, plus `verdict`, `feature_dir`, `handoff_path`) | n8n: none, items are free JSON. Windmill: JSON Schema from the script's signature, checked in the editor |
| **Wiring** | "This block's plan goes into that one" | Recipe facts by key; `with: {fact: key}`; RCP-WIRE-001 proves every input is produced on every path in (`Validation/WiringRules.Upstream.cs`) | n8n: JS expressions `$('Node').item.json.x`, keyed by node NAME (a rename breaks them), errors only at run time. Windmill / Kestra: by step id |
| **Trigger** | One button or one command | Explicit starts only: `sindri workflow start`, the app's Start page, `item add` + router (`Workflows/ItemRouter.cs`). No cron, no webhook | Trigger nodes: schedule, webhook, poll |
| **Run history** | See what ran and what each block wrote | Every transition in the ledger (`RecipeWorkflow.Mirror.cs:17`), the instance frame (`Views/Instance/*`) | `execution_entity` + per-node run data |
| **Test one block** | Try a block before wiring it in | `recipe eval` tests routing with stubbed blocks; `workflow rewind --to <state>` reruns a state on kept facts. No "run this one block" | "Execute step" with pinned data |
| **Jump in** | Open the live session, work in it, hand it back | `sindri takeover` pauses the state timer and opens the step's tmux session (`tmux -L sindri-launch`) in the viewer (Warp); the agent keeps running; `handback` re-arms the timer (`sindri-launcher.md:224-236`, `sindri-workflow.md:1377-1395`) | None: n8n nodes are not sessions |
| **Human question** | The skill asks me; I answer | Desk Steps and `answer`. A session that waits on the user looks idle: re-prompt, then `step.failed` after 10 min (`sindri-workflow.md:1470-1475`). The session collector already sees `asking` (`docs/csharp/sindri-sessions.md:24`) | Wait node, "send and wait for response" |
| **Loops** | "Needs work" goes back, at most N times | `loops: {cap, noProgress}`; a cap hit parks (`sindri-workflow.md:384-396`) | n8n: a back-wire with no cap. Windmill: `whileloopflow` |
| **Parallel** | Later | One active state per instance; parallel through child workflows | n8n: one thread. Windmill: `branchall` |
| **Edits under running runs** | Editing a flow never breaks a run that's going | Instances pin (id, version, hash) and the hash of every block they use; a changed block drift-parks them until re-approval (`sindri-workflow.md:180, 199-245`) | n8n: a waiting run resumes against renamed nodes and breaks |
| **Editor** | A canvas, later palette | None. Recipes are YAML, with JSON Schemas for editors (`schemas/recipes/recipe.editor.schema.json`). The run canvas draws line stages read-only (`Views/Run/RunCanvasWorld.cs`) | Canvas is the source of truth (n8n); in Kestra, YAML is the source and the graph a view |

Reading the table: Sindri already has the hard half (durable runs, pins, loops with caps, takeover into a live session). What's missing is the thin half: a block that runs a skill, a short file to wire skills, a "the skill is asking you" state, and an editor.

## 2. Decisions so far (engine's picks; each gets tested in the autoplan rounds)

D1. **A skill is a block made from a small manifest, not C#.** `skills/<name>/block.yaml` declares `inputs` (typed: text, path, pr, issue, packet dir), `outputs` (keys mapped from the result JSON: `artifacts[...]`, `feature_dir`, `verdict`, `handoff_path`), and `outcomes` (from `verdict`, else `done`). Sindri loads the manifests at serve start as descriptors `step.skill.<name>` (runtime agent; the `step.` prefix keeps them in the existing Step slot, so no new block kind). The descriptor hash covers the manifest only; the launch records the SKILL.md sha for audit (owner Q1).
D2. **The block's session:** an agent Step through the existing launcher, seeded with `/<skill> <args from inputs>`. It finishes when the skill writes its result file: Sindri reads it and completes the Step (§3.3). The agent never has to know it runs under Sindri.
D3. **Asking is not idle.** A skill block whose session is `asking` becomes a needs-you row "<block> asks you something" with **Answer** (open the session, no takeover) and **Take over**; it is never idle-failed, and its state timer pauses while it asks (§5.3).
D4. **The flow format compiles to a recipe; it doesn't replace recipes in release 1.** A flow file compiles to the recipe long form, so the validator (74 RCP rules), evals, approval, pins, Temporal and the ledger all apply unchanged. Refs are by block id. Positions live in a sidecar outside the hash. Meta's "graph replaces recipe YAML" is the later phase if the compiler hits a wall; §8.3 says what would prove that.
D5. **Sequential with branches and capped loops in release 1.** Parallel blocks need child workflows (later phase). Default routing: `done` -> next block; `failed` -> a desk "try again / stop", with Take over on the same row.
D6. **Trigger:** "Run" on the flow in the app and `sindri flow run <id> [--input k=v]`, both through `workflow start`. Linear issue ("Run flow on this issue") after the hub spec lands (#2077).
D7. **Skills that push or open PRs** (specToProvenPR, bugFixOrchestrator, shipRelease): agent Steps get scrubbed GitHub tokens and a pre-push hook that refuses pushes (`docs/csharp/sindri-launcher.md:238-256`). Recommendation: a per-block `push` grant the owner sees and approves in the approval preview, which passes the token to that block's session only. Owner Q2.
D8. **Sandbox and policy:** skill blocks write `$AW_DIR` (`$AW_STATE_DIR/<slug>/`), outside the worktree: the sandbox plan mounts it writable and the `claude-settings-file` policy adds it to the writable paths (today Edit/Write are limited to the Step's worktree, `sindri-launcher.md:258`). Bins and MCP servers come from each skill's `requires.yaml`.
D9. **Canvas (ui, §6):** custom, on `ZoomBorder`, `GraphEdgesControl`, `MiniMapControl` and the run canvas; no third-party node editor unless one passes the UI AOT smoke clean. Server-side drafts, a candidates call at drag start, keyboard wiring, optimistic edits, undo as inverse ops, no ledger rows for draft edits, `draft.changed` events, one draft per flow (the assistant edits it too), Health lists drafts idle > 14 d, test results keyed to (draft, rev). Full list in flow-decisions.md.
D10. **References:** structured `{ ref: <block>.<output> }` checked against the producing block's declared outputs at validate time. Computed values (a CEL subset) only in the n8n phase.

## 3. The skill block (D1, D2, D7, D8)

### 3.1 The manifest

One file per skill that can be a block, `skills/<name>/block.yaml`, beside `SKILL.md` and `requires.yaml`. Release 1 ships six: officeHours, autoplan, specToProvenPR, bugFixOrchestrator, review, system-design. A skill without a manifest is not a block; the palette lists only those that have one.

```yaml
# skills/autoplan/block.yaml
schema: 1
version: 1                      # bumps on any change; the descriptor hash covers this file
sentence: Reviews a plan through five lenses and says pass, needs work or blocked.
inputs:
  plan:   { type: path, label: Plan folder, required: true }
  mode:   { type: text, label: Review mode, required: false, values: [mvp, growth, scale, pivot] }
args: "{plan} --mode {mode}"    # the seed is "/autoplan <args>"; an empty optional drops its flag
outputs:
  featureDir: { type: path, from: feature_dir }
  report:     { type: path, from: "artifacts[consolidated-review.md]" }
outcomes:
  from: verdict
  map: { PASS: pass, NEEDS_WORK: needsWork, BLOCKED: blocked }
pushes: false
maxDuration: 3h
```

- **Input types:** `text` (one string), `path` (an absolute path the session can read: a file or a folder), `pr` (a PR URL or number; the forge port parses it), `issue` (a tracker key or pasted text), `packet` (a packet folder). The type picks the form field in the app and is what the canvas checks a wire against. The engine passes every input the same way: substituted into `args`, and written as `<key>.json` into the run directory (the launcher already does this for declared inputs, `sindri-launcher.md:256`).
- **Outputs** read the result JSON: a top-level key (`feature_dir`), `verdict`, or one artifact by file name (`artifacts[<name>]`, the one path in `artifacts` ending in `/<name>`). Output types are `text` and `path`. A **path fact holds the path, not the file**: a later block may edit the folder in place (§4.2 relies on this), and the file's sha256 at completion is recorded on the run for audit.
- **Outcomes:** `from: verdict` with a `map` to camelCase outcome names, or none: then the only outcome is `done`. A verdict missing from the map fails the Step with `SND-SKILL-003 <skill> said '<verdict>', which its block.yaml doesn't map`.
- **Checks** (`node scripts/check-skill-blocks.mjs`, beside the existing `scripts/check-skill-requires.sh`): schema, every `{name}` in `args` is an input, every output's `from` names a key the skill's Result block lists (read from its SKILL.md Result section), outcome names are camelCase and distinct. CI runs it in the bash suites.

### 3.2 From manifest to descriptor

At serve start Sindri reads every `skills/*/block.yaml` under the toolkit (`${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}`) and registers one descriptor per file:

| Descriptor field | From the manifest |
|---|---|
| `Id` | `step.skill.<kebab name>` (`step.skill.office-hours`) |
| `Runtime` | agent |
| `Parameters` | one per input, `ParameterType.Text`, `Required` as declared. Values come as `with: {fact: ...}` or a literal; RCP-WIRE-001 already checks fact-valued parameters (`WiringRules.Upstream.cs:94`) |
| `Outputs` | `fact:` per output, `RequiredOn` every outcome |
| `Outcomes` | the mapped outcomes, or `done` |
| `Capabilities` | `Tools`, `WriteScope`, plus `Effects` when `pushes: true` (it shows in the approval preview) |
| `MaxDuration` | `maxDuration` |
| `Sentence` | `sentence` (not hashed, like every sentence today) |

A manifest that fails the checks is skipped with a doctor row `skills:block <name> is not loaded: <reason>`, never a failed serve. Manifest edits need a serve restart in release 1 (the registry is built at start); `sindri doctor` warns when a loaded manifest's file hash differs from the one on disk.

### 3.3 The session and how it finishes

1. **Launch.** The existing `LaunchAgentStep`: worktree, sandbox, policy and scrubbed env as for any agent Step, plus three env vars: `AW_RESULT=1`, `AW_RESULT_SKILL=<name>`, `AW_RESULT_FILE=$AW_SINDRI_RUN_DIR/.sindri-out/result.json`. The seed is `/<name> <args>`.
2. **The skill writes its result.** One change to `skills/_shared/output-contract.md` and to the Result block of each skill with a manifest: when `AW_RESULT_SKILL` equals the skill's own name, write the result to `AW_RESULT_FILE` instead of `$AW_DIR/results/<skill>-<ts>.json`. The name check matters: autoplan runs five lens skills in the same session, and they must keep writing their own default files rather than overwrite autoplan's.
3. **Sindri completes the Step.** The watch reads `result.json` when the session's turn ends (the same moment today's recovery runs, `RecoverAgentStep`, `sindri-workflow.md:1479-1489`), maps outputs and outcome by the manifest, and applies `complete` as `agent:result`. No `sindri workflow complete` call from the agent is needed; one still works if a skill makes it.
4. **No result file at turn end** (the skill stopped early, or chained into a question in prose): today's idle rule applies unchanged: one re-prompt ("finish the skill and write its result"), then `step.failed` after 10 min, then the flow's failure desk (§4.3).

Worked example: autoplan's run ends with `{"skill":"autoplan","verdict":"NEEDS_WORK","feature_dir":"/home/u/.agentic-workflow/acme-api/plans/20261105-rate-limits","artifacts":[".../consolidated-review.md", ".../arch-review.md", ...]}`. Sindri records `fact:critiqueFeatureDir = ".../plans/20261105-rate-limits"`, `fact:critiqueReport = ".../consolidated-review.md"` (plus each file's sha256 on the run), and the outcome `needsWork`.

### 3.4 Where it runs and what it may touch

- **Working directory:** one worktree per flow run, `flow/<run>` off the repo's default branch, shared by the blocks of that run in order (release 1 is sequential, so they never write at the same time). A skill that makes its own worktrees (specToProvenPR) makes them from there.
- **`$AW_DIR`:** mounted writable in the sandbox plan and added to the policy's writable paths (D8). Two flow runs on the same repo share `$AW_DIR`; the result file is per run (step 2 above), and the skills' own folders are timestamped, so they don't collide.
- **Push (D7, owner Q2):** `pushes: true` in the manifest is a request. The block gets the token only if the flow's approval was given with that block's push shown in the preview. Then its launch skips the token scrub and the pre-push hook. Otherwise the block runs scrubbed, and a push fails the way it does today (`SND-LAUNCH-023`).
- **Agent and model:** the profile's `workflow.agent.target`, or the flow block's `agent:` (same rules as recipe states, RCP-AGENT-001).

## 4. The flow format and the compiler (D4, D5, D10)

### 4.1 The file

`flows/<name>.yaml` in the toolkit (shipped flows) or in a repo's `.agentic-workflow/flows/` (the owner's). Short on purpose:

```yaml
# flows/idea-to-pr.yaml
schema: 1
id: flow.idea-to-pr
version: 1
displayName: Idea to proven PR
why: Shape an idea into a plan, review it, and build it into proven PRs.
inputs:
  idea: { type: text, label: What do you want built? }
name: "{input.idea}"             # the run's name, same template rules as a recipe's
blocks:
  shape:
    use: skill.officeHours
    with: { idea: { ref: input.idea } }
  critique:
    use: skill.autoplan
    with: { plan: { ref: shape.featureDir } }
    on:
      needsWork: { to: revise, loop: { name: rework, cap: 2 } }
      blocked: stop
  revise:
    use: skill.officeHours
    with:
      plan:   { ref: shape.featureDir }
      review: { ref: critique.report }
    on: { done: critique }
  build:
    use: skill.specToProvenPR
    with: { spec: { ref: shape.featureDir } }
```

Rules:
- `use: skill.<name>` names the skill as it is spelled in `skills/` (`skill.officeHours`); the compiler maps it to the descriptor id `step.skill.office-hours`.
- Blocks run in file order; an outcome with no `on` entry goes to the next block (`pass` from `critique` goes to `build`, skipping `revise`, because `revise` is only reached by `needsWork`: the compiler skips a block that is the target of an explicit edge when falling through). `done` from the last block ends the run as resolved. `stop` ends it as unresolved.
- `ref` is `input.<key>` or `<block id>.<output>`. Checked at validate time: the block exists, declares that output, the output's type fits the input's type (`path` into `path` or `text`; `text` into `text`), and the producing block runs before the reader on every path (that last one is RCP-WIRE-001 on the compiled recipe, mapped back to the flow's names).
- A back-edge must name a `loop` with a `cap` (compiles to RCP-LOOP-001/002). A cap hit parks the run as today, with the needs-you row "rework hit its cap of 2".
- The `revise` block shows the "path fact holds the path" rule: officeHours revises the plan folder in place, so `critique` reading `shape.featureDir` sees the new plan on its second pass. F-1 includes the officeHours change for this (it accepts a `plan` folder and `review` notes and edits the folder in place; today its argument is only a description).
- Positions for the canvas live in `<name>.layout.json` beside the file, never in the hash.

### 4.2 What it compiles to

`sindri flow compile <file>` prints the recipe long form; validate, eval, approve and run all go through it. For the file above (trimmed: timeouts and labels omitted):

```yaml
id: workflow.flow.idea-to-pr
version: 1
inputs: [{ key: fact:idea, type: text, kind: text }]
initial: shape
states:
  shape:    { use: step.skill.office-hours, with: { idea: { fact: fact:idea } } }
  critique: { use: step.skill.autoplan, with: { plan: { fact: fact:shapeFeatureDir } } }
  revise:   { use: step.skill.office-hours, loop: rework,
              with: { plan: { fact: fact:shapeFeatureDir }, review: { fact: fact:critiqueReport } } }
  build:    { use: step.skill.spec-to-proven-pr, with: { spec: { fact: fact:shapeFeatureDir } } }
  shape-failed:    { use: step.flow.failed-desk, loop: retry-shape }     # one per block
  critique-failed: { use: step.flow.failed-desk, loop: retry-critique }
  # ... revise-failed, build-failed
  done:     { outcome: resolved }
  stopped:  { outcome: unresolved }
loops: { rework: { cap: 2, noProgress: [emptyDiff] }, retry-shape: { cap: 3 }, ... }
transitions:
  - { from: shape,    to: critique, on: step.completed, produces: [fact:shapeFeatureDir] }
  - { from: critique, to: build,    on: step.completed, when: { outcome: pass },      produces: [fact:critiqueReport, fact:critiqueFeatureDir] }
  - { from: critique, to: revise,   on: step.completed, when: { outcome: needsWork }, produces: [fact:critiqueReport, fact:critiqueFeatureDir] }
  - { from: critique, to: stopped,  on: step.completed, when: { outcome: blocked } }
  - { from: revise,   to: critique, on: step.completed }
  - { from: build,    to: done,     on: step.completed }
  - { from: shape,    to: shape-failed, on: step.failed }
  - { from: shape-failed, to: shape,   on: step.completed, when: { outcome: again } }
  - { from: shape-failed, to: stopped, on: step.completed, when: { outcome: stop } }
  # ... the same pair per block
```

- **Fact names:** `fact:<block id><Output>` in camelCase (`shape` + `featureDir` = `fact:shapeFeatureDir`). Flow inputs are `fact:<key>`.
- **Failure desk:** one new shipped human desk block `step.flow.failed-desk`, outcomes `again` and `stop`, one per flow block, each behind its own retry loop (cap 3). Take over is a button on the same needs-you row, not an outcome.
- **Errors point at the flow.** Every compiled state and transition carries its flow source position. A validator finding is printed against the flow file (`flows/idea-to-pr.yaml:14 critique.with.plan: 'shape.featurDir' is not an output of skill.officeHours (it has: featureDir, plan)`), never against compiled state names.
- **Hash and pins:** the recipe hash covers the compiled long form, so it changes exactly when the flow's meaning changes (a rename of a block id does change it; moving a card on the canvas doesn't). Approval pins the compiled recipe and every `step.skill.*` descriptor it uses, as for any recipe. Editing a skill's `block.yaml` drift-parks running flows that use it until the flow is re-approved; editing its SKILL.md doesn't (owner Q1).
- **Versioning:** the flow's `version` is the recipe's version; RCP-VERSION-001 applies (a content change bumps it).

### 4.3 Evals

Approval needs evals today (`sindri-workflow.md:199`), and the compiler gives them for free: `sindri flow eval --scaffold <file> --out <dir>` writes one case per outcome path (it calls `recipe eval --scaffold` on the compiled recipe), with the skills stubbed by their outcomes, as every eval does. `idea-to-pr` gets 5 cases: pass, needsWork then pass, needsWork x3 (cap hit, parked), blocked, and a failed block then again. The owner edits nothing: the scaffolded cases are the eval set unless they add their own.

## 5. Trigger, run page and jumping in (D3, D6)

### 5.1 Starting a flow

- **CLI:** `sindri flow run idea-to-pr --input idea="rate limits per API key"` makes an item named from the flow's `name` template, starts the compiled recipe through `workflow start`, and prints the run's address and `sindri workflow status <address>`. Exit 0 started, 1 not running (`SND-API-001`), 2 refused (unapproved: `SND-RECIPE-001`, a missing input, an agent session: `SND-AUTH-002`, since a flow can start sessions that push).
- **App:** a Flows page lists approved flows; **Run** opens a form with the flow's inputs (field per type: text box, folder picker, PR box) and starts it. The Start page lists flows beside recipes.
- **Not in release 1:** cron, webhook, Linear (F-6, after #2077).

### 5.2 The run page

A flow run is a recipe instance, so the instance frame (`Views/Instance/InstanceFrameView`, states band, facts panel, timeline) already works. What ui adds (F-4) is block cards: each block's state (waiting, running, asking, done with its outcome, failed), its outputs as clickable paths, and on a running block **Open session**. Loop rounds show on the card ("round 2 of 2").

### 5.3 Jumping in

Two actions on a running skill block, both on the run page and on its needs-you row:

| Action | What happens | Timer | The agent's own result |
|---|---|---|---|
| **Answer** | Opens the block's live tmux session in the viewer (Warp). No takeover. For answering a question or watching | Pauses while the session is `asking`, runs otherwise | Accepted as normal |
| **Take over** | Today's takeover: the instance is `taken-over`, the session opens in the viewer | Paused until handback | Refused until handback (today's rule) |

- **Answer** is new: a route that runs only the viewer half of today's takeover (`launch.takeover-opened`'s open, recorded as `launch.attached`). Nothing to hand back, so answering a skill's question is one click and some typing.
- **Asking pauses the clock.** The session collector reports `asking` for the block's launch (from the fleet hook; `docs/csharp/sindri-sessions.md:24`). Sindri sends the instance an `asking` signal; the workflow pauses the state timer the way a takeover does and keeps the remaining time, and the idle rule (§3.3 step 4) never counts `asking` time. When the session goes back to `running`, a `resumed` signal re-arms the timer. That is a workflow behavior change, so it goes behind a `Workflow.Patched("skill-asking-pause")` marker. The needs-you row "Shape the idea asks you something" appears with the first `asking` and leaves with the next `running`.
- **Handback after a takeover:** if the owner finished the work in the session and the skill wrote its result file while taken over, handback completes the Step from that file at once (the same read as §3.3 step 3). If there's no result file, the instance continues in the same state and the session carries on.
- **Providers:** the `asking` state comes from the fleet hook, which reports it for Claude Code today. A Codex or Cursor session has no `asking`, so release 1 falls back to the idle rule for them, with a longer idle limit for skill blocks (60 min instead of 10). Owner Q4.

## 6. The canvas (ui writes this; D9): TODO (ui)

## 7. API and CLI

All new routes are on the existing loopback control plane and follow its error and auth rules.

| Route | What it does | Caller |
|---|---|---|
| `GET /sindri/skills` | Loaded skill blocks: name, descriptor id and hash, sentence, inputs, outputs, outcomes, `pushes`, SKILL.md sha, and the doctor reason for any that didn't load | any |
| `GET /sindri/flows`, `GET /sindri/flows/{id}` | Flows with their approval state, the flow source, its layout and the compiled recipe's hash | any |
| `POST /sindri/flows/validate` | Body: a flow file. Findings against flow positions (§4.2). Writes nothing | any |
| `POST /sindri/flows/{id}/runs` | Body: inputs. Starts a run (§5.1) | owner; a session is refused `SND-AUTH-002` |
| `POST /sindri/launches/{launch}/attach` | Answer (§5.3): opens the viewer, no takeover | owner |
| F-5 drafts: `POST /sindri/flows/{id}/draft`, `PATCH .../draft` (`{rev, ops[]}`), `GET .../draft/candidates?rev=&port=`, `POST .../draft/test` (one block, keyed to `(draft, rev)`), `POST .../draft/promote` | The canvas's server-side draft, per flow-decisions.md: PATCHes not in the ledger, `draft.changed` on the event stream, promote = a new flow version (ledger `flow.saved`) | owner, the assistant |

CLI (each an API client unless noted): `sindri skills [--json]`; `sindri flow list|show <id>`; `sindri flow validate|compile|eval <file>` (in process, no serve, like `recipe validate`); `sindri flow approve <file>` (register + approve the compiled recipe; the preview lists each skill block, its pushes and its SKILL.md sha); `sindri flow run <id> --input k=v ...`. All listed in `Usage.cs`, the help goldens and aot-smoke's expected text.

Ledger: `flow.saved`, plus the existing `recipe.registered|approved`, `workflow.*` and `launch.*` events; new `launch.attached`; `workflow.transition` rows carry `skill` and the result file's sha256 for a skill block's completion.

## 8. Phases and the parallel plan

### 8.1 Phases

| Phase | What ships | Lane | Needs | Proof in the running app |
|---|---|---|---|---|
| **F-1** Skill blocks | `block.yaml` for the six skills + `check-skill-blocks.mjs`; the output-contract change (`AW_RESULT_SKILL` / `AW_RESULT_FILE`) in those six; manifest loader and `step.skill.*` descriptors; result-file completion; `$AW_DIR` in the sandbox plan and policy; `GET /sindri/skills`, `sindri skills`; the officeHours revise input | engine | - | A one-state hand-written recipe running `step.skill.autoplan` on a real plan folder completes with the right verdict and facts, no `workflow complete` call from the agent |
| **F-2** Asking and Answer | `asking` / `resumed` signals and the timer pause (Patched); idle rule ignores asking; the skill-block idle limit; `POST .../attach`; the needs-you row with Answer and Take over; handback completes from the result file | engine (server) + ui (row, buttons) | F-1 | officeHours in a flow asks its first question; the row appears; Answer opens Warp; the timer shows paused; answering resumes it |
| **F-3** Flow format | Flow schema, compiler, source-position mapping of RCP findings, `step.flow.failed-desk`, `flow validate|compile|eval|approve|run`, `/sindri/flows*`, the push grant in the approval preview, the first shipped flow `idea-to-pr` with its evals | engine | F-1's descriptors (a fake skill registry is enough to start) | `sindri flow run idea-to-pr` on a scratch repo runs shape -> critique -> build end to end |
| **F-4** Run page for flows | Block cards on the instance frame, Open session, loop rounds; Flows page with Run and its input form | ui | F-2's routes, F-3's `/sindri/flows` | The F-3 run, watched and jumped into from the app only |
| **F-5** Canvas | §6, drafts API, candidates, test one block | ui + api | F-3's format frozen | Build `idea-to-pr` on the canvas from an empty flow, run it |
| **F-6** Linear trigger | "Run flow on this issue" | context | #2077 merged, F-3 | One issue starts one run, linked both ways |
| Later | Parallel blocks (children), the n8n palette (cron, Slack, code, logic, CEL), a native graph runtime if §8.3 says so | - | - | - |

### 8.2 Parallel plan (always-busy)

- **Engine, two builders from day one:** builder A on F-1 (manifests, contract change, loader, completion); builder B on F-3's compiler against a fixture skill registry (two fake manifests in `tests/Aw.Sindri.Testing`), so the compiler doesn't wait on F-1. They meet when F-1's descriptors land: B swaps the fixtures for the real loader.
- **ui, from day one:** F-4's view models and cards against fake routes and the golden JSON of `/sindri/flows` and `/sindri/skills` (engine writes the goldens first, in F-1 and F-3's red commits), then §6 (the canvas) in parallel with F-3.
- **After F-1:** engine on F-2's server half; ui on F-2's row and buttons.
- **context:** F-6 once #2077 and F-3 are in; nothing before.
- Red first in every phase: each PR opens with its failing tests and goldens.

### 8.3 When to stop compiling and build a graph runtime

Compiling stays the plan while every flow the owner writes fits it. Any one of these, met on a real flow, is the signal to write the native-runtime spec: (1) a flow needs two blocks running at once that children can't express cleanly; (2) a compiled flow passes 40 states (twice the median of today's big recipes, `flow-internal.md` §1) and its validator findings stop reading well against the flow file; (3) the owner wants an edit to apply to a running run, which pinning forbids by design.

## 9. Owner questions

1. **When you edit a skill's SKILL.md, should flows that are already running stop and wait for you?** My pick: no. Only a change to the block's inputs and outputs (`block.yaml`) stops them; a prompt edit is recorded on the run but doesn't stop anything. Otherwise every skill tweak parks every running flow.
2. **May a block push and open PRs?** My pick: yes, per block, only when you approve the flow and the preview shows "build (specToProvenPR) can push". Without it, specToProvenPR can't finish a PR inside a flow.
3. **What comes first, the canvas or a flow file plus the run page?** My pick: file + run page first (F-1 to F-4, about two weeks), canvas second (F-5). You can run real flows sooner, and the canvas is built on a format that's been tested.
4. **Is Claude Code only OK for "the skill is asking you" in release 1?** Codex and Cursor sessions don't report asking yet, so for them a block waiting on you looks idle, and gets 60 min before it's re-prompted. My pick: yes, Claude Code first.
5. **Which flow ships first?** My pick: idea -> officeHours -> autoplan (needs work loops back, at most 2) -> specToProvenPR. The other one I'd consider is bug -> bugFixOrchestrator -> review.
