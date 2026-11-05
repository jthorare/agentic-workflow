# Sindri skill flows: chain repo skills from one trigger, and jump into any running one

Status: DRAFT (2026-11-05). Engine wrote §0-§5 and §7-§9; ui wrote §6. Autoplan round 1 (NEEDS_WORK, R1-R21) is folded in: release 1 is re-cut to skill blocks, one hand-written recipe and "waiting on you" on the existing takeover; the flow format, its compiler and the canvas move to release 2. Round 2 (NEEDS_WORK, S1-S32) is folded into §0-§5 and §7-§9 (legal camelCase fact keys, push through SB-7's agent-app mode with per-repo approval, `skill run` in release 1, F-2 split in two); ui folds §6. Round 3 (product PASS, design PASS, security PASS, devex NEEDS_WORK, arch NEEDS_WORK: six HIGHs on how existing machinery really works) is folded too. No ready until 3 /autoplan rounds, meta's UX check and the owner's read. Lane: engine (lead) + ui. Related: the engine spec (2026-10-10), the session launcher (2026-10-15), the sandbox (2026-10-18), the Linear hub (#2077, context).

Research behind this note (file:line refs): `~/repos/aw-wt/briefs-engine/flow/flow-internal.md` (Sindri today), `flow-external.md` (n8n, Node-RED, Windmill, Kestra; Avalonia node editors; reference languages), `flow-decisions.md` (canvas and draft rules agreed with ui). Reviews: `~/.agentic-workflow/joi-fairshare-agentic-workflow/plans/skill-flows-r1/` and `skill-flows-r2/`. Paths below are relative to the repo root unless they start with `~` or `$`.

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

D1. **A skill is a block made from a small manifest, not C#.** `skills/<name>/block.yaml` lists one or more blocks (officeHours ships two: start from an idea, and revise a plan from a review). Each declares an `id`, typed `inputs` (`text` or `path` in release 1), `outputs` mapped from the result JSON, `outcomes` with one `success` outcome, `pushes`, `maxDuration`. Sindri loads them at serve start as descriptors `step.skill.<id>` (runtime agent, so no new block kind). An output's fact key is `fact:<block id in camelCase><Output, first letter capitalised>`, e.g. `fact:officeHoursFeatureDir`, because `FactKey` allows only `^fact:[a-z][A-Za-z0-9]*` (`FactKey.cs:23`; round 2 S1). The descriptor's `Version` is a number derived from the canonical hash of the block's whole manifest entry (`args`, `from`, `values` included), so there's no hand-kept version field and no edit slips past the pin (S16). The launch records the SKILL.md sha. A pushing block pins the hash of its whole skill folder, not just SKILL.md (owner Q3).
D2. **The block's session** is an agent Step through the existing launcher, seeded with ONLY the skill's invocation in the target agent's form (`/officeHours ...` for Claude Code, `$officeHours ...` for Codex), not today's Step brief (`AgentStepBrief.Render`, `AgentStepBrief.cs:20-39`, which tells the agent to write fact files and call `sindri workflow complete`; a skill given both would try to satisfy both). F-1 adds a skill-descriptor branch in `AgentLaunches.StepArgsFor` with a golden per target (round 3, arch N4). It finishes when the skill writes its result to a per-launch file Sindri names; Sindri reads it at each turn end and completes the Step (§3.3). The agent never has to know it runs under Sindri.
D3. **A skill block is never idle-failed.** Its turn ending without a finished result means it's **waiting on you**: a needs-you row with the skill's question (from the result's new `waiting` key) or "stopped without a result", and **Take over** on the row. The idle sweep and the stalled check (`StalledAgentRuns.cs:25`, which today flags a quiet launch after 120 s) both skip skill-block launches (S13). The state timeout is the only bound. No `asking` signal, no timer pause, no new Answer route in release 1 (round 1 R2, R14).
D4. **Release 1 wires blocks in a hand-written recipe.** The first chain, `workflow.skill.idea-to-pr`, is a shipped recipe in today's long form using `step.skill.*` Steps, so the validator, evals, approval, pins, Temporal and the ledger apply unchanged. The flow format and its compiler (§4) are release 2, built once the owner has run the hand-written chain and a second one (round 1 R10). The compiler needs one recipe change, Step output aliasing (R1), which is release 2 work too.
D5. **Routing is explicit.** In the recipe that's already true. In the release 2 flow format, a block's `success` outcome goes to its `next` (default: the next block in file order), and every other outcome must have an `on` entry; there is no hidden skip rule (R4, R5). Loops on skill blocks declare `noProgress: [sameFailure]`. Leaving `noProgress` out means all three signals (`LoopSpec.cs:12`), and `emptyDiff` would park the rework loop after its first round, since the revise block edits the plan folder under `$AW_DIR`, not the worktree. An empty list is refused (`RCP-LOOP-003`) (R3, S6).
D6. **Trigger in release 1:** the existing starts (the app's Start page, `sindri workflow start`), with the recipe's input form. `sindri flow run` and the Flows page come with the format in release 2; Linear after #2077.
D7. **Skills that push or open PRs** (specToProvenPR, bugFixOrchestrator, shipRelease) run scrubbed today. A block with `pushes: true` runs its Step with SB-7's `sandbox.credentials.github: agent-app`. That mode is profile-wide today, resolved in two places that must agree (`PlanContext.cs:35`, `SandboxLaunchStages.cs:138`), and `StepPolicy` only tightens (`StepPolicy.cs:10, 20-26`). So F-2b adds one nullable `Github` ask to `StepPolicy`: the one loosening, gated by the approval preview. One resolver serves both call sites, and the ask goes into the plan hash (`PlanHasher.cs:37` already hashes `github`). The sandbox spec and `sindri-sandbox.md` are amended (round 3, arch N1) (`docs/csharp/sindri-sandbox.md` "GitHub (agent-app) (SB-7)"): `GitHubAppTokenSource` mints the repo's agent App token into a 0600 file in the session home, the `gh` and `git-credential-sindri` shims read it, serve renews it every 45 min, and `Release` stops the renewal when the Step ends, parks, or is cancelled. Release doesn't revoke: the last token expires within 60 min (`sindri-sandbox.md:366`), and `launch.started` and the release row carry the repo and the last-minted time. The pre-push hook stays on and allows only `refs/heads/<run namespace>` (`sindri/<instance>/<run>`), never a delete. That limit covers `git push` only: `gh` or the REST API with the same token can act on any branch the App's permissions allow (same doc). So the preview says exactly that and lists the App's permissions, and the skill-launch `gh` shim refuses `gh pr merge` and `gh api -X DELETE`, as an honest-mistake guard (round 3, security). Serve does the renewing, so after a serve restart a running pushing Step re-mints at startup. If that fails, the state parks with "build lost its push token when Sindri restarted. Resume to get a new one" instead of letting the skill hit a bare 401. No new route, no new secret (S3). The approval is keyed per (recipe, version, hash, repo): the first start on a repo opens the preview naming that repo, and every row's [Approve] opens the preview and never approves in place (S4). Re-entering a pushing state (a desk's "Try again", a rewind) reuses the approved grant; it never widens it (S30). Starting any recipe with a pushing block is owner-only, wherever the start comes from (R9): it starts only from a human caller (the app, the CLI), never from an agent session, a parent's child step, the item router or a trigger. The Linear trigger (F-6) must bring its own approval. Owner Q2.
D8. **Sandbox and policy:** the host computes the repo's slug once and passes `AW_DIR=$AW_STATE_DIR/<slug>/`; the sandbox plan mounts exactly that folder writable and the policy adds it to the writable paths (R12). `skills/_shared/repo-slug.sh` overwrites `AW_DIR` today (line 13); F-1 makes it keep a preset `AW_DIR`, with a bash test, and keeps `skill-evals.sh`'s rewrite working (S17). The wall limit (round 3, arch N2): today every launch gets `StepTimeout: null` (`SandboxLaunchStages.cs:59`), so the 4 h step default applies, capped at `workflow.steps.longMax` (default `2h`, `LineProfile.cs:49`; `LimitsResolver.cs:63-67`). Every launch dies at about 2 h as SND-LAUNCH-019, which would fail an officeHours question left over lunch. F-1 passes the state's timeout on `LaunchRequest` into `SandboxRequest.StepTimeout`, and `step.skill.*` launches skip the `longMax` cap: the state timeout is their bound (RCP-SKILL-001). A test pins that `timeout.after: 48h` plans `Wall = 48h + grace`. Before launch Sindri checks the skill's `requires.yaml` against the agent home and the sandbox PATH and refuses with the missing names (R16).
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
    args: ["{plan}", { flag: --mode, input: mode }]   # a fragment: an empty optional drops the flag and its value
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
    args: ["{idea}"]
    outputs: { featureDir: { type: path, from: feature_dir } }   # F-1 adds feature_dir to officeHours' Result block (it writes plan_dir today, SKILL.md:657)
    outcomes: { success: done }
    pushes: false
    maxDuration: 3h
  - id: office-hours-revise
    sentence: Revises a plan folder in place from a review report.
    inputs:
      plan:   { type: path, label: Plan folder, required: true }
      review: { type: path, label: Review report, required: true }
    args: [{ flag: --revise, input: plan }, { flag: --review, input: review }]
    outputs: {}
    outcomes: { success: done }
    pushes: false
    maxDuration: 2h
```

- **Input types:** `text` and `path` (an absolute path the session can read). The type picks the form field (text box, folder picker). `pr`, `issue` and `packet` wait until a block needs them (C6). Every input is also written as `<key>.json` into the run directory (the launcher does this for declared inputs, `sindri-launcher.md:256`).
- **`args` grammar** (S19): a list, joined with single spaces in order. A string item is literal text with `{input}` slots. A `{ flag, input }` item becomes `<flag> <value>`, and is dropped whole when that optional input is empty. Each value is placed as one token, and one with a space is double-quoted (a golden pins a path with a space). The seed is typed into the agent's prompt, never run by a shell, so `$(...)` is just text.
- **Long or risky text:** a `text` value of one line up to 500 characters, not starting with `/` or `$`, is put into the seed as is. Anything longer, with a newline, or with that leading character (it would read as a command) is replaced in the seed by `(the text in <run dir>/<key>.json)`, so the seed is always one line and starts with the skill's own invocation (R13, S19).
- **Outputs** read the result JSON: a top-level key (`feature_dir`), `verdict`, or one artifact by file name (`artifacts[<name>]`: `<name>` is a file name, matched against the one path in `artifacts` ending in `/<name>`). Output types are `text` and `path`. A path output must be absolute, exist, and hold no newline, or it's SND-SKILL-004 (S19). A **path fact holds the path, not the file**: a later block may edit the folder in place (the revise block relies on this), and the file's sha256 at completion is recorded on the run.
- **Fact keys** are fixed per block: `fact:<block id in camelCase><Output capitalised>`, so `fact:autoplanReport`, `fact:autoplanFeatureDir`, `fact:officeHoursFeatureDir` (S1). Two Steps of the same block in one recipe write the same key, the later one winning (today's rule for a fact written on two transitions). In release 1, a recipe that needs both keeps them apart by using different blocks, and the Start page's description says so when a shipped chain repeats one. Renaming per use is release 2's aliasing (§4.2).
- **Outcomes:** `from: verdict` with a `map` to camelCase names and the one `success` outcome; with no `from`, the only outcome is `success` (named `done` by convention).
- **The `aw-result` grammar** (S20): each skill with a manifest has one fenced ` ```aw-result ` block in its SKILL.md's Result section, a JSON example of the result it writes. Its top-level keys are the names an output's `from` may use, and its `artifacts` array lists example paths whose file names `artifacts[<name>]` may use.
- **Checks** (`node scripts/check-skill-blocks.mjs`, beside `scripts/check-skill-requires.sh`, in the bash suites): schema; ids unique across all manifests; every `{name}` and fragment `input` in `args` is a declared input, and every required input appears; `success` is one of the mapped outcomes; every output's `from` names a key or artifact file name in the `aw-result` block; every `values` entry is a non-empty string list on a `text` input; derived fact keys are unique across all manifests and at most 64 characters (`FactKey.cs:26`; arch N12); `requires.yaml` may name `bins`, `mcp`, `skills` and `agents`, and SND-SKILL-005 checks all four.

### 3.2 From manifest to descriptor

At serve start Sindri reads every `skills/*/block.yaml` under the skills folder and registers one descriptor per block. The folder is, in order (S8): `--skills <dir>` on the in-process verbs (`recipe validate|eval`); `AW_SINDRI_SKILLS_DIR` (not `AW_SKILLS_DIR`, which `setup.sh:83` already uses for `~/.claude/skills`); the checkout's `skills/` when run inside an agentic-workflow checkout; else the installed toolkit's (`${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills`). Serve resolves it once at start. `sindri skills` asks serve, so it prints the path each loaded block came from and takes no `--skills`. `install-cs.sh`'s `recipe eval --shipped` and CI pass `--skills skills`, so a published binary is checked against the manifests it ships with.

**The author loop** (round 3). Serve loads manifests only at start, so a trial of an edited manifest can't go through the loaded one. `sindri skill run <id> --skills <dir>` sends that folder with the request: serve reads and checks that one block's manifest from it for this trial only, and nothing else changes. `sindri-skills.md` writes it as numbered steps:
1. Edit `skills/<name>/block.yaml` (and the Result block).
2. `node scripts/check-skill-blocks.mjs`.
3. `sindri skill run <id> --skills ./skills --input k=v --follow`. It prints the outcome, each fact and the manifest hash it ran.
4. Repeat 1-3 until it's right.
5. Restart serve to load it for chains, then `sindri recipe approve` any recipe that uses it.

| Descriptor field | From the manifest |
|---|---|
| `Id` | `step.skill.<id>` |
| `Runtime` | agent |
| `Parameters` | one per input, `ParameterType.Text`, `Required` as declared. Values come as `with: {fact: ...}` or a literal; RCP-WIRE-001 already checks fact-valued parameters (`WiringRules.Upstream.cs:94`) |
| `Version` | a number derived from the canonical hash of the manifest entry (D1, S16) |
| `Outputs` | `fact:<idCamel><Output>` per output (D1), `RequiredOn` the success outcome |
| `Outcomes` | the mapped outcomes, or `done` |
| `Capabilities` | `Tools`, `WriteScope`, plus `Effects` when `pushes: true` (it shows in the approval preview) |
| `MaxDuration` | `maxDuration`. An agent Step isn't held to it by the runtime; the recipe's state `timeout` is the real bound, and the check in §3.5 makes the recipe say it |
| `Sentence` | `sentence` (not hashed, like every sentence today) |

A manifest that fails the checks is skipped with a doctor row `skills:block <id> is not loaded: <reason>`, never a failed serve; no skills folder at all is an empty list and one doctor row. A recipe naming a block that isn't loaded fails validation with that reason, and only recipes naming it become unloadable; the rest load as usual (S8). Manifest edits need a serve restart in release 1; `sindri doctor` warns when a loaded manifest's hash differs from the file on disk.

After the restart, what happens to a run already using a changed block (round 3, arch N5). A workflow's approval context is fixed at its start (`RecipeWorkflow.cs:119`), and only recipe effects recheck it (`RecipeWorkflow.Effects.cs:238-239`); `idea-to-pr` has none. So: **a non-pushing block continues on its old pins**, and `sindri doctor` warns that run X uses a block whose manifest changed. **A pushing block is checked by the reader:** before returning a finished result it calls the approval check (`IApprovedRecipes.Check`). On `Drifted` it emits `launch.owner-waiting` with `reason: skillUpdated` and leaves the session alone, and it does not mark that turn end handled, so after re-approval (`sindri recipe approve`, same version) the next poll completes from the same result file (R6). The row says it in the owner's words: "build's skill was updated. Approve idea-to-pr again to continue: [Approve]", and that Approve opens the preview (D7). **What counts as a change** (arch N6): `PinRules.Compatible` renews a pin that moved only in `Version` without a person (`PinRules.cs:136-147`, owner q93). So the descriptor puts `args` and the outcome map into its interface hash, and for `pushes: true` also the skill-folder hash. Those edits then need a re-approval, and nothing a pushing block runs changes silently.

### 3.3 The session and how it finishes

1. **Launch.** The existing `LaunchAgentStep`: worktree, sandbox, policy and scrubbed env as for any agent Step, plus `AW_DIR` (D8), `AW_RESULT=1`, `AW_RESULT_SKILL=<name>`, and `AW_RESULT_FILE=<run dir>/.sindri-out/result-<launch>.json`: a new name per launch, so a loop's second round can never read the first round's file. The run dir is the launch's run area under `$AW_SINDRI_HOME`, never inside the worktree, so a pushing skill's `git add -A` can't commit the result (S18). The `requires.yaml` check runs first (SND-SKILL-005 below).
2. **The skill writes its result.** One change to `skills/_shared/output-contract.md` and to the Result block of each skill with a manifest: when `AW_RESULT_SKILL` equals the skill's own name, write the result to `AW_RESULT_FILE` instead of `$AW_DIR/results/<skill>-<ts>.json`. The name check keeps autoplan's five lens skills, which run in the same session, writing their own default files. And in that mode, **a question goes back in the result, not in a picker**: the skill writes `"waiting": "<the question, one line>"` with the rest of the result as it stands, prints it, and ends its turn, instead of calling AskUserQuestion (a pending picker is invisible to Sindri: the session looks busy forever). When unset, nothing changes, and a bash test pins that. `output-contract.md` adds `waiting` to its key table: set only under `AW_RESULT_SKILL`, and any reader (eval graders, a skill reading another's result) treats a result carrying it as not finished. The new mode is gated, not just written: `check-skill-requires.sh` also checks that every skill with a `block.yaml` has the `AW_RESULT_FILE` and `waiting` wording in its Result block; `skill-evals.sh`'s staged-copy rewrite sets `AW_RESULT_SKILL` and `AW_RESULT_FILE` the way it sets `AW_RESULT` today; and each block gets two eval cases: a finished run writes the file, a run that needs an answer writes `waiting` and opens no picker (round 3, devex).
3. **Sindri reads the file at each turn end, inside the Step's own watch** (round 3, arch N3). A live session can complete only from inside the workflow, and `WatchAsync` already loops per launch every poll interval, heartbeats, and re-attaches after a worker restart (`AgentStepActivities.cs:32-55`). For a skill launch it also compares `TurnEndedAt` with the last turn end it handled (a local; a retried activity re-reads, which is harmless), reads the result file, and then either (a) emits `launch.owner-waiting` and keeps watching, for the waiting and unreadable rows below, or (b) returns a new `AgentSessionState` value for a finished result. The workflow handles that value in the same arm as `Reported`: `Close(measure: true)`, which stops the tmux session, then decides from the result. There's no separate sweep and no new timer. `agent:recovered` (`RecipeWorkflow.AgentEscalation.cs:36`) is not this path: it runs only after a session has ended, and `AgentRecovery.Decide` looks for fact-named files, so skill states bypass it. `AW_RESULT_FILE` sits in the launcher's `.sindri-out` folder (the same name as `AgentRecovery.OutputFolder`). A skill launch with no turn-end marker after 10 min gets a doctor row naming it. S-0 checks the marker for Codex as well as Claude Code. Result text (`waiting`, `summary`) goes through `Aw.Redaction` before it reaches the ledger or a row (S18).

| What it finds | What happens | Code |
|---|---|---|
| A result with no `waiting`, the `skill` matching, the verdict mapped, every output present | `complete` as `agent:result`: outputs to facts, verdict to outcome, each path's sha256 on the run | - |
| A result with `waiting` | Waiting on you; the row shows the question | - |
| No file, or one written before this launch started | Waiting on you: "stopped without a result" | - |
| The same file as at the last turn end (unchanged) | Still waiting on you | - |
| Not valid JSON | Waiting on you, with the parse error on the row (Take over, fix or tell the skill) | SND-SKILL-001 |
| `skill` names another skill | Waiting on you, "the result came from <other>" | SND-SKILL-002 |
| A verdict missing from the map | `step.failed`: "autoplan said 'MAYBE', which its block.yaml doesn't map" | SND-SKILL-003 |
| A mapped output missing, or a path output that's relative, missing on disk or holds a newline | `step.failed`: "autoplan's result has no artifact 'consolidated-review.md' (it has: arch-review.md, ...)" | SND-SKILL-004 |
| (before launch) `requires.yaml` names a bin, MCP server or skill the agent home lacks | `step.failed` before any session starts, naming each one and its fix | SND-SKILL-005 |

   The two failures are block-author bugs, so they fail; everything the owner can sort out in the session waits for them. Each waiting row is a `launch.owner-waiting` ledger event (§7), one per turn end. A `sindri workflow complete` call from a skill still works if one makes it.
4. **Waiting on you is bounded only by the state timeout.** The idle sweep skips skill-block launches; the session stays alive in tmux. The state's `timeout` ends the wait (`then: park`), and the park row offers Resume and Stop.

Worked example: autoplan's run ends with `{"skill":"autoplan","verdict":"NEEDS_WORK","feature_dir":"/home/u/.agentic-workflow/acme-api/plans/20261105-rate-limits","artifacts":[".../consolidated-review.md", ".../arch-review.md", ...]}` in `.sindri-out/result-LCH-7Q2.json`. Sindri records `fact:autoplanReport = ".../consolidated-review.md"`, `fact:autoplanFeatureDir = ".../plans/20261105-rate-limits"` (plus each file's sha256 on the run) and the outcome `needsWork`. Had officeHours instead ended a turn with `"waiting": "Who calls this API: browsers, servers, or both?"`, the row would read "idea-to-pr › Shape the idea is waiting on you: Who calls this API: browsers, servers, or both? [Take over]".

### 3.4 Where it runs and what it may touch

- **Working directory** (round 3, arch N9): each skill Step gets the launcher's default worktree (`…/.claude/worktrees/sindri-<workflow>-<run>`, `AgentLaunches.cs:42-47`), as any agent Step without `fact:workspace` does. The chain needs no shared worktree: skills write their plans under `$AW_DIR`, and specToProvenPR makes its own worktrees. The sandbox already sets `AW_SINDRI_BRANCH` to the run namespace + `/work` (`PlanEnvironment.cs:98`). The namespace is `sindri/<instance>/<RunToken.Number>` (`SandboxLaunchStages.cs:68-70`), and `Number` counts runs within one state entry from 1 (`RunToken.cs:19-21`), so it's `1` in practice. specToProvenPR derives its branch prefix by stripping `/work` from `AW_SINDRI_BRANCH` (an F-1 edit; no `AW_BRANCH_PREFIX`). One edge: a re-run within the same entry moves to namespace `2`, and the hook then refuses updates to a branch pushed at `1`, so the build-failed desk's Try again starts a new branch.
- **`$AW_DIR`:** exactly `$AW_STATE_DIR/<slug>/` of the run's repo, computed by the host (D8), so a skill inside `flow/<instance>` can't land in a second folder. Two runs on the same repo share it; the result file is per launch, and the skills' own folders are timestamped.
- **Push (D7):** `pushes: true` is a request. The block's Step runs with `sandbox.credentials.github: agent-app` only if the approval preview for this repo showed "build (specToProvenPR) can push to <owner/repo>, branches under sindri/<instance>/" and the owner approved. The SB-7 machinery does the rest: the token file, the shims, renewal, the pre-push hook on the run namespace, and `Release` when the Step ends, parks or is cancelled. The ledger records the grant on the launch (`launch.started` carries `push: true`). Otherwise the block runs scrubbed, and a push fails the way it does today (`SND-LAUNCH-023`). A pushing block pins the hash of its skill folder, so an edit to any file in it stops the run at that block's next finish until you approve again (§3.2).
- **Agent and model:** the profile's `workflow.agent.target`, or the state's `agent:` (RCP-AGENT-001). The seed's form comes from the target's launcher entry.

### 3.5 The recipe rules a skill state follows

- A skill state has `timeout: { after: <at least the block's maxDuration>, then: park }`: a new rule, RCP-SKILL-001, so the manifest's `maxDuration` is never a number nothing enforces (R17). `idea-to-pr`'s `build` state: `after: 48h`.
- Only `exit`, `idle` and `lost` failures route to the recipe's desk; a launch the launcher couldn't start keeps today's `unavailable` rule (two reruns, then park), so a down launcher doesn't turn into three "try again" clicks.
- Loops on skill states declare `noProgress: [sameFailure]` (D5): skills write outside the worktree, so the default's `emptyDiff` would park every rework loop after one round (R3, S6).
- Every outcome a skill block declares has a transition. That's today's `RCP-OUTCOME-001`, so no new rule; `idea-to-pr`'s evals include "build says blocked" to prove it (S21).
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
      needsWork: { to: revise, loop: { name: rework, cap: 2 } }   # the edge entering the cycle carries the loop
      blocked: stop
  revise:
    use: office-hours-revise
    with: { plan: { ref: shape.featureDir }, review: { ref: critique.report } }
    next: critique                                # closes the cycle; needs no loop of its own
  build:
    use: spec-to-proven-pr
    with: { spec: { ref: shape.featureDir } }
    on: { blocked: stop }
```

Rules:
- `use:` is the block id from `block.yaml` (no name mapping).
- A block's **success** outcome goes to `next:`: a block id, `end` (resolved) or `stop` (unresolved). Left out, it's the next block in file order, or `end` for the last. The validator refuses a left-out `next` whose default target is also reached by an explicit edge (`critique` would fall into `revise`), so the file says it.
- **Every other outcome needs an `on` entry** (`<block>`, `stop`, or `{ to, loop }`). An unhandled non-success outcome is a finding: `flows/idea-to-pr.yaml:14 critique.on: outcome 'blocked' is not handled; add 'blocked: stop' or 'blocked: <block>'`. A BLOCKED plan can't reach a pushing block by default.
- `with:` values are `{ ref: input.<key> }`, `{ ref: <block>.<output> }` or `{ value: <literal> }`. A ref is checked at validate time: the block exists, declares that output, the types fit (`path` into `path` or `text`; `text` into `text`), and the producer runs before the reader on every path (RCP-WIRE-001 on the compiled recipe, mapped back to flow names). Refs use flow names (`shape.featureDir`); the compiled fact keys are camelCase (`fact:shapeFeatureDir`, §4.2).
- **Loops** (S7): every cycle in the block graph carries exactly one `loop:`, on the edge that enters the cycle. The compiler puts it on the state that edge enters, which is what the validator expects today (a looped state needs a transition back into it, `RCP-LOOP-002`). In `idea-to-pr`, `critique.needsWork → revise` carries `rework`, and `revise.next: critique` closes the cycle with none. File order doesn't decide it: a wire to an earlier block that closes no cycle needs no loop. A cycle with no `loop:` is a finding. Each loop has a `cap` (RCP-LOOP-001), and the compiler gives skill loops `noProgress: [sameFailure]` (D5).
- Positions for the canvas live in `<name>.layout.json` beside the file, never in the hash.

### 4.2 What it compiles to

`sindri flow compile <file>` prints the recipe long form; validate, eval, approve and run all go through it. It is exactly the hand-written recipe of §5.1 with three differences: state ids are block ids, every state gets `displayName` = the block id so `workflow status` and needs-you rows never show compiled names (R20), and facts are per use. That last one needs **Step output aliasing**, the one recipe schema change: `produces: [{ fact: fact:shapeFeatureDir, from: fact:officeHoursFeatureDir }]` on a Step's transition (aliases follow D1's rule with the flow block id: `fact:<blockCamel><Output>`), the form a child's outputs already use. RCP-WIRE-002 is extended to allow it, the descriptor's own keys stay what the agent writes, and replay goes behind a `Workflow.Patched("step-output-alias")` marker. With it, one block used twice keeps both outputs.

- **Errors point at the flow.** Every compiled state and transition carries its flow source position; findings print against the flow file (`flows/idea-to-pr.yaml:14 critique.with.plan: 'shape.featurDir' is not an output of office-hours (it has: featureDir)`). The spec of F-4 lists the eight messages as golden text: unknown block, unknown output, unknown outcome, type mismatch, cycle without a loop, unhandled outcome, ambiguous default `next`, unreachable block.
- **Hash and pins:** the recipe hash covers the compiled long form (a block rename changes it; moving a card doesn't). The flow's `version` is the recipe's version (RCP-VERSION-001).

### 4.3 Evals

`sindri flow eval --scaffold <file> --out <dir>` writes one case per outcome path (it calls `recipe eval --scaffold` on the compiled recipe). `idea-to-pr` gets 6: pass, needsWork then pass, needsWork twice then pass (reaches build: the loop's `sameFailure` doesn't fire on a changing review, S6), needsWork x3 (cap hit, parked), critique blocked, build blocked, a failed block then again.

## 5. Trigger, run page and jumping in

### 5.1 Release 1's chain: `workflow.skill.idea-to-pr`

A shipped recipe in `recipes/sindri/`. The sketch is trimmed: labels and sentences are left out, and real recipes nest `use`/`with` under `step:` (S31).

```yaml
id: workflow.skill.idea-to-pr
version: 1
inputs: [{ key: fact:idea, type: text, kind: text }]
initial: shape
states:
  shape:    { use: step.skill.office-hours,        with: { idea: { fact: fact:idea } },                       timeout: { after: 3h,  then: park } }
  critique: { use: step.skill.autoplan,            with: { plan: { fact: fact:officeHoursFeatureDir }, mode: mvp }, timeout: { after: 3h,  then: park } }
  revise:   { use: step.skill.office-hours-revise, loop: rework,
              with: { plan: { fact: fact:officeHoursFeatureDir }, review: { fact: fact:autoplanReport } }, timeout: { after: 2h,  then: park } }
  build:    { use: step.skill.spec-to-proven-pr,   with: { spec: { fact: fact:officeHoursFeatureDir } },    timeout: { after: 48h, then: park } }
  shape-failed:    { use: step.skill.failed-desk, loop: retry-shape, with: { block: shape } }   # one per skill state
  # ... critique-failed, revise-failed, build-failed
  done:     { outcome: resolved }
  stopped:  { outcome: unresolved }
loops: { rework: { cap: 2, noProgress: [sameFailure] }, retry-shape: { cap: 3, noProgress: [sameFailure] }, ... }
transitions:
  - { from: shape,    to: critique, on: step.completed, produces: [fact:officeHoursFeatureDir] }
  - { from: critique, to: build,    on: step.completed, when: { outcome: pass },      produces: [fact:autoplanReport, fact:autoplanFeatureDir] }
  - { from: critique, to: revise,   on: step.completed, when: { outcome: needsWork }, produces: [fact:autoplanReport, fact:autoplanFeatureDir] }
  - { from: critique, to: stopped,  on: step.completed, when: { outcome: blocked } }
  - { from: revise,   to: critique, on: step.completed }
  - { from: build,    to: done,     on: step.completed, when: { outcome: done } }
  - { from: build,    to: stopped,  on: step.completed, when: { outcome: blocked } }
  - { from: shape,        to: shape-failed, on: step.failed }
  - { from: shape-failed, to: shape,   on: step.completed, when: { outcome: again } }
  - { from: shape-failed, to: stopped, on: step.completed, when: { outcome: stop } }
  # ... the same three per skill state
```

**A retry of `revise` uses a rework round** (arch N8): rounds count on every transition back into the looped state (`LoopRules.cs:14-17, 34-52`), and `revise-failed --again--> revise` is one. So the owner's limit reads "twice, counting retries", and the eval "revise fails, retry, needsWork -> parks" pins it. A launch that never started a session doesn't count (`LaunchFailed`).

`step.skill.failed-desk` is the one new shipped desk block (F-2a): outcomes `again` and `stop`, its row shows the failed Step's one-line reason, and Take over sits on the same row. Today's desks are domain ones (`step.build.scope-desk`, `step.evidence.desk`) or the route twin `step.desk.choose`, none of which fits. One desk per skill state, each behind its own retry loop (cap 3), is also what the compiler emits in release 2. Its evals (the 6 cases of §4.3) ship with it. Start: the app's Start page lists it with its one input, or `sindri workflow start workflow.skill.idea-to-pr` with the input (owner only, D7).

### 5.2 The run page

A run is a recipe instance, so the instance frame (`Views/Instance/InstanceFrameView`, states band, facts panel, timeline) already works. What ui adds in release 1 (F-3) is a skill state's card: its block's sentence, its state (waiting to start, running, **waiting on you** with the question, done with its outcome, failed), its outputs as clickable paths, loop rounds ("round 2 of 2"), the session's cost when the launcher reports one (summed on the run header, S32; otherwise "cost: not reported", no new work), and Take over on a running or waiting one. A chain keeps one result per block: a block used twice keeps the later one, and the Start page's description of such a chain says so. State is a word on the card, never colour alone, and the waiting row announces once, politely, through the live-region pattern `TakeoverBannerView` uses.

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
| Skill updated (drift park, §3.2) | "autoplan's skill was updated. Approve idea-to-pr again to continue" | Approve (opens the preview) | Re-approval |
| A block it uses isn't loaded (§3.2) | Start entry greyed: "idea-to-pr can't start: autoplan isn't loaded (<doctor reason>)" | Open Health | Fix the manifest, restart serve |
| Approval preview loading, or failed | "Loading the approval preview…", then "Couldn't load the approval preview: <reason>" | Try again, Cancel | The preview loads |
| Unapproved recipe, or first start on this repo | The approval preview opens, naming the repo and each pushing block's branches; Approve then starts it (one click) | Approve and run, Cancel | - |
| Run in progress | the instance frame's existing Cancel | Cancel | - |

Every row names the run (`idea-to-pr › ...`), so two runs on one repo never share a row. The waiting and failed rows are NeedsYou feed rows, so they count in the title, the badge and the tray (`AttentionSummary.cs:33` counts that feed); that's what lets the owner start a chain and walk away. Focus stays where it is when a row appears or clears, and the row is announced once. The first waiting row the owner meets carries one line under the question: "Take over opens this session in Warp. Hand back when you're done." With no viewer installed, Take over says so and offers the `tmux -L sindri-launch attach` command to copy. A long question is cut to one line on the row, with the full text in the row's details.

## 6. The canvas (ui; D9, release 2: F-5)

**A release-2 sketch.** Release 1 ships none of this. It fixes enough for F-5's goldens to start against (§6.7) and freezes only §6.9's asks of engine's sections. Everything else is re-read against what release 1 actually shipped before F-5 starts. It is drawn against release 1's blocks: ports of type `text` and `path` only (§3.1; `pr`, `issue` and `packet` wait for C6), and the five shipped skills' six blocks.

The canvas edits one flow's server-side draft (§6.4, §7's F-5 routes) and draws the flow file (§4.1) as cards and wires. It holds no rules of its own: wire legality comes from the candidates call, findings from the server, and the file stays the source of truth. A promoted draft is a normal flow version, approved and run like a hand-written one.

### 6.1 Where it sits

- **Top bar:** a **Flows** page beside Workflows and Recipes, chord `G F` (free today; `ShellViewModel.Chords.cs` `SecondKey`). The Flows page lists flows (`GET /sindri/flows`, §7), each with **Run**, plus **New flow**.
- **Flows, Workflows and Recipes:** Flows is what the owner writes (flow files and their drafts). Recipes is what they compile to and what gets approved (every recipe, flow-made or hand-written). Workflows is what's running (instances of any recipe). A flow's page links to its compiled recipe on Recipes, and a run of a flow is an ordinary row on Workflows.
- **A flow's page** has three tabs: **Canvas** (this section), **Runs** (its runs, each opening the run page of §5.2), and **File** (the flow YAML of the open draft, read-only, with each finding's line marked). The File tab is how the owner checks that the canvas wrote what they meant.
- **Opening a flow with an open draft** asks once: "You have a draft from 14:02 (12 edits). Resume or discard?" Discard asks again before it deletes. With no draft, the canvas shows the current version read-only with an **Edit** button. The first edit creates the draft (`POST .../draft`).

```
┌ Flows › Idea to proven PR ─────── Canvas | Runs | File ──────────────────────────────┐
│ Palette        │                    stage (ZoomBorder, 22 px dotted grid)        │ Inspector     │
│ ─────────────  │   ┌ Inputs ┐                                                    │ critique      │
│ ⌕ filter       │   │ idea T─┼──┐   ┌ 1 shape ─────────┐                          │ autoplan      │
│ office-hours   │   └────────┘  └──▶T idea  featureDir P─┐                        │ Reviews a pl… │
│ office-hours-  │                   │ done ◆            │ │                        │ ─ Inputs ──── │
│   revise       │                   └───────┬──────────┘ │                        │ plan ← shape… │
│ autoplan       │                           ┆ next       │                        │ mode  [mvp ▾] │
│ spec-to-prov…  │   ┌ 2 critique ─────────┐ ▼            │                        │ ─ Outcomes ── │
│ bug-fix-orch…  │   │ plan P◀──────────────┼────────────┘                        │ pass → build  │
│ review         │   │        report P     │                                     │ needsWork →   │
│                │   │ pass ◆ needsWork ◆ blocked ◆ │                              │  revise,      │
│ (only skills   │   └──┬───────┬───────────┬───┘                                 │  loop rework 2│
│  with a        │      │next   │rework ≤2  └──▶ ■ stop                            │ blocked → stop│
│  block.yaml)   │      ▼       ▼ 3 revise ──┐  (return wire to critique, no pill) │ [Test block]  │
├────────────────┴──────────────────────────────────────────────────────────────┴───────────────┤
│ Draft rev 14 · saved · 0 findings                    Undo  Redo   [Discard]  [Save as v3]        │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 6.2 How the file maps to the canvas

| In the file (§4.1) | On the canvas |
|---|---|
| A block (`blocks.<id>`) | A card: header = file-order number, block id and the block it uses; input ports on the left, one per declared input, in `block.yaml` order; output ports on the right; outcome ports along the bottom, one per outcome (`done` for a block with no `outcomes`) |
| `inputs` of the flow | One **Inputs** card, fixed at the top left: an output port per flow input. Add, rename and type (`text` or `path`) an input in the inspector |
| `with: { x: { ref: a.out } }` | A **data wire**, output port to input port: solid, Wire colour (`GraphEdgesControl`'s default) |
| `with: { x: { value: v } }` | No wire: the input port shows a filled glyph and the inspector shows the value in its field |
| `on: { o: <block> }` / `on: { o: stop }` | An **outcome wire** from the outcome port to the target card's header, or to the **Stop** terminal: solid, in the outcome's tone |
| `on: { o: { to, loop: { name, cap } } }` | The outcome wire plus a pill on **that** wire: "rework ≤ 2". The pill always sits on the edge that carries `loop:` in the file, which is the edge entering the loop (`critique.needsWork → revise`) |
| The edge that closes a cycle (`revise.next: critique`) | A **return wire**: routed round the left of the cards it skips, no pill, labelled with its outcome. It needs no `loop:` of its own when an edge on the same cycle carries one (§6.3) |
| `next:` (the success outcome's target, §4.1) | A **next wire** from the success outcome port. When `next:` is left out (the next block in file order), it's dashed, drawn at the dimmed tone, labelled "next"; wiring it somewhere writes `next:` and makes it solid, and deleting that wire brings the dashed default back. Every other outcome has no wire until it's wired: an unwired non-success outcome is a finding on its port (§4.1) |
| File order | The number in the card header. Moving a card on the canvas never changes the order. **Move up / Move down** in the inspector does |
| `<name>.layout.json` | Card positions. Missing positions (a flow written by hand, a new card from the assistant) get `RunCanvasLayout`'s deterministic layout (main path down the centre in file order, loop targets beside their source) and are saved on the next edit |

**Ports** carry their type as a letter plus a shape (`T` round for `text`, `P` square for `path`), so colour is never the only signal. Every port has an automation name ("plan, path input, wired from shape.featureDir").

**Card sizes.** `RunCardView` is fixed height (`RunCardSizes`: one header and a step strip), and a flow card has one to three port rows plus an outcome row. Reused: the card's chrome, header, focus and selection drawing, and `RunCanvasLayout.Of(graph, sizes)`, which already takes a size per node. New: `FlowCardSizes`, the height from the card's port and outcome rows, passed to that same layout call.

**Contrast.** Wires, port outlines and the dashed next wire meet 3:1 non-text contrast against the stage and its grid, in both themes. "Dimmed" (ports that aren't candidates, the default next wire) lowers emphasis but never below 3:1. The F-5 rendering test checks each tone against the grid.

### 6.3 Wiring

**Loops and cycles.** A **back-edge** is the edge that closes a cycle, wherever it sits in file order. Every cycle needs one edge on it that carries `loop: { name, cap }`, and that edge is the one entering the loop (what the validator checks, §4.1). In `idea-to-pr`, `critique.needsWork → revise` carries `loop: rework, cap 2`, and `revise.next: critique` closes the cycle with no loop of its own. The canvas never decides this. The candidates call (§6.9.5) marks each target that would close a cycle, with the edge the loop belongs on and whether that edge has one already:
- The cycle already has its loop (wiring `revise → critique` after `needsWork → revise` has one): the drop just sends the edit.
- It doesn't: the drop opens the loop form on the edge the server named (loop name, prefilled from the outcome; cap, default 2, 1 to 9), and that edge is outlined. The form is a small dialog-like region: focus moves into it, Tab stays inside it, Esc cancels the wire and returns focus to the port it started from, Enter sends both edits as one PATCH. A cycle is never sent without its loop.

**Mouse.** Press on an output port, which calls `GET .../draft/candidates?rev=&port=` once. While the call is in flight (usually under 300 ms), the wire follows the pointer and no port is dimmed. After 300 ms the status bar says "Checking where this can go…". When it answers, the legal input ports are lit and every other port is dimmed. Hovering a dimmed port shows the server's reason ("needs a path; report is text"). Dropping on a lit port sends the edit. Dropping anywhere else, or Esc, cancels without a request. If the call fails, the wire is cancelled at once, no port stays dimmed, and the status bar says "Couldn't check where this wire can go: <reason>. Try again." Dragging from an **outcome** port works the same way, with cards and the Stop terminal as targets. With reduced motion (the app's setting or the OS's), no wire follows the pointer: the start port stays highlighted, the port under the pointer shows lit or dimmed, and the wire is drawn once, on drop.

**Keyboard** (a must, not a nice-to-have). The stage is one tab stop: **Tab** and **Shift+Tab** always leave it (to the inspector, then the status bar, and back to the palette), so it never traps focus. Inside the stage:
- **Arrow keys** move between cards: ←/→ and ↑/↓ to the nearest card in that direction, **Home** and **End** to the first and last card in file order. Focus comes back into the stage on the card that had it last.
- **W** on a card lists its unwired output and outcome ports. Pick one with ↑/↓ and Enter, and the second list is the candidates, with each disallowed row disabled and its reason read out. Enter wires it. A cycle without its loop opens the same loop form. Esc backs out one list at a time and then returns focus to the card.
- **Enter** moves focus to the selected card's first inspector field. **Esc** in the inspector (from any field that isn't a dropdown left open) returns focus to that card on the stage, and Shift+Tab from the inspector's first field does the same.
- **Delete** removes the selected card. Removing a card that has wires asks first and names them. Wires aren't focusable, so a wire is removed from the inspector: each row of the card's wire lists (inputs and outcomes) has a **Remove** button, and Delete on a focused row does the same.
- **Shift+arrows** move a card one grid step (22 px), and the position is sent on key release; the inspector's **Position** fields (x, y) do the same. File order changes only through **Move up / Move down** in the inspector. The stage binds no Alt or Ctrl+Alt arrows: Alt+← already means "go up a layer" (`ContextPage.axaml.cs:79`), and Ctrl+Alt+arrows switch workspaces on GNOME.
- Ctrl/Cmd+Z and Ctrl/Cmd+Shift+Z are undo and redo. `+`, `-` and `0` zoom (ZoomBorder's keys today), and **F** fits the flow.
- **Single-key commands** (W, F, Delete, `+`, `-`, `0`, the arrows, Shift+arrows, Home, End) fire only while the stage has focus. In a text field (the inspector, the palette filter, the loop form) they type text. Undo and redo in a text field undo the field's text, not the draft.

**Screen readers.** Wires aren't focusable, so the inspector's lists are the readable view of them. A card's automation name reads like the file: "Block 2, critique, autoplan. plan from shape.featureDir. pass goes to build. needsWork goes to revise, loop rework, cap 2. blocked stops." The status bar is a polite live region that is always in the tree. It announces each message once and keeps the last one on screen until the next replaces it, so a refused edit's reason can be read after it's spoken. Every list in the keyboard path is a real list control with names, so the same steps work with a screen reader.

**Palette.** It lists `GET /sindri/skills` by block (the six release-1 blocks, so officeHours shows as `office-hours` and `office-hours-revise`), filterable by id and sentence. A skill that failed to load is shown greyed with its doctor reason. Drag one onto the stage, or Enter on it to place it under the selected card, and it becomes a new block with an id made from the block id (`autoplan`, then `autoplan2`). Rename it in the inspector, and refs to it follow because the rename is one op the server applies to the whole draft.

**Inspector.** It shows the selected card's sentence and inputs. Each input shows its wire ("← shape.featureDir") or a literal field, picked by type: a text box for `text`, a folder picker for `path`, and a dropdown when `block.yaml` has `values`. Below that are its outcome wires (with the loop, if one is on that edge), each wire row with **Remove**, the **Position** fields, **Move up / Move down**, and **Test block** (§6.5). With nothing selected, the inspector says "Select a block to see its inputs" and lists the flow-level findings (an unreachable block, an unhandled outcome), each one a button that selects what it points at.

### 6.4 Drafts (the client half of D9, and where they live)

- **Where a draft lives.** One draft per flow, server side, in `$AW_SINDRI_HOME/drafts.db` (owner-only, beside `views.db` but not rebuilt from the ledger, so `views rebuild` never drops a draft). Draft edits are not ledger events; only promote is (`flow.saved`). Drafts are never deleted by age: Health lists drafts idle for more than 14 days with **Open** and **Discard**.
- **Base.** A draft records the flow file and the hash of that file it started from. The **File** tab shows "Based on flows/idea-to-pr.yaml v2".
- **Every edit is an op.** The edit shows at once, then `PATCH .../draft {rev, ops[]}` goes out. Ops are small and named: `addBlock`, `removeBlock`, `renameBlock`, `setRef`, `clearRef`, `setLiteral`, `setOutcome`, `clearOutcome`, `setLoop`, `move` (position), `reorder`, `setInput`. Card drags send one `move` when you let go, never during the drag.
- **Refused or 409.** A refused op rolls back that one edit, and its reason shows on the card or wire and in the status bar. A 409 (someone else's rev landed first) re-pulls the draft, re-applies the ops not yet sent on top, and only then sends them.
- **Can't reach Sindri** is not a refusal. A PATCH that gets no answer (serve down, connection refused, timeout) rolls nothing back. The edits stay on the canvas, queue in order with the rev they were made on, and the status bar says "Can't reach Sindri: 3 edits waiting". When the stream reconnects, the canvas re-pulls the draft and sends the queue (a 409 replays as above). Leaving the page with edits waiting asks "3 edits haven't reached Sindri. Leave and lose them?". Save as is disabled while edits wait.
- **Undo** sends the inverse ops as a new edit (removing a wire you added re-adds it, `move` restores the old position). The stack holds ops, never revisions, so an assistant edit between yours survives your undo. The stack lives with the page: leaving the flow clears it, but the draft stays.
- **`draft.changed`** from someone else (the assistant, another window) applies the new revision live. What changed is outlined for 2 s (with reduced motion, a steady outline that stays until the next edit, no fade), and the status bar says "Assistant added review after critique" (from the event's op list). An edit you're making (a wire in the air, the loop form open) is kept and re-checked against the new revision when you finish it. `draft.changed` rides the event stream as a frame **with no id** (`SseWriter.Event` with a null id): it isn't a ledger event, so a reconnect never replays it, and the canvas re-pulls the draft on every reconnect instead.
- **Findings** come back on every PATCH response for the new revision, and the §7 PATCH route returns them. Each finding attaches to its card, port or wire and is listed in the status bar.
- **Save as vN** (`POST .../draft/promote`) is enabled at 0 findings and says what blocks it otherwise ("2 findings: build.spec is not wired"). Promote writes the flow file and bumps its `version` itself (RCP-VERSION-001: a content change is a new version). An owner's flow is written in place at `<repo>/.agentic-workflow/flows/<name>.yaml`. A shipped flow (`flows/` in the toolkit) is never written: Save forks it under a new id into the repo's `.agentic-workflow/flows/`, and the button says so ("Save as my-idea-to-pr v1"). Then the compiled recipe goes to approval like any other.
- **The file changed under the draft** (a hand edit, a pull, another promote). When the file's hash no longer matches the draft's base, the canvas shows a banner before any more edits: "flows/idea-to-pr.yaml changed since this draft started (v2 → v3)." It offers **Re-base** (the server re-applies the draft's ops on the new file; ops that no longer apply are listed and dropped, and the owner sees the list before it's applied) and **Discard draft**. Promote is refused until one is picked.

### 6.5 Test this block

**Test block** in the inspector calls `POST .../draft/test` for the selected block at the current rev.
- **The owner's only.** An agent session calling the route gets `SND-AUTH-002` (§7), so the assistant can edit a draft but never start a test.
- **It's a real session, without the push grant.** A test always runs scrubbed (no push token, even for a block with `pushes: true`), counts against the session cap like any launch, and runs in a fresh scratch worktree of the flow's repo at its current main, made for the test and removed when it ends (the `sindri skill run --skills <dir>` path, §7, with the draft's block as the one-trial manifest, so a block being edited in `block.yaml` is tested the same way from the terminal). The button says "Runs autoplan as a real agent session in a scratch copy of the repo (up to 3h, no pushes)", using the `maxDuration` from `block.yaml`. While it runs, the card shows running, **Take over** (§5.3), the "waiting on you" state with the skill's question, and the result: the outcome chip, the outputs as clickable paths, and the cost when the session reports one.
- **Cancel.** A running or waiting test shows **Cancel** on its card (it asks first: "Stop the autoplan test? Its session ends and the scratch copy is removed."). Leaving the page or closing the flow doesn't stop a test; it keeps running, shows on Workflows like any run, and its result is on the card when the owner comes back.
- **Inputs:** a wired input uses the upstream block's last test output in this draft when it's not stale, else it asks for a value in the same field the literal would use. Nothing runs until every required input has one.
- **Stale:** a result is keyed to (draft, rev). Any later edit to that block, or to anything wired into it, marks the result **stale** on the card, and a stale result is never fed into another block's test.

### 6.6 What the owner sees (canvas states)

Shaped like §5.4. Every surface also uses the app's vocabulary: skeleton rows on first load only, the stale pill after 30 s, the Not running card when serve is down.

| Surface | State | Text | Buttons | What clears it |
|---|---|---|---|---|
| Flows page | Loading | skeleton rows | - | The list |
| Flows page | Empty | "No flows yet. A flow chains skill blocks; start from a blank one or from idea-to-pr." | New flow, Copy idea-to-pr | A flow |
| Flows page | Request failed | "Couldn't load flows: <reason>" above the last list | Try again | A good read |
| Flows page | Serve down | the Not running card | Start Sindri | Serve up |
| Canvas | Loading | the stage with skeleton cards | - | The draft or version |
| Canvas | Empty (new flow) | the Inputs card and "Drag a block from the palette, or press Enter on one" | - | The first block |
| Canvas | Request failed (the draft won't load) | "Couldn't load this flow's draft: <reason>" | Try again, Open read-only | A good read |
| Canvas | Can't reach Sindri mid-edit | "Can't reach Sindri: N edits waiting" (§6.4) | - | Reconnect and send |
| Canvas | File changed under the draft | §6.4's banner | Re-base, Discard draft | A pick |
| Palette | Loading | skeleton rows | - | The list |
| Palette | Empty | "No skill blocks loaded. `sindri doctor` says why." | Open Health | A block |
| Palette | A block failed to load | the row greyed with its doctor reason | - | A fixed `block.yaml` |
| File tab | Loading | "Rendering the draft…" | - | The YAML |
| File tab | Request failed | "Couldn't render the draft: <reason>" | Try again | A good read |
| Candidates | In flight > 300 ms | "Checking where this can go…" | - | The answer |
| Candidates | Failed | "Couldn't check where this wire can go: <reason>. Try again." | - | The next drag |
| Test block | Running, waiting, done, failed | §5.4's rows for a skill state, on the card, plus Cancel while running or waiting | §5.4's, Cancel | §5.4's |
| Test block | Cancelled | "Test cancelled at 14:20. No result." | Test again | The next test |
| Inspector | Nothing selected | "Select a block to see its inputs" and the flow-level findings | One per finding | A selection |

### 6.7 What's reused and what's new

| Reused (`src/Sindri/Aw.Sindri.Ui/Views`) | For |
|---|---|
| `Controls/ZoomBorder.cs` | The stage: pan, zoom at the cursor, pinch, dotted grid, reduced motion |
| `Controls/GraphEdgesControl.cs` | Every wire in one render pass, the dashed next wires, the hit stroke, dimming, highlight around the selected card. Bezier routes and the left-side return route are a new `Route` shape |
| `Controls/MiniMapControl.cs` | The overview, bottom right |
| `Run/RunCardView.cs`, `ViewModels/Run/RunCanvasLayout.cs` | The card chrome, header and automation, and the layout for cards with no position (with `FlowCardSizes`, §6.2) |

New: `FlowCardSizes`, the port controls and the in-air wire, the palette, the inspector, the draft client (op queue, offline queue, rollback, replay, undo stack), the candidates overlay, the loop form, the re-base banner and the File tab. No new package. Everything stays in compiled bindings and passes `src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh` with zero IL2xxx/IL3xxx warnings.

### 6.8 Proof and tests

- **Unit (Ui.Tests, headless):** the op inverse table (every op's undo, round-tripped), 409 replay order, rollback of one refused op among three in flight, the offline queue (no rollback, sent in order on reconnect, leave-page prompt), the next-wire rule, the unwired-non-success-outcome finding, the loop form opening only for a cycle without its loop (and not for `revise → critique` in `idea-to-pr`), Tab leaving the stage from every card and Esc returning from the inspector, single-key commands not firing in a text field, the candidates failure cancelling the wire, the re-base banner, stale marking, each row of §6.6, and the automation names against goldens.
- **Rendering (Ui.Rendering.Tests):** each wire and port tone at 3:1 against the grid, in both themes.
- **API goldens first:** the draft, candidates and PATCH-with-findings responses as fake-route goldens, so the canvas is built and tested before F-5's server half lands. ui starts on them as soon as engine writes them at F-4's start (§8.2), the way F-3 starts on F-1's `/sindri/skills` golden and F-2a's `launch.owner-waiting` event and its Needs you row.
- **Proof in the running app (F-5's row in §8.1):** build `idea-to-pr` from an empty flow **by keyboard only**, driven through `aw-ui-driver`: inputs, four blocks, data wires, the rework loop, then Save as v1. Then one more step with the mouse. Then an assistant edit arrives mid-session and shows live. Then undo steps over the owner's edit and leaves the assistant's. Then stop serve, make two edits, start it, and see them land. Each step gets a screenshot and its automation tree.

### 6.9 Asks of engine's sections (ui's picks)

Asks 1 to 4 are accepted by engine and folded into §4.1 and §7. Ask 5 is new in round 2.

1. **PATCH returns the findings** for the new rev (§6.4), so the canvas doesn't send a validate after every edit. `POST /sindri/flows/validate` stays for files.
2. **Literal values in `with:`** (`mode: { value: mvp }`), now in §4.1.
3. **`draft.changed` carries the op list** (or a one-line summary per op), so the status bar can say what the assistant did without diffing two revisions in the app.
4. **`renameBlock` is one server op** that rewrites every ref to the block, so a rename never leaves dangling refs, even for a moment.
5. **The candidates response marks cycles.** For each target that would close a cycle: `closesCycle: true`, the edge the loop belongs on, and whether it already carries one. The canvas opens the loop form from that, never from its own graph walk (§6.3). Also: `draft/test` is owner-only and scrubbed (§6.5), the draft records its base file hash and promote bumps the version and forks shipped flows (§6.4), and `draft.changed` is an id-less stream frame (§6.4).

## 7. API and CLI

All new routes are on the existing loopback control plane and follow its error and auth rules.

**Release 1:**

| Route | What it does | Caller |
|---|---|---|
| `GET /sindri/skills` | Loaded skill blocks: id, skill, descriptor id and version, sentence, inputs, outputs, outcomes, `pushes`, SKILL.md sha, the path it was loaded from, and the doctor reason for any that didn't load | any |
| `POST /sindri/skills/{id}/runs` | One block as a trial run: a one-state synthetic recipe. Starting it records an approval of that recipe's hash with the owner as approver, because the owner's request is the approval and every start needs one (`StartInstanceService.cs:165-168`; arch N7). Without `--repo` it uses the repo of the current directory. It runs always scrubbed (no push grant, even for `pushes: true`), counted against the session cap, in a fresh scratch worktree at the repo's default branch that's removed after. `$AW_DIR` is still the repo's real one, so a trial of `office-hours-revise` on a real plan edits that plan, with no undo. The CLI header and the Test button say "Writes to your real plans under <AW_DIR>" and print the path (round 3, security; a scratch `AW_DIR` would make trials on real plans impossible). The same path as §6.5's Test block (S5, S24) | owner; a session is refused `SND-AUTH-002` |

There's no push-token route: pushing uses SB-7's agent-app mode (D7, S3).

CLI: `sindri skills [--json]` (exit 0 ok, 1 not running); `sindri skills --stats [--json]` (§8.3's three counters from the ledger); `sindri skill run <id> [--skills <dir>] --input k=v | --input-file k=<path> [--repo R] [--follow] [--json]` (the route above; `--skills` sends the folder so serve checks and runs that manifest for this trial only, §3.2). Without `--follow`: exit 0 started, 1 not running, 2 refused. With `--follow`, it prints the manifest hash at the top, and at the end the outcome, each `fact:<key> = <value>`, the result path and any `SND-SKILL-00x` line. It exits 0 when the block ended on its `success` outcome, 3 for any other mapped outcome, 4 for `step.failed` (round 3, devex). One golden for that output is in F-1's red commit. Both are listed in `Usage.cs`, the help goldens and aot-smoke's expected text. `recipe validate|eval` take `--skills <dir>` (§3.2). Everything else uses today's verbs: `sindri workflow start|status`, `takeover`, `handback`, `recipe approve`.

Ledger: `launch.started` carries `skill`, the descriptor `Version`, the manifest path, the SKILL.md sha, `push` and `trial` (true for `skill run`); the `workflow.transition` of a skill Step's completion carries the result file's sha256. A new **`launch.owner-waiting`** event, one per turn end, is history. The needs-you row itself is computed live from the reader's last result, the way `StalledAgentRuns` feeds today's stalled rows, so it clears when the Step completes, the next turn starts, or the instance is taken over (arch N10). Its fields are `launchId`, `reason` (`question` | `noResult` | `unreadable` | `skillUpdated`), `text` (the question, redacted), `resultPath`, and `detail` (the parse error or the other skill's name) (S2, S22). `launch.waiting` already exists (the session-cap wait, `EventTypes.cs:168`), and the hyphenated name follows `launch.session-reported`. The row's details show `resultPath`. The API goldens for these are integration-lane and run in F-1's and F-2a's gates.

**Release 2** (with the format and the canvas):

| Route | What it does | Caller |
|---|---|---|
| `GET /sindri/flows`, `GET /sindri/flows/{id}` | Flows with their approval state, source, layout and the compiled recipe's hash | any |
| `POST /sindri/flows/validate` | Body: a flow file. Findings against flow positions (§4.2). Writes nothing | any |
| `POST /sindri/flows/{id}/runs` | Body: inputs. Starts a run | owner; a session is refused `SND-AUTH-002` |
| `POST /sindri/flows/{id}/draft`, `PATCH .../draft` (`{rev, ops[]}`, the response carries the new rev's findings), `GET .../draft/candidates?rev=&port=`, `POST .../draft/test`, `POST .../draft/promote` | The canvas's server-side draft (§6.4): drafts live in `$AW_SINDRI_HOME/drafts.db` (not a ledger view, so `views rebuild` doesn't touch it), are never deleted by age (Health lists ones idle over 14 days), and record their base file and its hash. PATCHes are not in the ledger. `draft.changed` is an SSE frame with no id, never replayed (the canvas re-pulls on reconnect), and carries the op list. `renameBlock` is one op that rewrites every ref. The candidates response marks each target that would close a cycle with `closesCycle`, names the edge the loop belongs on (§4.1), and says whether that edge has one already (§6.9.5). `test` is §7's skill-run path at the draft's rev. Promote writes `<repo>/.agentic-workflow/flows/<name>.yaml` and bumps `version`; a shipped flow forks under a new id; a changed base file refuses promote until the owner picks Re-base or Discard (S9). Promote's ledger row is `flow.saved` | owner, the assistant; promote and test are the owner's |

CLI: `sindri flow list|show <id>`; `sindri flow validate|compile|eval <file> [--json]` (in process, no serve, like `recipe validate`; exit 0 pass, 1 findings, 2 usage); `sindri flow approve <file>` (registers and approves the compiled recipe; the preview lists each block, its pushes and its SKILL.md sha; owner only); `sindri flow run <id> --input k=v | --input-file k=<path> [--follow]` (relative paths resolve against the cwd; exit 0 started, 1 not running, 2 refused).

## 8. Phases and the parallel plan

### 8.1 Phases

**Release 1** (one trigger, a chain, jump in):

| Phase | What ships | Lane | Needs | Proof in the running app |
|---|---|---|---|---|
| **S-0** Spike (day one, R18; ~0.5 day) | No product code. Run autoplan and officeHours in a launcher session with `AW_RESULT_SKILL` / `AW_RESULT_FILE` set by hand, under both Claude Code and Codex. Check: each writes the per-launch file; a question comes back as `waiting` instead of AskUserQuestion; what the hooks report meanwhile, including the turn-end marker for Codex (S14); how many turn ends officeHours takes to shape one idea (S25); specToProvenPR naming its branches from `AW_SINDRI_BRANCH` (§3.4); whether a write Step can write anywhere in the run area besides `.sindri-out/` (the shims and the pre-push hook live there; only `.sindri-out/` should be writable). Notes into this spec | engine | - | The two result files and the hook markers, pasted into the spec's §3.3 as fixtures. If officeHours needs more than 3 turn ends per idea, API P10a's attach (Answer without Take over) becomes a dependency of F-3 (S25) |
| **F-1** Skill blocks (~3 days) | `block.yaml` for the six blocks + `check-skill-blocks.mjs`; the output-contract change (`AW_RESULT_SKILL`, `AW_RESULT_FILE`, `waiting`) and the `aw-result` block in the five skills; `feature_dir` in officeHours' Result block and its revise mode; specToProvenPR's branch prefix from `AW_SINDRI_BRANCH`; the skill seed branch in `AgentLaunches.StepArgsFor` (D2); the result read inside `WatchAsync` (§3.3); the state timeout on the launch and no `longMax` cap for skill launches (D8); `repo-slug.sh` keeps a preset `AW_DIR` (+ bash test); manifest loader with `--skills` / `AW_SINDRI_SKILLS_DIR`, and `step.skill.*` descriptors; result-file completion inside `WatchAsync`, and SND-SKILL-001..005; `requires.yaml` launch check; `AW_DIR` and the wall limit in the sandbox plan and policy; `GET /sindri/skills`, `sindri skills`, and `sindri skill run` with its route (S24); `.env.example` lines for `AW_SINDRI_SKILLS_DIR`, `AW_RESULT_SKILL`, `AW_RESULT_FILE`; aot-smoke `expected.txt` for the new doctor rows and help (S23); **docs**: `docs/csharp/sindri-skills.md` (block.yaml author guide and the numbered author loop, the result-file table, the error codes), `.agents/rules/skills.md` (the new file), `AGENTS.md` lines (R8) | engine | S-0 | `sindri skill run autoplan --input plan=<a real plan folder>` completes with the right verdict and facts, with no `workflow complete` call from the agent |
| **F-2a** Waiting on you, and the chain to a proven plan (~3 days) | The idle sweep and `StalledAgentRuns` skip skill launches; `launch.owner-waiting` and its needs-you rows; the finished-while-taken-over nudge; handback completes from the result file; the approval check before a pushing block completes, and the doctor warning for the rest (§3.2); RCP-SKILL-001; owner-only start for pushing recipes; `step.skill.failed-desk`; the shipped `workflow.skill.idea-to-pr` with its 6 evals; `sindri skills --stats` (§8.3); docs: the owner-states table in `sindri-skills.md` | engine | F-1 | Start `idea-to-pr` on a scratch repo from the app; officeHours asks its first question; the row shows it; Take over opens Warp; answer, hand back; the chain runs shape -> critique -> (rework once) -> a passed plan. `build` is run by Take over (no push grant yet), and its push fails with `SND-LAUNCH-023` as today |
| **F-2b** Push (~2.5-3 days) | the `StepPolicy.Github` ask and its one resolver (D7); a doctor row and a preview that refuses push when `AGENT_APP_KEY_FILE` is missing or the profile repo name doesn't match GitHub's (SB-7's `no-key` / `http-422`, arch N11); `pushes: true` -> `agent-app` on the Step; the approval keyed per repo and the preview line naming repo and branch prefix; every row's Approve opens the preview; grant reuse on re-entry; docs: the push section in `sindri-launcher.md` | engine | F-2a | The same run reaches build on its own and ends in a pushed PR on the scratch repo, on a `sindri/<instance>/...` branch. A second start on another repo opens the preview first. Restart serve during build: the push still works, or the run parks with the restart row |
| **F-3** Skill cards (~3 days, ui) | The skill state's card on the instance frame (§5.2), the rows of §5.4, Take over and the nudge on them, the approve-and-run preview from Start | ui | F-1's `/sindri/skills` golden, F-2a's `launch.owner-waiting` golden and its Needs you row; P10a only if S-0 says so | The F-2b run watched and jumped into from the app only |

Estimates are engine's guess for a lead with builders, and S-0 re-checks them. Serial that's about 12-13 days (F-2b grew with the per-Step switch). With §8.2's parallel plan, a usable chain (F-2a, build by Take over) lands around day 6.5, and all of release 1 around day 8-9. If it runs over, F-2b goes first, because build by Take over still works without it. Then the doctor's manifest-drift warning, then the `requires.yaml` launch check.

**If S-0 fails:** if a target reports no turn end, or ignores the `waiting` instruction, release 1 ships for Claude Code only and §3.1 says so; Codex waits for a hook fix. If officeHours needs more than 3 turn ends per idea, P10a's attach becomes a dependency of F-3 (S25).

**Release 2** (starts on the owner's word after using release 1; §8.3):

| Phase | What ships | Lane | Needs | Proof |
|---|---|---|---|---|
| **F-4** Flow format | Flow schema, compiler, Step output aliasing (Patched), source-position findings and their golden messages, `flow validate|compile|eval|approve|run`, `/sindri/flows*`, `drafts.db`, `flows/idea-to-pr.yaml` replacing the hand-written recipe (it compiles to the same id, `workflow.skill.idea-to-pr`, at version 2, so the Start entry stays and running v1 instances keep their pins); docs `sindri-flows.md` | engine | release 1 | `sindri flow run idea-to-pr` runs the same chain as F-2b |
| **F-5** Canvas | §6, the draft routes, candidates, test one block | ui + api | F-4's format frozen | Build `idea-to-pr` on the canvas from an empty flow by keyboard, run it |
| **F-6** Linear trigger | "Run flow on this issue" | context | #2077 merged, F-4 | One issue starts one run, linked both ways |
| Later | Parallel blocks (children), the n8n palette (cron, Slack, code, logic, CEL), a native graph runtime if §8.3 says so | - | - | - |

### 8.2 Parallel plan (always-busy)

- **Engine, day one:** the lead runs S-0 (a few hours, no builder). In parallel, builder A writes F-1's red commit: the `/sindri/skills` golden, the loader tests over two fixture manifests in `tests/Aw.Sindri.Testing`, and the result-file table as one test per row. After S-0, builder A goes green on F-1's C# half and builder B does the skill-side half (the five `block.yaml` files, the output-contract and Result block edits, officeHours revise, `check-skill-blocks.mjs`, docs), which touches no C#.
- **After F-1's descriptors land:** builder A on F-2a's server half; builder B on `idea-to-pr` and its evals (recipe only). F-2b follows F-2a on builder A; it's mostly wiring an existing SB-7 mode.
- **ui, day one:** F-3's cards and rows against fake routes and the goldens from F-1's and F-2a's red commits (engine pings ui with each red sha). Then §6's view models against the release 2 goldens, which engine writes when F-4 starts.
- **context:** F-6 once #2077 and F-4 are in; nothing before.
- Red first in every phase: each PR opens with its failing tests and goldens.

### 8.3 When to build the format, and when to stop compiling

Release 2 starts when the owner has run `idea-to-pr` and written or asked for a second chain by hand, and says the long form is the slow part. If they're happy writing recipes, F-4 waits and the canvas edits recipes instead (a smaller §6). Three counters, read from ledger rows by `sindri skills --stats` (built in F-2a, once there's a chain to count; no Health section in release 1), back that call with numbers (S27):
1. Runs per chain (`workflow.started` per recipe id). Is the chain used at all?
2. Takeovers per run (`workflow.transition` rows into the engine's `taken-over` state, `EngineStates.cs:13`; there's no separate takeover event type). Is the owner jumping in on every run? If yes, the blocks need work before a canvas does.
3. Rework rounds per run (`workflow.transition` rows into `revise`, the state that carries the `rework` loop). Does autoplan send plans back so often that the chain's shape is wrong?

Compiling then stays the plan while every flow the owner writes fits it. Any one of these, met on a real flow, is the signal to write a native-runtime spec: (1) a flow needs two blocks running at once that children can't express cleanly; (2) a compiled flow passes 40 states (twice the median of today's big recipes, `flow-internal.md` §1) and its findings stop reading well against the flow file; (3) the owner wants an edit to apply to a running run, which pinning forbids by design.

## 9. Owner questions

1. **Release 1 is skill blocks plus one ready-made chain (idea -> officeHours -> autoplan, needs work loops back at most twice -> specToProvenPR), started from the Start page, with Take over to jump in. The flow file and the canvas come after you've used it. OK?** My pick: yes. The chain runs sooner, and the format gets built from what hurts in practice.
2. **May a block push and open PRs?** My pick: yes, per block and per repo, only when you approve the chain for that repo and the preview says "build (specToProvenPR) can push to <owner/repo>: git pushes only to branches under sindri/<instance>/; gh can also act on <owner/repo> with the App's permissions (<list>), except merging PRs and deleting". It runs the same way Sindri's own pushing Steps do: the agent App token, renewed every 45 min. Renewal stops when the block ends, and the last token expires within the hour. Without it, specToProvenPR can't finish a PR inside a chain. Starting the chain on a second repo asks again.
3. **When you edit a skill, should chains already running stop and wait for you?** My pick: only for blocks that push. For those, an edit to any file in the skill's folder stops the run at that block's next finish, until you approve again. For any other skill, a running chain carries on with what it started on, and `sindri doctor` tells you which runs use an edited skill. A new run always uses the current version.
4. **Inside a chain, skills ask their questions as plain text and stop, instead of the pop-up picker, so Sindri can see they're waiting and show you the question. OK?** My pick: yes. A pending picker looks like a busy session, so nothing would tell you it's waiting.
5. **Which chain ships first?** My pick: idea -> officeHours -> autoplan -> specToProvenPR. The second one, written by hand after it, would be bug -> `bug-fix-orchestrator` -> `review`, with review's "needs changes" going back to the fix at most twice. Those are two different blocks, so nothing collides. Release 1's limit: a chain can't use the same block in two places and keep both results; it keeps the later one. Release 2's aliasing lifts that.
