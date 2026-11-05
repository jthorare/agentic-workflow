# Sindri skill flows: chain repo skills from one trigger, and jump into any running one

Status: DRAFT (2026-11-05). Engine wrote §0-§5 and §7-§9; ui wrote §6. Autoplan round 1 (NEEDS_WORK, R1-R21) is folded in: release 1 is re-cut to skill blocks, one hand-written recipe and "waiting on you" on the existing takeover; the flow format, its compiler and the canvas move to release 2. No ready until 3 /autoplan rounds, meta's UX check and the owner's read. Lane: engine (lead) + ui. Related: the engine spec (2026-10-10), the session launcher (2026-10-15), the sandbox (2026-10-18), the Linear hub (#2077, context).

Research behind this note (file:line refs): `~/repos/aw-wt/briefs-engine/flow/flow-internal.md` (Sindri today), `flow-external.md` (n8n, Node-RED, Windmill, Kestra; Avalonia node editors; reference languages), `flow-decisions.md` (canvas and draft rules agreed with ui). Round 1 reviews: `~/.agentic-workflow/joi-fairshare-agentic-workflow/plans/skill-flows-r1/`. Paths below are relative to the repo root unless they start with `~` or `$`.

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
| **Typed IO** | Inputs (an idea, a plan folder, a PR) and outputs (the files it wrote, its verdict) | Facts are untyped blobs: "type picks a viewer and nothing else" (`docs/csharp/sindri-workflow.md:306`). `ParameterType` is Text, Number, Flag or List. Skills already write a result JSON under `AW_RESULT=1` (`skills/_shared/output-contract.md`: `skill`, `artifacts`, `summary`, plus `verdict`, `feature_dir`, `handoff_path`), and a run that hands a question back writes one too | n8n: none, items are free JSON. Windmill: JSON Schema from the script's signature, checked in the editor |
| **Wiring** | "This block's plan goes into that one" | Recipe facts by key; `with: {fact: key}`; RCP-WIRE-001 proves every input is produced on every path in (`Validation/WiringRules.Upstream.cs`). A Step records only its descriptor's fixed fact keys; only a child's outputs can be renamed (RCP-WIRE-002) | n8n: JS expressions `$('Node').item.json.x`, keyed by node NAME (a rename breaks them), errors only at run time. Windmill / Kestra: by step id |
| **Trigger** | One button or one command | Explicit starts only: `sindri workflow start`, the app's Start page, `item add` + router (`Workflows/ItemRouter.cs`). No cron, no webhook | Trigger nodes: schedule, webhook, poll |
| **Run history** | See what ran and what each block wrote | Every transition in the ledger (`RecipeWorkflow.Mirror.cs:17`), the instance frame (`Views/Instance/*`) | `execution_entity` + per-node run data |
| **Test one block** | Try a block before wiring it in | `recipe eval` tests routing with stubbed blocks; `workflow rewind --to <state>` reruns a state on kept facts. No "run this one block" | "Execute step" with pinned data |
| **Jump in** | Open the live session, work in it, hand it back | `sindri takeover` pauses the state timer and opens the step's tmux session (`tmux -L sindri-launch`) in the viewer (Warp); the agent keeps running; `handback` re-arms the timer (`sindri-launcher.md:224-236`, `sindri-workflow.md:1377-1395`). Attach without a takeover is the app's `AttachButton` main action, waiting on API P10a | None: n8n nodes are not sessions |
| **Human question** | The skill asks me; I answer | Desk Steps and `answer`. A session whose turn ended sits 120 s (`LaunchRules.IdleAfter`) and then the idle sweep ends it as idle (`SweepLostSessions/IdleSessions.cs`); only an API-error stop gets one re-prompt first, and a session with no hook marker gets 10 min (`StalledAfter`). The fleet hook writes `asking` only for a permission prompt or an MCP elicitation, so a question in prose looks idle | Wait node, "send and wait for response" |
| **Loops** | "Needs work" goes back, at most N times | `loops: {cap, noProgress}`; a cap hit parks (`sindri-workflow.md:384-396`) | n8n: a back-wire with no cap. Windmill: `whileloopflow` |
| **Parallel** | Later | One active state per instance; parallel through child workflows | n8n: one thread. Windmill: `branchall` |
| **Edits under running runs** | Editing a flow never breaks a run that's going | Instances pin (id, version, hash) and the hash of every block they use; a changed block drift-parks them, and re-approving the same recipe version resumes them (`sindri-workflow.md:180, 199-245`) | n8n: a waiting run resumes against renamed nodes and breaks |
| **Editor** | A canvas, later palette | None. Recipes are YAML, with JSON Schemas for editors (`schemas/recipes/recipe.editor.schema.json`). The run canvas draws line stages read-only (`Views/Run/RunCanvasWorld.cs`) | Canvas is the source of truth (n8n); in Kestra, YAML is the source and the graph a view |

Reading the table: Sindri already has the hard half (durable runs, pins, loops with caps, takeover into a live session). What's missing is the thin half: a block that runs a skill, a rule that a skill waiting on the owner is waiting and not idle, a short file to wire skills, and an editor. Release 1 builds the first two and wires them by hand in a recipe; release 2 builds the file and the editor.

## 2. Decisions (engine's picks after round 1; each gets tested again in rounds 2 and 3)

D1. **A skill is a block made from a small manifest, not C#.** `skills/<name>/block.yaml` lists one or more blocks (officeHours ships two: start from an idea, and revise a plan from a review). Each declares an `id`, typed `inputs` (`text` or `path` in release 1), `outputs` mapped from the result JSON, `outcomes` with one `success` outcome, `pushes`, `maxDuration`. Sindri loads them at serve start as descriptors `step.skill.<id>` (runtime agent, so no new block kind). The descriptor hash covers the block's entry in the manifest; there is no hand-kept version field. The launch records the SKILL.md sha; a pushing block also pins it (owner Q1).
D2. **The block's session** is an agent Step through the existing launcher, seeded with the skill's invocation in the target agent's form (`/officeHours ...` for Claude Code, `$officeHours ...` for Codex). It finishes when the skill writes its result to a per-launch file Sindri names; Sindri reads it at each turn end and completes the Step (§3.3). The agent never has to know it runs under Sindri.
D3. **A skill block is never idle-failed.** Its turn ending without a finished result means it's **waiting on you**: a needs-you row with the skill's question (from the result's new `waiting` key) or "stopped without a result", and **Take over** on the row. The idle sweep skips skill-block launches. The state timeout is the only bound. No `asking` signal, no timer pause, no new Answer route in release 1 (round 1 R2, R14).
D4. **Release 1 wires blocks in a hand-written recipe.** The first chain, `workflow.skill.idea-to-pr`, is a shipped recipe in today's long form using `step.skill.*` Steps, so the validator, evals, approval, pins, Temporal and the ledger apply unchanged. The flow format and its compiler (§4) are release 2, built once the owner has run the hand-written chain and a second one (round 1 R10). The compiler needs one recipe change, Step output aliasing (R1), which is release 2 work too.
D5. **Routing is explicit.** In the recipe that's already true. In the release 2 flow format, a block's `success` outcome goes to its `next` (default: the next block in file order), and every other outcome must have an `on` entry; there is no hidden skip rule (R4, R5). Loops on skill blocks take a `cap` only (R3).
D6. **Trigger in release 1:** the existing starts (the app's Start page, `sindri workflow start`), with the recipe's input form. `sindri flow run` and the Flows page come with the format in release 2; Linear after #2077.
D7. **Skills that push or open PRs** (specToProvenPR, bugFixOrchestrator, shipRelease) run scrubbed today. A block with `pushes: true` gets a push grant only when the owner approved a recipe whose preview showed it. The grant is a git credential helper and a `gh` wrapper in the session that ask the control plane for the 1 h agent App token of that repo, minted per call and refused once the Step ends (R11). Starting any recipe with a pushing block is owner-only, wherever the start comes from (R9). Owner Q2.
D8. **Sandbox and policy:** the host computes the repo's slug once and passes `AW_DIR=$AW_STATE_DIR/<slug>/`; the sandbox plan mounts exactly that folder writable and the policy adds it to the writable paths (R12). Before launch Sindri checks the skill's `requires.yaml` against the agent home and the sandbox PATH and refuses with the missing names (R16).
D9. **Canvas (ui, §6, release 2):** custom, on `ZoomBorder`, `GraphEdgesControl`, `MiniMapControl` and the run canvas; no third-party node editor unless one passes the UI AOT smoke clean. Server-side drafts, a candidates call at drag start, keyboard wiring, optimistic edits, undo as inverse ops, no ledger rows for draft edits, `draft.changed` events, one draft per flow (the assistant edits it too). Full list in flow-decisions.md.
D10. **References (release 2):** structured `{ ref: <block>.<output> }` or a literal `{ value: ... }`, checked against the producing block's declared outputs at validate time. Computed values (a CEL subset) only in the n8n phase.

## 3. The skill block (release 1; D1, D2, D3, D7, D8)

### 3.1 The manifest

One file per skill that has blocks, `skills/<name>/block.yaml`, beside `SKILL.md` and `requires.yaml`. Release 1 ships five skills, six blocks: officeHours (two), autoplan, specToProvenPR, bugFixOrchestrator, review. A skill without a manifest is not a block.

```yaml
# skills/autoplan/block.yaml
schema: 1
blocks:
  - id: autoplan                 # descriptor step.skill.autoplan; ids are kebab-case, written out, never derived
    sentence: Reviews a plan through five lenses and says pass, needs work or blocked.
    inputs:
      plan: { type: path, label: Plan folder, required: true }
      mode: { type: text, label: Review mode, required: false, values: [mvp, growth, scale, pivot] }
    args: "{plan} --mode {mode}"     # an empty optional drops its flag
    outputs:
      report:     { type: path, from: "artifacts[consolidated-review.md]" }
      featureDir: { type: path, from: feature_dir }
    outcomes:
      from: verdict
      map: { PASS: pass, NEEDS_WORK: needsWork, BLOCKED: blocked }
      success: pass
    pushes: false
    maxDuration: 3h
```

```yaml
# skills/officeHours/block.yaml (two blocks, so no input has to be "this one OR those two", R19)
schema: 1
blocks:
  - id: office-hours
    sentence: Shapes an idea into a plan folder with requirements and tasks.
    inputs:  { idea: { type: text, label: What do you want built?, required: true } }
    args: "{idea}"
    outputs: { featureDir: { type: path, from: feature_dir } }
    outcomes: { success: done }
    pushes: false
    maxDuration: 3h
  - id: office-hours-revise
    sentence: Revises a plan folder in place from a review report.
    inputs:
      plan:   { type: path, label: Plan folder, required: true }
      review: { type: path, label: Review report, required: true }
    args: "--revise {plan} --review {review}"
    outputs: {}
    outcomes: { success: done }
    pushes: false
    maxDuration: 2h
```

- **Input types:** `text` and `path` (an absolute path the session can read). The type picks the form field (text box, folder picker). `pr`, `issue` and `packet` wait until a block needs them (C6). Every input is also written as `<key>.json` into the run directory (the launcher does this for declared inputs, `sindri-launcher.md:256`).
- **Long text:** a `text` value of one line up to 500 characters is put into the seed as is; anything longer, or with a newline, is replaced in the seed by `(the text in <run dir>/<key>.json)`, so the seed is always one line (R13).
- **Outputs** read the result JSON: a top-level key (`feature_dir`), `verdict`, or one artifact by file name (`artifacts[<name>]`, the one path in `artifacts` ending in `/<name>`). Output types are `text` and `path`. A **path fact holds the path, not the file**: a later block may edit the folder in place (the revise block relies on this), and the file's sha256 at completion is recorded on the run.
- **Fact keys** are fixed per block: `fact:<block id>.<output>`, so `fact:autoplan.report`, `fact:office-hours.featureDir`. Two Steps of the same block in one recipe write the same key, the later one winning (today's rule for a fact written on two transitions); a recipe that needs both keeps them apart by using different blocks. Renaming per use is release 2's aliasing (§4.2).
- **Outcomes:** `from: verdict` with a `map` to camelCase names and the one `success` outcome; with no `from`, the only outcome is `success` (named `done` by convention).
- **Checks** (`node scripts/check-skill-blocks.mjs`, beside `scripts/check-skill-requires.sh`, in the bash suites): schema; ids unique across all manifests; every `{name}` in `args` is an input; `success` is one of the mapped outcomes; every output's `from` names a key the skill's Result block lists. The Result block's grammar for that is fixed in F-1: a fenced `aw-result` example in SKILL.md, read as JSON.

### 3.2 From manifest to descriptor

At serve start Sindri reads every `skills/*/block.yaml` under the toolkit (`${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}`, the installed copy, not a checkout; `sindri skills` prints the path each block came from) and registers one descriptor per block:

| Descriptor field | From the manifest |
|---|---|
| `Id` | `step.skill.<id>` |
| `Runtime` | agent |
| `Parameters` | one per input, `ParameterType.Text`, `Required` as declared. Values come as `with: {fact: ...}` or a literal; RCP-WIRE-001 already checks fact-valued parameters (`WiringRules.Upstream.cs:94`) |
| `Outputs` | `fact:<id>.<output>` per output, `RequiredOn` the success outcome |
| `Outcomes` | the mapped outcomes, or `done` |
| `Capabilities` | `Tools`, `WriteScope`, plus `Effects` when `pushes: true` (it shows in the approval preview) |
| `MaxDuration` | `maxDuration`. An agent Step isn't held to it by the runtime; the recipe's state `timeout` is the real bound, and the check in §3.5 makes the recipe say it |
| `Sentence` | `sentence` (not hashed, like every sentence today) |

A manifest that fails the checks is skipped with a doctor row `skills:block <id> is not loaded: <reason>`, never a failed serve; no toolkit folder at all is an empty list and one doctor row. A recipe naming a block that isn't loaded fails validation with that reason. Manifest edits need a serve restart in release 1; `sindri doctor` warns when a loaded manifest's hash differs from the file on disk. After the restart, runs using a changed block drift-park, and the existing remedy applies: re-approving the same recipe version (`sindri recipe approve`) pins the new descriptor hashes and wakes them (`sindri-workflow.md:199-245`; R6). The needs-you row says it: "autoplan's block changed. Approve idea-to-pr again to continue: [Approve]".

### 3.3 The session and how it finishes

1. **Launch.** The existing `LaunchAgentStep`: worktree, sandbox, policy and scrubbed env as for any agent Step, plus `AW_DIR` (D8), `AW_RESULT=1`, `AW_RESULT_SKILL=<name>`, and `AW_RESULT_FILE=<run dir>/.sindri-out/result-<launch>.json`: a new name per launch, so a loop's second round can never read the first round's file. The `requires.yaml` check runs first (SND-SKILL-005 below).
2. **The skill writes its result.** One change to `skills/_shared/output-contract.md` and to the Result block of each skill with a manifest: when `AW_RESULT_SKILL` equals the skill's own name, write the result to `AW_RESULT_FILE` instead of `$AW_DIR/results/<skill>-<ts>.json`. The name check keeps autoplan's five lens skills, which run in the same session, writing their own default files. And in that mode, **a question goes back in the result, not in a picker**: the skill writes `"waiting": "<the question, one line>"` with the rest of the result as it stands, prints it, and ends its turn, instead of calling AskUserQuestion (a pending picker is invisible to Sindri: the session looks busy forever). When unset, nothing changes, and a bash test pins that.
3. **Sindri reads the file at each turn end** (the Stop hook's marker, the moment today's idle sweep looks):

| What it finds | What happens | Code |
|---|---|---|
| A result with no `waiting`, the `skill` matching, the verdict mapped, every output present | `complete` as `agent:result`: outputs to facts, verdict to outcome, each path's sha256 on the run | - |
| A result with `waiting` | Waiting on you; the row shows the question | - |
| No file, or one written before this launch started | Waiting on you: "stopped without a result" | - |
| The same file as at the last turn end (unchanged) | Still waiting on you | - |
| Not valid JSON | Waiting on you, with the parse error on the row (Take over, fix or tell the skill) | SND-SKILL-001 |
| `skill` names another skill | Waiting on you, "the result came from <other>" | SND-SKILL-002 |
| A verdict missing from the map | `step.failed`: "autoplan said 'MAYBE', which its block.yaml doesn't map" | SND-SKILL-003 |
| A mapped output missing | `step.failed`: "autoplan's result has no artifact 'consolidated-review.md' (it has: arch-review.md, ...)" | SND-SKILL-004 |
| (before launch) `requires.yaml` names a bin, MCP server or skill the agent home lacks | `step.failed` before any session starts, naming each one and its fix | SND-SKILL-005 |

   The two failures are block-author bugs, so they fail; everything the owner can sort out in the session waits for them. A `sindri workflow complete` call from a skill still works if one makes it.
4. **Waiting on you is bounded only by the state timeout.** The idle sweep skips skill-block launches; the session stays alive in tmux. The state's `timeout` ends the wait (`then: park`), and the park row offers Resume and Stop.

Worked example: autoplan's run ends with `{"skill":"autoplan","verdict":"NEEDS_WORK","feature_dir":"/home/u/.agentic-workflow/acme-api/plans/20261105-rate-limits","artifacts":[".../consolidated-review.md", ".../arch-review.md", ...]}` in `.sindri-out/result-LCH-7Q2.json`. Sindri records `fact:autoplan.report = ".../consolidated-review.md"`, `fact:autoplan.featureDir = ".../plans/20261105-rate-limits"` (plus each file's sha256 on the run) and the outcome `needsWork`. Had officeHours instead ended a turn with `"waiting": "Who calls this API: browsers, servers, or both?"`, the row would read "idea-to-pr › Shape the idea is waiting on you: Who calls this API: browsers, servers, or both? [Take over]".

### 3.4 Where it runs and what it may touch

- **Working directory:** one worktree per run, `flow/<instance>` off the repo's default branch, shared by the run's Steps in order (one active state, so they never write at the same time). Sindri creates it at run start, removes it when the run resolves, and keeps it while parked. A skill that makes its own worktrees (specToProvenPR) makes them from there.
- **`$AW_DIR`:** exactly `$AW_STATE_DIR/<slug>/` of the run's repo, computed by the host (D8), so a skill inside `flow/<instance>` can't land in a second folder. Two runs on the same repo share it; the result file is per launch, and the skills' own folders are timestamped.
- **Push (D7):** `pushes: true` is a request. The block gets the credential helper only if the approval preview showed "build (specToProvenPR) can push to <repo>" and the owner approved. Then its launch keeps the pre-push hook off and installs the helper; every token it hands out is the repo's 1 h agent App token, minted per call, and the ledger records the grant on the launch (`launch.started` carries `push: true`). Otherwise the block runs scrubbed, and a push fails the way it does today (`SND-LAUNCH-023`). A pushing block pins its SKILL.md sha: an edit to that SKILL.md drift-parks the run like a `block.yaml` change.
- **Agent and model:** the profile's `workflow.agent.target`, or the state's `agent:` (RCP-AGENT-001). The seed's form comes from the target's launcher entry.

### 3.5 The recipe rules a skill state follows

- A skill state has `timeout: { after: <at least the block's maxDuration>, then: park }`: a new rule, RCP-SKILL-001, so the manifest's `maxDuration` is never a number nothing enforces (R17). `idea-to-pr`'s `build` state: `after: 48h`.
- Only `exit`, `idle` and `lost` failures route to the recipe's desk; a launch the launcher couldn't start keeps today's `unavailable` rule (two reruns, then park), so a down launcher doesn't turn into three "try again" clicks.
- Loops on skill states take a `cap` and no `noProgress`: skills write outside the worktree, so `emptyDiff` would fire every round (R3).
- A recipe with a `pushes: true` block is owner-only to start: `workflow start`, the item router and the API all refuse an agent session with `SND-AUTH-002` (R9).

## 4. The flow format and the compiler (release 2; D4, D5, D10)

Release 2 starts after the owner has run `idea-to-pr` and one more hand-written chain and says the long form hurts (§8.3). This section fixes the format so §6's canvas and its goldens can start against it; nothing here ships in release 1.

### 4.1 The file

`flows/<name>.yaml` in the toolkit (shipped flows) or in a repo's `.agentic-workflow/flows/` (the owner's):

```yaml
# flows/idea-to-pr.yaml: the same chain as release 1's hand-written recipe (§5.1)
schema: 1
id: flow.idea-to-pr
version: 1
displayName: Idea to proven PR
why: Shape an idea into a plan, review it, and build it into proven PRs.
inputs:
  idea: { type: text, label: What do you want built? }
name: "{input.idea}"
blocks:
  shape:
    use: office-hours
    with: { idea: { ref: input.idea } }
  critique:
    use: autoplan
    with: { plan: { ref: shape.featureDir }, mode: { value: mvp } }
    next: build                                   # pass; written out because the next block in file order is revise
    on:
      needsWork: { to: revise, loop: { name: rework, cap: 2 } }
      blocked: stop
  revise:
    use: office-hours-revise
    with: { plan: { ref: shape.featureDir }, review: { ref: critique.report } }
    next: critique
  build:
    use: spec-to-proven-pr
    with: { spec: { ref: shape.featureDir } }
    on: { blocked: stop }
```

Rules:
- `use:` is the block id from `block.yaml` (no name mapping).
- A block's **success** outcome goes to `next:`: a block id, `end` (resolved) or `stop` (unresolved). Left out, it's the next block in file order, or `end` for the last. The validator refuses a left-out `next` whose default target is also reached by an explicit edge (`critique` would fall into `revise`), so the file says it.
- **Every other outcome needs an `on` entry** (`<block>`, `stop`, or `{ to, loop }`). An unhandled non-success outcome is a finding: `flows/idea-to-pr.yaml:14 critique.on: outcome 'blocked' is not handled; add 'blocked: stop' or 'blocked: <block>'`. A BLOCKED plan can't reach a pushing block by default.
- `with:` values are `{ ref: input.<key> }`, `{ ref: <block>.<output> }` or `{ value: <literal> }`. A ref is checked at validate time: the block exists, declares that output, the types fit (`path` into `path` or `text`; `text` into `text`), and the producer runs before the reader on every path (RCP-WIRE-001 on the compiled recipe, mapped back to flow names).
- A back-edge names a `loop` with a `cap` (RCP-LOOP-001/002); the compiler gives skill loops no `noProgress`.
- Positions for the canvas live in `<name>.layout.json` beside the file, never in the hash.

### 4.2 What it compiles to

`sindri flow compile <file>` prints the recipe long form; validate, eval, approve and run all go through it. It is exactly the hand-written recipe of §5.1 with three differences: state ids are block ids, every state gets `displayName` = the block id so `workflow status` and needs-you rows never show compiled names (R20), and facts are per use. That last one needs **Step output aliasing**, the one recipe schema change: `produces: [{ fact: fact:shape.featureDir, from: fact:office-hours.featureDir }]` on a Step's transition, the form a child's outputs already use. RCP-WIRE-002 is extended to allow it, the descriptor's own keys stay what the agent writes, and replay goes behind a `Workflow.Patched("step-output-alias")` marker. With it, one block used twice keeps both outputs.

- **Errors point at the flow.** Every compiled state and transition carries its flow source position; findings print against the flow file (`flows/idea-to-pr.yaml:14 critique.with.plan: 'shape.featurDir' is not an output of office-hours (it has: featureDir)`). The spec of F-4 lists the eight messages as golden text: unknown block, unknown output, unknown outcome, type mismatch, back-edge without a loop, unhandled outcome, ambiguous default `next`, unreachable block.
- **Hash and pins:** the recipe hash covers the compiled long form (a block rename changes it; moving a card doesn't). The flow's `version` is the recipe's version (RCP-VERSION-001).

### 4.3 Evals

`sindri flow eval --scaffold <file> --out <dir>` writes one case per outcome path (it calls `recipe eval --scaffold` on the compiled recipe). `idea-to-pr` gets 5: pass, needsWork then pass, needsWork x3 (cap hit, parked), blocked, a failed block then again.

## 5. Trigger, run page and jumping in

### 5.1 Release 1's chain: `workflow.skill.idea-to-pr`

A shipped recipe in `recipes/sindri/` (trimmed: labels and sentences left out):

```yaml
id: workflow.skill.idea-to-pr
version: 1
inputs: [{ key: fact:idea, type: text, kind: text }]
initial: shape
states:
  shape:    { use: step.skill.office-hours,        with: { idea: { fact: fact:idea } },                       timeout: { after: 3h,  then: park } }
  critique: { use: step.skill.autoplan,            with: { plan: { fact: fact:office-hours.featureDir }, mode: mvp }, timeout: { after: 3h,  then: park } }
  revise:   { use: step.skill.office-hours-revise, loop: rework,
              with: { plan: { fact: fact:office-hours.featureDir }, review: { fact: fact:autoplan.report } }, timeout: { after: 2h,  then: park } }
  build:    { use: step.skill.spec-to-proven-pr,   with: { spec: { fact: fact:office-hours.featureDir } },    timeout: { after: 48h, then: park } }
  shape-failed:    { use: step.skill.failed-desk, loop: retry-shape, with: { block: shape } }   # one per skill state
  # ... critique-failed, revise-failed, build-failed
  done:     { outcome: resolved }
  stopped:  { outcome: unresolved }
loops: { rework: { cap: 2 }, retry-shape: { cap: 3 }, ... }
transitions:
  - { from: shape,    to: critique, on: step.completed, produces: [fact:office-hours.featureDir] }
  - { from: critique, to: build,    on: step.completed, when: { outcome: pass },      produces: [fact:autoplan.report, fact:autoplan.featureDir] }
  - { from: critique, to: revise,   on: step.completed, when: { outcome: needsWork }, produces: [fact:autoplan.report, fact:autoplan.featureDir] }
  - { from: critique, to: stopped,  on: step.completed, when: { outcome: blocked } }
  - { from: revise,   to: critique, on: step.completed }
  - { from: build,    to: done,     on: step.completed, when: { outcome: done } }
  - { from: build,    to: stopped,  on: step.completed, when: { outcome: blocked } }
  - { from: shape,        to: shape-failed, on: step.failed }
  - { from: shape-failed, to: shape,   on: step.completed, when: { outcome: again } }
  - { from: shape-failed, to: stopped, on: step.completed, when: { outcome: stop } }
  # ... the same three per skill state
```

`step.skill.failed-desk` is the one new shipped desk block (F-2): outcomes `again` and `stop`, its row shows the failed Step's one-line reason, and Take over sits on the same row. Today's desks are domain ones (`step.build.scope-desk`, `step.evidence.desk`) or the route twin `step.desk.choose`, none of which fits. One desk per skill state, each behind its own retry loop (cap 3), is also what the compiler emits in release 2. Its evals (5 cases, as §4.3) ship with it. Start: the app's Start page lists it with its one input, or `sindri workflow start workflow.skill.idea-to-pr` with the input (owner only, D7).

### 5.2 The run page

A run is a recipe instance, so the instance frame (`Views/Instance/InstanceFrameView`, states band, facts panel, timeline) already works. What ui adds in release 1 (F-3) is a skill state's card: its block's sentence, its state (waiting to start, running, **waiting on you** with the question, done with its outcome, failed), its outputs as clickable paths, loop rounds ("round 2 of 2"), and Take over on a running or waiting one. State is a word on the card, never colour alone, and the waiting row announces once, politely, through the live-region pattern `TakeoverBannerView` uses.

### 5.3 Jumping in

One action in release 1: **Take over** (today's): the instance is `taken-over`, its timer pauses, the session opens in the viewer (Warp). The owner answers the skill's question or works in the session, then **Hand back**:
- If the skill wrote a finished result while taken over, handback completes the Step from it at once (the same read as §3.3 step 3). While the owner still holds it, a finished result raises a nudge on the row: "Shape the idea finished while you were in it. [Hand back]" (R14), so a forgotten handback doesn't hold the timer forever.
- With no finished result, the instance carries on in the same state and the session keeps going; its next turn end is read as usual.

When API P10a's attach lands (Attach without a takeover, the `AttachButton` main action), a waiting skill block's row shows it labelled **Answer**, with Take over in the chevron. That's the same component, not a second button, and it needs nothing from this spec.

### 5.4 What the owner sees (owner states, R15)

| State | Row or card text | Buttons | What clears it |
|---|---|---|---|
| Waiting on you (question) | "idea-to-pr › Shape the idea is waiting on you: <question>" | Take over | A finished result, or the state timeout (park) |
| Waiting on you (no result) | "idea-to-pr › Shape the idea stopped without a result" | Take over | Same |
| Result unreadable (001, 002) | "...'s result couldn't be read: <reason>" | Take over | A good result at the next turn end |
| Finished while taken over | "Shape the idea finished while you were in it" | Hand back | Handback |
| Block failed (003-005, exit, lost) | "Build failed: <one line>" (the desk) | Try again, Stop, Take over | A desk answer |
| Loop cap hit | "Rework hit its cap of 2: <autoplan's summary>" (the park) | Run once more, Stop, Take over | An answer |
| Blocked | "autoplan blocked the plan: <summary>" with the report linked, on the run's resolved-unresolved notice | Open report | Dismiss |
| Timeout | "Build ran past 48 h" (the park) | Resume, Stop | An answer |
| Block changed | "autoplan's block changed. Approve idea-to-pr again to continue" | Approve | Re-approval |
| Unapproved recipe on Start | The approval preview opens, Approve then starts it (one click) | Approve and run, Cancel | - |
| Run in progress | the instance frame's existing Cancel | Cancel | - |

Every row names the run (`idea-to-pr › ...`), so two runs on one repo never share a row.

## 6. The canvas (ui; D9, release 2: F-5)

Release 2. The canvas edits one flow's server-side draft (§7, F-5 routes) and draws the flow file (§4.1) as cards and wires. It holds no rules of its own: wire legality comes from the candidates call, findings from the server, and the file stays the source of truth. A promoted draft is a normal flow version, approved and run like a hand-written one.

### 6.1 Where it sits

- **Top bar:** a **Flows** page beside Workflows and Recipes, chord `G F` (free today; `ShellViewModel.Chords.cs` `SecondKey`). The Flows page lists flows (§5.1's list with **Run**) plus **New flow**.
- **A flow's page** has three tabs: **Canvas** (this section), **Runs** (its runs, each opening the run page of §5.2), and **File** (the flow YAML of the open draft, read-only, with each finding's line marked). The File tab is how the owner checks that the canvas wrote what they meant.
- **Opening a flow with an open draft** asks once: "You have a draft from 14:02 (12 edits). Resume or discard?" Discard asks again before it deletes. With no draft, the canvas shows the current version read-only with an **Edit** button. The first edit creates the draft (`POST .../draft`).

```
┌ Flows › Idea to proven PR ─────── Canvas | Runs | File ──────────────────────────────┐
│ Palette        │                    stage (ZoomBorder, 22 px dotted grid)        │ Inspector     │
│ ─────────────  │   ┌ Inputs ┐                                                    │ critique      │
│ ⌕ filter       │   │ idea ●─┼──┐   ┌ 1 shape ─────────┐                          │ autoplan      │
│ officeHours    │   └────────┘  └──▶● idea  featureDir ●─┐                        │ Reviews a pl… │
│ autoplan       │                   │ done ◆            │ │                        │ ─ Inputs ──── │
│ specToProven…  │                   └───────┬──────────┘ │                        │ plan ← shape… │
│ bugFixOrch…    │                           ┆ next       │                        │ mode  [mvp ▾] │
│ review         │   ┌ 2 critique ─────────┐ ▼            │                        │ ─ Outcomes ── │
│ system-design  │   │ plan ●◀──────────────┼────────────┘                        │ needsWork →   │
│                │   │        report ●     │                                     │  revise (≤2)  │
│ (only skills   │   │ pass ◆ needsWork ◆ blocked ◆ │                              │ blocked → stop│
│  with a        │   └──┬───────┬───────────┬───┘                                 │ pass → build  │
│  block.yaml)   │      ┆next   │ rework ≤2 └──▶ ■ stop                            │ [Test block]  │
├────────────────┴──────────────────────────────────────────────────────────────┴───────────────┤
│ Draft rev 14 · saved · 0 findings                    Undo  Redo   [Discard]  [Save as v3]        │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 6.2 How the file maps to the canvas

| In the file (§4.1) | On the canvas |
|---|---|
| A block (`blocks.<id>`) | A card (the run canvas's card shape, `RunCardView`): header = file-order number, block id and skill name; input ports on the left, one per declared input, in `block.yaml` order; output ports on the right; outcome ports along the bottom, one per outcome (`done` for a skill with no `outcomes`) |
| `inputs` of the flow | One **Inputs** card, fixed at the top left: an output port per flow input. Add, rename and type an input in the inspector |
| `with: { x: { ref: a.out } }` | A **data wire**, output port to input port: solid, Wire colour (`GraphEdgesControl`'s default) |
| `on: { o: { to: b } }` / `on: { o: stop }` | An **outcome wire** from the outcome port to the target card's header, or to the **Stop** terminal: solid, in the outcome's tone |
| `loop: { name, cap }` on a back-edge | The outcome wire plus a pill on it: "rework ≤ 2". A wire into an earlier card is a back-edge and can't exist without its loop (§6.3) |
| `next:` (the success outcome's target, §4.1) | A **next wire** from the success outcome port. When `next:` is left out (the next block in file order), it's dashed and dimmed, labelled "next"; wiring it somewhere writes `next:` and makes it solid, and deleting that wire brings the dashed default back. Every other outcome has no wire until it's wired: an unwired non-success outcome is a finding on its port (§4.1) |
| File order | The number in the card header. Moving a card on the canvas never changes the order. **Move up / Move down** in the inspector, or Alt+Shift+↑/↓, does |
| `<name>.layout.json` | Card positions. Missing positions (a flow written by hand, a new card from the assistant) get the run canvas's deterministic layout (`RunCanvasLayout`: main path down the centre in file order, loop targets beside their source) and are saved on the next edit |

Port glyphs carry the type (`text`, `path`, `pr`, `issue`, `packet`) as a letter plus the shape. Colour is never the only signal, and every port has an automation name ("plan, path input, wired from shape.featureDir").

### 6.3 Wiring

**Mouse.** Press on an output port, which calls `GET .../draft/candidates?rev=&port=` once. While the wire follows the pointer, the legal input ports are lit and every other port is dimmed. Hovering a dimmed port shows the server's reason ("needs a path; report is text"). Dropping on a lit port sends the edit. Dropping anywhere else, or Esc, cancels without a request. Dragging from an **outcome** port works the same way, with cards and the Stop terminal as targets. A target earlier in file order is a back-edge, so the drop opens an inline form on the wire (loop name, prefilled from the outcome; cap, default 2, 1 to 9). Esc there cancels the wire. A back-edge is never sent without its loop.

**Keyboard** (a must, not a nice-to-have). The stage is one tab stop. Inside it:
- Tab / Shift+Tab move between cards in file order. Arrow keys move to the nearest card in that direction.
- **W** on a card lists its unwired output and outcome ports. Pick one with ↑/↓ and Enter, and the second list is the candidates, with each disallowed row disabled and its reason read out. Enter wires it. A back-edge opens the same loop form.
- **Enter** puts focus in the inspector. **Delete** removes the selected card or wire. Removing a card that has wires asks first and names them.
- **Alt+arrows** move a card one grid step (22 px), and the position is sent on key release. Alt+Shift+↑/↓ changes file order.
- Ctrl/Cmd+Z and Ctrl/Cmd+Shift+Z are undo and redo. `+`, `-` and `0` zoom (ZoomBorder's keys today), and **F** fits the flow.

**Screen readers.** Wires aren't focusable, so the inspector's lists are the readable view of them. A card's automation name reads like the file: "Block 2, critique, autoplan. plan from shape.featureDir. needsWork goes to revise, loop rework, cap 2. blocked stops. pass goes to the next block, build." Every list in §6.3's keyboard path is a real list control with names, so the same steps work with a screen reader.

**Palette.** It lists `GET /sindri/skills`: only skills with a `block.yaml` (§3.1), filterable by name and sentence, with a skill that failed to load shown greyed with its doctor reason. Drag one onto the stage, or Enter on it to place it under the selected card, and it becomes a new block with an id made from the skill name (`autoplan`, then `autoplan2`). Rename it in the inspector, and refs to it follow because the rename is one op the server applies to the whole draft.

**Inspector.** It shows the selected card's sentence and inputs. Each input shows its wire ("← shape.featureDir") or a literal field, picked by type: a text box for `text`, a folder picker for `path`, a PR box for `pr`, and a dropdown when `block.yaml` has `values`. Below that are its outcome wires and **Test block** (§6.5).

### 6.4 Drafts in the app (the client half of D9)

- **Every edit is an op.** The edit shows at once, then `PATCH .../draft {rev, ops[]}` goes out. Ops are small and named: `addBlock`, `removeBlock`, `renameBlock`, `setRef`, `clearRef`, `setLiteral`, `setOutcome`, `clearOutcome`, `setLoop`, `move` (position), `reorder`, `setInput`. Card drags send one `move` when you let go, never during the drag.
- **Refused or 409.** A refused op rolls back that one edit, and the reason shows on the card or wire for 6 s and in the status bar. A 409 (someone else's rev landed first) re-pulls the draft, re-applies the ops not yet sent on top, and only then sends them.
- **Undo** sends the inverse ops as a new edit (removing a wire you added re-adds it, `move` restores the old position). The stack holds ops, never revisions, so an assistant edit between yours survives your undo. The stack lives with the page: leaving the flow clears it, but the draft stays.
- **`draft.changed`** from someone else (the assistant, another window) applies the new revision live. What changed is outlined for 2 s, and the status bar says "Assistant added review after critique" (from the event's op list). An edit you're making (a wire in the air, the loop form open) is kept and re-checked against the new revision when you finish it.
- **Findings** come back on every PATCH response for the new revision. That's one request instead of PATCH-then-validate, so it's an ask of the §7 route (below). Each finding attaches to its card, port or wire and is listed in the status bar. **Save as vN** (`POST .../draft/promote`) is enabled at 0 findings and says what blocks it otherwise ("2 findings: build.spec is not wired").
- **Status bar:** "Draft rev 14 · saving… / saved · N findings", then Undo, Redo, Discard and Save as vN.

### 6.5 Test this block

**Test block** in the inspector calls `POST .../draft/test` for the selected block at the current rev.
- **Inputs:** a wired input uses the upstream block's last test output in this draft when it's not stale, else it asks for a value in the same field the literal would use. Nothing runs until every required input has one.
- **It's a real session:** the button says "Runs autoplan as a real agent session (up to 3h)", using the `maxDuration` from `block.yaml`. While it runs the card shows running, **Take over** (§5.3), the "waiting on you" state with the skill's question, and the result: the outcome chip, the outputs as clickable paths, and the cost when the session reports one.
- **Stale:** a result is keyed to (draft, rev). Any later edit to that block, or to anything wired into it, marks the result **stale** on the card, and a stale result is never fed into another block's test.

### 6.6 What's reused and what's new

| Reused (`src/Sindri/Aw.Sindri.Ui/Views`) | For |
|---|---|
| `Controls/ZoomBorder.cs` | The stage: pan, zoom at the cursor, pinch, dotted grid, reduced motion |
| `Controls/GraphEdgesControl.cs` | Every wire in one render pass, the dashed next wires, the hit stroke, dimming, highlight around the selected card. Bezier routes are a new `Route` shape |
| `Controls/MiniMapControl.cs` | The overview, bottom right |
| `Run/RunCardView.cs`, `ViewModels/Run/RunCanvasLayout.cs` | The card shape and its automation, and the layout for cards with no position |

New: the port controls and the in-air wire, the palette, the inspector, the draft client (op queue, rollback, replay, undo stack), the candidates overlay, the loop form and the File tab. No new package. Everything stays in compiled bindings and passes `src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh` with zero IL2xxx/IL3xxx warnings.

### 6.7 Proof and tests

- **Unit (Ui.Tests, headless):** the op inverse table (every op's undo, round-tripped), 409 replay order, rollback of one refused op among three in flight, the next-wire rule, the unwired-non-success-outcome finding, the back-edge-needs-a-loop rule, stale marking, and the automation names against goldens.
- **API goldens first:** the draft, candidates and PATCH-with-findings responses as fake-route goldens, so the canvas is built and tested before F-5's server half lands. That's the same day-one start as F-4 (§8.2).
- **Proof in the running app (F-5's row in §8.1):** build `idea-to-pr` from an empty flow **by keyboard only**, driven through `aw-ui-driver`: inputs, four blocks, data wires, the rework loop, then Save as v1. Then one more step with the mouse. Then an assistant edit arrives mid-session and shows live. Then undo steps over the owner's edit and leaves the assistant's. Each step gets a screenshot and its automation tree.

### 6.8 Asks of engine's sections (ui's picks; all four accepted by engine, folded into §4.1 and §7)

1. **PATCH returns the findings** for the new rev (§6.4), so the canvas doesn't send a validate after every edit. `POST /sindri/flows/validate` stays for files.
2. **Literal values in `with:`** (`mode: { value: mvp }`). §4.1 shows refs only, but `autoplan`'s `mode` and a fixed `idea` in a test both need one. If literals stay out of release 1, the inspector shows unwired optional inputs as "not set", and an unwired required input is a finding.
3. **`draft.changed` carries the op list** (or a one-line summary per op), so the status bar can say what the assistant did without diffing two revisions in the app.
4. **`renameBlock` is one server op** that rewrites every ref to the block, so a rename never leaves dangling refs, even for a moment.


## 7. API and CLI

All new routes are on the existing loopback control plane and follow its error and auth rules.

**Release 1:**

| Route | What it does | Caller |
|---|---|---|
| `GET /sindri/skills` | Loaded skill blocks: id, skill, descriptor id and hash, sentence, inputs, outputs, outcomes, `pushes`, SKILL.md sha, the path it was loaded from, and the doctor reason for any that didn't load | any |
| `POST /sindri/launches/{launch}/push-token` | The push grant's helper (D7): a fresh 1 h agent App token for the launch's repo, only while its Step runs and only for a launch started with `push: true` | the launch's own session (by its launch secret) |

CLI: `sindri skills [--json]` (exit 0 ok, 1 not running). Listed in `Usage.cs`, the help goldens and aot-smoke's expected text. Everything else uses today's verbs: `sindri workflow start|status`, `takeover`, `handback`, `recipe approve`.

Ledger: `launch.started` carries `skill`, the SKILL.md sha and `push`; the `workflow.transition` of a skill Step's completion carries the result file's sha256; new `launch.waiting` (the turn ended waiting on the owner: `launchId`, `reason` = `question` | `noResult` | `unreadable`, the question text) feeds the needs-you row. The API goldens for these are integration-lane and run in F-1's and F-2's gates.

**Release 2** (with the format and the canvas):

| Route | What it does | Caller |
|---|---|---|
| `GET /sindri/flows`, `GET /sindri/flows/{id}` | Flows with their approval state, source, layout and the compiled recipe's hash | any |
| `POST /sindri/flows/validate` | Body: a flow file. Findings against flow positions (§4.2). Writes nothing | any |
| `POST /sindri/flows/{id}/runs` | Body: inputs. Starts a run | owner; a session is refused `SND-AUTH-002` |
| `POST /sindri/flows/{id}/draft`, `PATCH .../draft` (`{rev, ops[]}`, the response carries the new rev's findings), `GET .../draft/candidates?rev=&port=`, `POST .../draft/test`, `POST .../draft/promote` | The canvas's server-side draft (§6): PATCHes not in the ledger; `draft.changed` on the event stream carries the op list; `renameBlock` is one op that rewrites every ref; promote = a new flow version (ledger `flow.saved`) | owner, the assistant; promote is the owner's |

CLI: `sindri flow list|show <id>`; `sindri flow validate|compile|eval <file> [--json]` (in process, no serve, like `recipe validate`; exit 0 pass, 1 findings, 2 usage); `sindri flow approve <file>` (registers and approves the compiled recipe; the preview lists each block, its pushes and its SKILL.md sha; owner only); `sindri flow run <id> --input k=v | --input-file k=<path> [--follow]` (relative paths resolve against the cwd; exit 0 started, 1 not running, 2 refused); `sindri skill run <id> --input k=v` (one block as a one-state trial run, the CLI half of the canvas's Test block).

## 8. Phases and the parallel plan

### 8.1 Phases

**Release 1** (one trigger, a chain, jump in):

| Phase | What ships | Lane | Needs | Proof in the running app |
|---|---|---|---|---|
| **S-0** Spike (day one, R18) | No product code. Run autoplan and officeHours in a launcher session with `AW_RESULT_SKILL` / `AW_RESULT_FILE` set by hand; check each writes the per-launch file, hands a question back as `waiting` instead of AskUserQuestion, and what the hooks report meanwhile. Notes into this spec | engine | - | The two result files and the hook markers, pasted into the spec's §3.3 as fixtures |
| **F-1** Skill blocks | `block.yaml` for the six blocks + `check-skill-blocks.mjs`; the output-contract change (`AW_RESULT_SKILL`, `AW_RESULT_FILE`, `waiting`) in the five skills; officeHours' revise mode; manifest loader and `step.skill.*` descriptors; result-file completion and SND-SKILL-001..005; `requires.yaml` launch check; `AW_DIR` in the sandbox plan and policy; `GET /sindri/skills`, `sindri skills`; **docs**: `docs/csharp/sindri-skills.md` (block.yaml author guide, the result-file table, the error codes), `.agents/rules/skills.md` (the new file), `AGENTS.md` lines (R8) | engine | S-0 | A one-state hand-written recipe running `step.skill.autoplan` on a real plan folder completes with the right verdict and facts, with no `workflow complete` call from the agent |
| **F-2** Waiting on you, and the chain | The idle sweep skips skill launches; `launch.waiting` and its needs-you rows; the finished-while-taken-over nudge; handback completes from the result file; RCP-SKILL-001; owner-only start for pushing recipes; the push grant (helper, token route, preview line); `step.skill.failed-desk`; the shipped `workflow.skill.idea-to-pr` with its evals; docs: the owner-states table in `sindri-skills.md`, the push grant in `sindri-launcher.md` | engine | F-1 | Start `idea-to-pr` on a scratch repo from the app; officeHours asks its first question; the row shows it; Take over opens Warp; answer, hand back; the chain runs shape -> critique -> (rework once) -> build to a pushed PR on the scratch repo |
| **F-3** Skill cards | The skill state's card on the instance frame (§5.2), the rows of §5.4, Take over and the nudge on them, the approve-and-run preview from Start | ui | F-1's `/sindri/skills` golden, F-2's event goldens | The F-2 run watched and jumped into from the app only |

**Release 2** (starts on the owner's word after using release 1; §8.3):

| Phase | What ships | Lane | Needs | Proof |
|---|---|---|---|---|
| **F-4** Flow format | Flow schema, compiler, Step output aliasing (Patched), source-position findings and their golden messages, `flow validate|compile|eval|approve|run`, `skill run`, `/sindri/flows*`, `flows/idea-to-pr.yaml` replacing the hand-written recipe; docs `sindri-flows.md` | engine | release 1 | `sindri flow run idea-to-pr` runs the same chain as F-2 |
| **F-5** Canvas | §6, the draft routes, candidates, test one block | ui + api | F-4's format frozen | Build `idea-to-pr` on the canvas from an empty flow by keyboard, run it |
| **F-6** Linear trigger | "Run flow on this issue" | context | #2077 merged, F-4 | One issue starts one run, linked both ways |
| Later | Parallel blocks (children), the n8n palette (cron, Slack, code, logic, CEL), a native graph runtime if §8.3 says so | - | - | - |

### 8.2 Parallel plan (always-busy)

- **Engine, day one:** the lead runs S-0 (a few hours, no builder). In parallel, builder A writes F-1's red commit: the `/sindri/skills` golden, the loader tests over two fixture manifests in `tests/Aw.Sindri.Testing`, and the result-file table as one test per row. After S-0, builder A goes green on F-1's C# half and builder B does the skill-side half (the five `block.yaml` files, the output-contract and Result block edits, officeHours revise, `check-skill-blocks.mjs`, docs), which touches no C#.
- **After F-1's descriptors land:** builder A on F-2's server half; builder B on `idea-to-pr` and its evals (recipe only).
- **ui, day one:** F-3's cards and rows against fake routes and the goldens from F-1's and F-2's red commits. Then §6's view models against the release 2 goldens, which engine writes when F-4 starts.
- **context:** F-6 once #2077 and F-4 are in; nothing before.
- Red first in every phase: each PR opens with its failing tests and goldens.

### 8.3 When to build the format, and when to stop compiling

Release 2 starts when the owner has run `idea-to-pr` and written or asked for a second chain by hand, and says the long form is the slow part. If they're happy writing recipes, F-4 waits and the canvas edits recipes instead (a smaller §6).

Compiling then stays the plan while every flow the owner writes fits it. Any one of these, met on a real flow, is the signal to write a native-runtime spec: (1) a flow needs two blocks running at once that children can't express cleanly; (2) a compiled flow passes 40 states (twice the median of today's big recipes, `flow-internal.md` §1) and its findings stop reading well against the flow file; (3) the owner wants an edit to apply to a running run, which pinning forbids by design.

## 9. Owner questions

1. **Release 1 is skill blocks plus one ready-made chain (idea -> officeHours -> autoplan, needs work loops back at most twice -> specToProvenPR), started from the Start page, with Take over to jump in. The flow file and the canvas come after you've used it. OK?** My pick: yes. The chain runs sooner, and the format gets built from what hurts in practice.
2. **May a block push and open PRs?** My pick: yes, per block, only when you approve the chain and the preview says "build (specToProvenPR) can push to <repo>". It uses the 1 h agent token, fetched fresh on each push. Without it, specToProvenPR can't finish a PR inside a chain.
3. **When you edit a skill's SKILL.md, should chains already running stop and wait for you?** My pick: only for blocks that push. Any other skill's prompt edit is recorded on the run but stops nothing; a `block.yaml` change (inputs, outputs) always stops them until you approve again.
4. **Inside a chain, skills ask their questions as plain text and stop, instead of the pop-up picker, so Sindri can see they're waiting and show you the question. OK?** My pick: yes. A pending picker looks like a busy session, so nothing would tell you it's waiting.
5. **Which chain ships first?** My pick: idea -> officeHours -> autoplan -> specToProvenPR. The other one I'd consider is bug -> bugFixOrchestrator -> review.
