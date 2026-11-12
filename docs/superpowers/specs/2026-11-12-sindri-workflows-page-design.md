# Sindri's Workflows page: build it, try it, trust the run

Status: draft (ideas lane, 2026-11-12). Serves #2978 (one app that replaces the others) and #2960 (workflows trivially easy), and builds the "Workflows" rows of the prior-art ledger (`docs/sindri/prior-art/workflows.md`, merged in #3012).
Reviews: 3 /autoplan rounds plus the screenshot review, before ready (no owner read).

## 0. Why

The owner's bar for workflows (#2960): trivially easy to build, and guaranteed to work once on. Today a person cannot get from nothing to one passing run.

ux walked the three reference workflows from a fresh scratch home on main 2766315 (issue comment 6819037446). A is "a PR opens, a review loop runs, ping me". B is "an issue is labelled, a plan comment is posted". C is "a nightly check opens an issue when it is red". **0 of 3 reach a passing run.** A and B cannot be built. C only exists as a reworded Weekday check template, and its first run fails. n8n, Zapier and Windmill take about 4 steps for the same jobs (trigger, action, test, publish; ux's prior-art notes). We fail at step 2 of 4.

What main does today (read from `origin/main` 4fafac85b4; the app was not driven for this spec, ux's walk is the evidence):

- **Add step saves an empty step.** `GraphEditViewModel.AddStep` (`Ui/ViewModels/Workflows/GraphEditViewModel.cs:107`) sends `add_node` with an empty `config`. The server checks required settings on every edit, so all 10 kinds fail with `GRAPH-CONFIG-001` (#3072). The canvas can only edit the steps a template made.
- **No way to change the trigger.** A second trigger is refused with `GRAPH-TRIGGER-001`; the only path is remove, add, reconnect (#3075).
- **No try before on.** The UI never calls the validate route, and Run now is refused until Activate (`SND-GRAPH-105`, `SindriErrors.Graph.cs:50`), so the first try is a live run (#2969). The engine has no test mode (checked in #2969's plan).
- **A stock template fails its first run.** Weekday check ends `SND-GRAPH-103: the answer is not JSON: 'U' is an invalid start of a value`. The run page shows the node's Input but not what the model said (#3079).
- **A finished run keeps saying Running.** `RunsViewModel` reads the run list once (`RunsViewModel.cs:107`); no file under `Ui/ViewModels/Workflows` or `Ui/Views/Workflows` reads the `workflow.run.finished` event. A run that failed 14 s after it started still shows "Running, Started 0s ago" 2.5 minutes later (#3080).
- **One step, three names.** The Add step menu says "If" (`ConditionBlock.cs:16`), the canvas says "condition" (the block type), the sheet says "Settings of gate" (the node id, `Labels.WorkflowEditor.cs:35`) (#3074).
- **No event for a PR opening or an issue being labelled.** The block catalog has `github.pr.merged` only, and that one never fires on real GitHub (#2988, #3008).
- **The graph is invisible to a screen reader** (#3073).
- **The engine has holes that make "valid" not mean "will run"**: #2985 (every `run_agent` node refused at launch), #2986, #2987, #2999, #3001, #3003, #3005 and the 14 unfiled items in the audit comment on #2960 (issuecomment-6817972848).

The job: **from nothing to a proven, running workflow in 4 steps: pick a trigger, add an action, test it, turn it on. The test uses real or sample input, spends no model time unless asked, and sends nothing. When it passes, it will run.**

## 1. The bar

"Ledger N" is problem N in `docs/sindri/prior-art/workflows.md`. "Take N" is item N of its closing list "What we take". The ledger's "Unconfirmed / thin" section is not used as a source: no claim below leans on wrkflw's `--dry-run`, Pipedream (shutting down 2027-03-31) or the gallery counts it could not read.

| Step of the loop | Ledger | Best tool today | What we take | Here |
|---|---|---|---|---|
| Start from zero | 1 | Zapier one-decision-per-screen, Retool unconnected warning | trigger, action, test, on; an unfilled step is a visible warning, never a silent skip | §4.1, §4.2 |
| Start from words | 2 | n8n Assistant, Windmill compare view | Describe it makes a skeleton, shown as a diff, validated and test-run before "done" (Take 3) | §4.1, phase P6 |
| Start from a template | 3 | n8n, Actions starters | a template is a full spec plus a first-run checklist; the gallery holds the 10 jobs of #2960 | §4.1 |
| Pick a trigger, try it on a real event | 4 | Pipedream, Zapier, Inngest | last 10 real events of the kind, pick, edit, would-match line (Take 4, #3032) | §4.3, §6 |
| Fill a field from earlier output | 5 | n8n drag, Windmill suggest | `/` opens a picker of upstream outputs with real values (Take 7, #3033) | §4.4 |
| Check before on | 6 | actionlint | a stable id per finding, shown on the step; Activate off while a finding blocks (Take 1) | §4.5 |
| Dry run, effects fenced | 7 | none solid (ledger) | ours: effect steps record "would do" with the exact payload (Take 2) | §5 |
| See each step's input and output | 8 | Inngest, Dify | the canvas with a dot per step; click a step for Input, Output, Error, Raw answer | §7 |
| Re-run from a step | 9 | Inngest plus Dify | later: "Re-run from here" with an editable input | phase P7 |
| Fail in words, fix in one action | 10 | Inngest, Temporal | a sentence plus one button chosen from the cause (Take 5) | §7.3 |
| Versions, undo | 11 | n8n history | later; the store has revisions already | §11 |
| Safe replay | 12 | Inngest | already true for agent nodes (`agent-launch:<run>:<node>:<index>`) | §10 |
| Keyboard | 13 | n8n | arrows walk the graph, `N` or Ctrl+K add, Enter opens | §4.6 |
| Connections | 14 | Windmill, n8n | the form lists connected services; none fits, "Connect GitHub" and come back | §4.2 |
| Is it on, when next | 15 | n8n, Zapier | a state chip beside the title (Take 6, #2970) | §4.7 |

Nobody has a dry run with side effects fenced by block (ledger 7). That one is ours and has no prior art to lean on.

**The bar in one line:** each of A, B and C reaches a passing test run from an empty scratch home in at most 4 steps and under 3 minutes; the test sends nothing; a run that ends changes on screen within 1 s; and "passed" means the same checks Activate runs.

Done-checks. Each is a test or a measured run in the real app (`aw-ui-driver`). Each names its lane in §14.

| # | Check | Measured as |
|---|---|---|
| W1 | **The ux walk, A.** Trigger "PR opened", action "run an agent to review it", action "tell me", test, activate. | From a fresh `context-first-run.sh` home with a GitHub connection: at most 4 steps and 3 minutes to Active with a passing test run. Today: cannot be built. |
| W2 | **The ux walk, B.** Trigger "issue labelled", a Prompt step that writes a plan, a "comment on the issue" step. | Same measure. Needs an issue-labelled event (§12 gap 1). |
| W3 | **The ux walk, C.** Schedule, a check, an If step, create an issue on red. | Same measure. |
| W4 | Every stock template passes its first test run. | CI runs each template twice: with a fake model that returns the schema's example, and with one that returns plain text. The second case ends with a sentence and the first 200 characters of the answer (§6.2), never a parser message. |
| W5 | Add step never saves an invalid step. | For each of the 10 kinds: pick it, the sheet opens, Cancel adds nothing (revision unchanged), Save with an empty required field is refused in the sheet and sends nothing, Save with the fields filled sends one `add_node` and the count goes up by 1. |
| W6 | Test run sends nothing. | A test run of A, B and C against the fakes: zero notifications, zero HTTP writes, zero created items, zero agent launches, zero model calls (default mode). The run view lists each as "would do". |
| W7 | Activate means it runs. | Property: a revision that passes validate and has a passing test run never fails its first live run on a missing connection, repo or agent target (#2985, #2986, #2987, #3001 are the red tests). |
| W8 | Run state is true. | A run ends, the row and header change within 1 s (p95 over 50 runs on the fakes); "N ago" ticks; no run stays "Running" after the engine closes it (#2999 sweep). |
| W9 | First paint under 100 ms from the local store; key to visible change under 50 ms. | Window open to the workflow list painted, 200 workflows; keydown to repaint. |
| W10 | Run page opens fast. | A run with 50 steps opens in under 200 ms (ledger 8). |
| W11 | One name per step kind. | A test walks every kind: menu label, canvas subtitle, sheet title and run row use the catalog `Title`; no node id or type id appears outside the JSON view. |
| W12 | Screen reader. | Each step is its own element named "Check, Prompt step, 2 of 4"; moving the selection is announced (#3073). |
| W13 | Agent parity. | A test lists the page's actions; each has a CLI verb and an MCP tool; results fence repo text. |

## 2. Decisions

1. **A step opens its form when you add it and is saved only when valid.** Pick Add step, pick a kind, the settings sheet opens with that kind's required fields and presets (#2891). Save sends ONE `add_node` with the filled config. Cancel adds nothing. *Why:* the server keeps checking required settings on every edit, so a saved revision always runs; the fix is in the editor only. This is meta's decision on #3072 and fixes it.
2. **Test run works on a draft.** A draft is the newest revision when it is not the active one. Test run needs no Activate. It takes a sample input or a picked real recent event, and it fences every effect step (§5). *Why:* the first try must not be a live run (fixes #2969). Meta accepted engine's split on that issue.
3. **The run page shows each step's input AND output, and the model's raw text when the JSON parse fails.** The node run keeps the first 64 KB of the raw answer when `SND-GRAPH-103` fires, and the sentence quotes the first 200 characters (fixes #3079's second and third ask).
4. **A run's state comes from the engine's terminal event, and "N ago" ticks.** The page reads `workflow.run.started`, `workflow.run.finished` and `workflow.run.cancelled` from the event stream and repaints the row and header within 1 s. One shared clock (1 s under a minute, 30 s after) drives every "N ago" (fixes #3080).
5. **Replace the trigger is one op.** A new graph op `replace_node` swaps a node for another of the same slot, keeps its outgoing edges, and is checked as one edit. The trigger slot offers "Change trigger" (fixes #3075). *Why:* three client-side steps with a broken graph between them is the bug.
6. **One word for the If step everywhere: "If".** The catalog `Title` (`ConditionBlock.cs:16`) is the only name shown. The canvas subtitle, the sheet ("Settings of Flagged?", the step's own title) and the run row use it. Type ids and node ids show only in the JSON view. Same rule for "Send notification". *Why:* the menu already says "If", and "condition" and "gate" are a type id and a node id (fixes #3074).
7. **Four steps, in this order: trigger, action, test, turn it on.** The page's primary button follows the step the workflow is at: "Add a trigger", "Add an action", "Test run", "Activate". One decision per screen region, as Zapier does (Take on ledger 1).
8. **Activate needs a passing test run of the current revision, and every validate finding gone.** Until the UI button ships, Activate only warns "Not tested yet"; from phase P4 it is refused. This is the contract meta accepted on #2969. See open question 1.
9. **The test run is fenced by block, not by promise.** The block catalog says which blocks write; a test version of each write port records the call and returns a made-up result (§5). Model and agent steps are stubbed by default and opt in per run (§5.2).
10. **Raw JSON is for the Advanced fold.** Fields show captions (#2925), presets, and the `/` picker. A Prompt step's output is a small form of named fields, not JSON Schema typed by hand (#2968).
11. **The trigger step lists real events.** The last 10 events of its kind that Sindri saw on this box; pick one, edit it, pin it; a line says "would match" or "would not match: repo is not x" (#3032).
12. **State is a chip.** "Active, next weekday 7:00" or "Off" beside the title, the same word and glyph as the list row (#2970).
13. **Agents get the same verbs.** Every action here has a CLI verb and an MCP tool; a session reads and tests but never presses the owner's Activate (§8).
14. **The old canvas-only editor stays until P5.** The profile key `workflows.editor` (`sheet` | `steps`) picks the editor. P1 to P4 ship `sheet` (today's) as the default and `steps` for the owner to try. P5 flips the default; P6 deletes the old path after one week with no miss of W1 to W3. After P6 the key is accepted and ignored for one release.

## 3. The model

### 3.1 The draft and its checks

There is no new store. A workflow already keeps immutable revisions (`docs/csharp/sindri-graph.md:48-51`: every save is a revision, `Activate` pins the one triggers run, a stale base is `SND-GRAPH-007`). Four facts hang off a revision:

| Fact | Where it lives | Set by |
|---|---|---|
| `findings[]` | computed on read by `POST /sindri/workflows/validate` (no write) | validate; each finding has a stable id, the node it belongs to, a cause line and a fix line |
| `tested` | a `workflow.tested` ledger event naming workflow, revision, run id, mode | a test run that ends `succeeded` |
| `pins` | `test.pins` on the revision: `{<node>: output}` | the owner pins a step's output, or a test request sends it |
| `active` | the pin `Activate` writes | Activate |

The page's step is derived, not stored:

1. no trigger -> "Add a trigger"
2. a trigger, no other step -> "Add an action"
3. any finding -> "Fix N things" (opens the first)
4. no passing test of this revision -> "Test run"
5. tested and off -> "Activate"
6. active -> "Active, next <time>"

### 3.2 A test run

A test run is a run row with `mode = test`. It follows the real graph walk (`GraphWorkflow.Walk`), so it checks the same wiring. Differences from a live run:

- The trigger is a payload, not an event. The payload is a picked real event, a hand edit of one, or the trigger block's schema example when nothing was seen.
- Every write port is replaced by a recorder (§5.1).
- `prompt` and `run_agent` return the pinned output, else the schema example (§5.2).
- The run never counts as the workflow's last real run, never takes a `one-per-workflow` slot from a live run, and never shows in Home's run failures.
- It runs validate first, so a test cannot pass what Activate would refuse.

### 3.3 A run's state

`running`, `succeeded`, `failed`, `cancelled`, plus `parked` when it waits on `wait` or `ask_owner` (already in the run row). The UI gets the state from the event stream and keeps the run list it read as a cache. A run that has no terminal event and an engine that says the workflow is closed is the engine's sweep to fix (#2999), not the UI's to guess.

## 4. The page

### 4.1 Start

`New workflow` opens one screen with three entries:

| Entry | What it does | Phase |
|---|---|---|
| Start from a template | a gallery of the 10 jobs of #2960 (A, B and C are three of them). Each card shows trigger, step count, "needs: GitHub connection" and the last time it ran on this box. Choosing one opens the workflow on its Test step with the checklist. | P1 |
| Blank | a workflow with an empty trigger slot, on "Add a trigger" | P1 |
| Describe it | one sentence in, a drafted graph out as a diff against blank; Sindri validates and test-runs it before showing "done". A secret never goes in this box. | P6 |

The gallery reads `GET /sindri/workflows/templates` (new; today the list is hard-coded in `Labels.WorkflowsEmpty.cs:64` and its comment says there is no route).

### 4.2 Build (steps 1 and 2)

The editor is the existing canvas plus a right-hand sheet. At 1440x900:

```
┌ top bar 40 ───────────────────────────────────────────────────────────────────────────────┐
│ Workflows › PR review             ● Off · Not tested        [Test run]  [Activate]        │ header 48
├ canvas ────────────────────────────────────────────────┬ sheet 440 ─────────────────────┤
│  ┌ PR opened ────────┐                                 │ Run an agent        step 2 of 3 │
│  │ github:me  repo x  │─▶┌ Review ──────────┐          │ ─────────────────────────────── │
│  └────────────────────┘  │ Run an agent       │─▶ ...   │ Which repo?        [ acme/api ▾]│
│                          │ ⚠ Pick a repo      │          │ What should it do?              │
│                          └────────────────────┘          │ [ Review PR / and the diff... ] │
│                                                          │ Which agent?       [ claude ▾ ] │
│  [ + Add step ]  N                                       │ Advanced ▸                      │
│                                                          │            [Cancel]  [Save step]│
├ N add · Enter open · T test run · ? all keys ──────────────────────────────────────────────┤ footer 28
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Add step** opens a picker of the catalog's groups (Triggers, Data, AI, Logic, Outputs). Choosing a kind opens the sheet for a step that does not exist yet. Save sends one `add_node` with the config and, when a step is selected, a `connect` after it, as one op list (the code already builds one: `Editor.AddNodeAfter`).
- **The sheet** shows the kind's required fields first, each with its caption and presets. A field refuses an empty value in place ("Pick a repo"), before anything is sent.
- **Connections.** A connection field lists the connected services with their scope and state ("github:me, read, ok" / "expired"). When none fits it shows "Connect GitHub"; that goes to Connections and returns here with the form kept (#3072 comment, #3001, ledger 14).
- **Change trigger.** The trigger step has "Change trigger" instead of a second Add. It sends one `replace_node` (decision 5).
- **An unfilled or invalid step** carries a ⚠ and its finding line on the canvas, never a silent skip (Retool's rule).
- **Only the step you are building asks for anything.** Nothing on the canvas is saved empty.

### 4.3 The trigger step

Below the trigger's fields: "Recent events" lists up to 10 events of this kind seen on this box (newest first, source-side time), each with a one-line summary. Enter picks one into the test input. Edit opens the payload as JSON in Advanced. The line under it says "would match" or "would not match: repo is not acme/api". With no events yet, the list says "None seen yet. Using the example." and shows the example payload (Pipedream's lesson: the shape is visible before any real event).

### 4.4 Fields from earlier output

In any step field, `/` opens a picker of upstream outputs. Each row shows the name, its type and the value from the picked event or the last run ("pr.title = fix: ..."). With no sample it shows the type. Picking inserts the reference. An unknown reference is underlined using the validator's `GRAPH-REF-*` findings. The raw `{{node.out}}` text stays under Advanced (Take 7, #3033).

### 4.5 Findings

`POST /sindri/workflows/validate` runs after each edit (debounced 150 ms) and before Test and Activate. A finding sits on its step (a chip with the id, the cause line and the fix line), and the header says "2 things to fix" with a button that jumps to the first. Activate's tooltip lists them. Catalog style follows actionlint: a stable id, a cause, a fix, one example input.

### 4.6 Keyboard and focus

Grammar from #2978: rows and steps lead with mono ids; a bare letter never commits a write; `Left` and `Right` fold; bare `g` is the shell's chord.

| Key | Does | Writes? |
|---|---|---|
| Arrows | walk the graph; the selected step is announced (#3073) | no |
| `N` or Ctrl+K | open Add step | no (opens a picker) |
| Enter | open the selected step's sheet | no |
| Esc | close the sheet or picker; nothing is saved | no |
| `F2` | rename the step | opens a field, Enter saves |
| `T` | open Test run (the input picker); Enter in it runs | opens a composer; the run is fenced |
| `1` | fit the graph | no |
| Ctrl+Enter in the sheet | Save step | yes, one op |
| Ctrl+Z | undo the last saved edit (a new revision restoring the old graph) | yes, one op |

Activate, Pause, Delete and Remove are buttons or the `.` panel, not bare keys.

### 4.7 The header

Title, then the state chip (decision 12), then the test fact ("Tested rev 4, passed 2m ago" / "Not tested" / "Rev 5 changed since the test"), then Test run and Activate. The header subtitle describes this workflow, not the list's count (#2970).

### 4.8 States

| State | What shows |
|---|---|
| Loading | the list paints from the local store in under 100 ms; no spinner |
| Empty (no workflows) | the three entries of §4.1 |
| Empty trigger | the canvas holds one dashed "Add a trigger" slot; the primary button says the same |
| Findings | ⚠ on the steps; header "2 things to fix"; Activate off |
| Untested | chip "Not tested"; Test run is the primary button |
| Testing | the canvas shows a dot per step as it runs (§7); the button says "Testing..." and Esc cancels |
| Test failed | the failed step is selected, its sheet shows the sentence and the one fix button (§7.3) |
| Tested | "Passed 2m ago"; Activate is the primary button |
| Active | chip "Active, next weekday 7:00"; Pause; Run now |
| Conflict | a stale-base edit (`SND-GRAPH-007`) shows "Changed elsewhere. Reload" and keeps the owner's open sheet text |
| Engine down | the page says serve is not running and keeps the last read; writes are off |

## 5. Test run

### 5.1 What is fenced

| Port | Test version | Run view shows |
|---|---|---|
| `INotifyPort` | records the call | "would have: notify 'Main gate red'" |
| `ICreateItemPort` | records, returns a made-up item | "would have: create item 'X'" |
| `IGraphHttpPort` non-GET | records | "would have: POST host/path" with the body |
| `IGraphHttpPort` GET | runs for real | the real response |
| `IConnectionToolPort` writes | records | "would have: comment on acme/api#12" |
| `IGraphCallPort` (`run_workflow`) | runs the callee as a test | the callee's test run |
| `IQuestionPort` | auto-answers with the first choice | "would have asked: ..." |
| `IGraphMemoryPort` writes, `IGraphSeenStore` | `only_new` reads the real store and writes nothing | "would remember: ..." |

Every "would have" row shows the exact payload so the owner can read what a live run would send.

### 5.2 Model and agent steps

By default `prompt` and `run_agent` return their pinned output, else the schema's example, so a test spends no model time and no worktree. That proves the wiring and not the model's answer. Two switches change it, both per test run:

- **With model** runs `prompt` steps for real (read-only, no side effects). Use it to catch #3079: the model answered text, not JSON.
- **With agents** runs `run_agent` nodes in a sandbox with no push and no write (engine's `--real-agents`).

The Test button has a small menu: Test (fenced), Test with model, Test with model and agents. A step with a pin shows a pin glyph; "Pin this output" is in the run page (§7).

### 5.3 What passing means

A test passes when validate passes and the walk ends `succeeded`. A step the test could not check says so in its row ("model not run: output taken from the example"). The tested fact on the header names the mode: "Passed (fenced)" or "Passed (with model)". Activate accepts either; the header shows which.

## 6. Errors, in words

### 6.1 One table, one test per row

Each failure code maps to a sentence and one button. The mapping lives in one catalog file in Core (not in the UI), so the CLI and MCP print the same words. One test per row asserts the sentence and that the named action exists.

| Code | Sentence | Button |
|---|---|---|
| `GRAPH-CONFIG-001` | "Check needs a repo." | Open the step |
| `GRAPH-NAME-001` (once #3001 ships) | "github:home is not a connection on this box." | Connect GitHub |
| `GRAPH-NAME-002` (today a warning) | "Couldn't check the connection yet." | Retry |
| `GRAPH-TRIGGER-001` | "A workflow has one trigger." | Change trigger |
| `SND-GRAPH-103` | "The model answered with text, not the JSON this step asks for: "Usage limit reached ..." (first 200 characters)." | Open the raw answer; Retry with model |
| `SND-LAUNCH-003` (#2986, should be caught earlier) | "No agent is chosen for step Review and the profile has none." | Choose an agent |
| `SND-GRAPH-105` | "This workflow is not on." | Activate |
| `SND-GRAPH-007` | "Changed elsewhere." | Reload |

### 6.2 The prompt step

The block checks the exit code and the text before it parses. Exit code non-zero or an answer that does not start a JSON value becomes `SND-GRAPH-103` with the `raw` text kept on the node run. The stock template's first run must pass (W4).

## 7. The run page

### 7.1 Layout

The run page is the canvas with a status dot per step, and a bottom panel for the selected step. At 1440x900: header 48, canvas 520, panel 216.

- **Header.** `<workflow> › run 3` (mono id left of the title), the run's state chip from the event (decision 4), mode (Live / Test / With model), trigger summary, "Started 4m ago · took 14s".
- **Dots.** Queued, running (arc), succeeded, failed (x), skipped (grey), parked (pause), would-have (dashed ring). Colour is never the only signal.
- **Panel.** Tabs: Input, Output, Raw answer (only when a model step kept text), Error, Time. Input and Output are the stored values (`NodeRunView.Input`, `.Output`, cut at 64 KB with a "truncated" badge). Large payloads fold. For a test run the panel also shows "would have" rows.
- **Pin.** "Pin this output" on a succeeded step saves it as `test.pins[<node>]` for later test runs.

### 7.2 State and age

The run list and header read `workflow.run.*` events. They update in place; no page reload. A run row is "Running" only while the engine has not closed it. "N ago" uses the shared clock. If the event stream is down the header says "Not live. Last read 14:02" and the list reads again every 15 s.

### 7.3 A failed run

The header sentence comes from the §6.1 table. Under it, one button chosen from the cause. Raw codes stay in "Details". **Re-run from here** (ledger 9) is later (P7): it restores the earlier steps' stored outputs, re-runs this step and the ones after it, and links the new run to the old one. A step that already made an object shows "done before: comment #5521" and skips it by default, with "do again" as an explicit toggle (ledger 12).

## 8. Agents

Every action has a CLI verb and an MCP tool. A session may run `wf validate`, `wf test`, `wf create`, `wf edit` and read runs; it never presses Activate or Pause for the owner (`SND-AUTH-002`, as for profile writes).

| Action | CLI | MCP tool |
|---|---|---|
| Validate a revision | `sindri wf validate --slug S [--revision N]` | `workflow_validate` |
| Test run | `sindri wf test S [--revision N] [--trigger f.json \| --trigger-event ID] [--outputs f.json] [--with-model] [--real-agents]` | `workflow_test` |
| Replace the trigger | `sindri wf edit S --base N --ops '[{"op":"replace_node",...}]'` | `workflow_apply_ops` |
| Recent events of a kind | `sindri wf events --kind K [--connection C]` | `workflow_events` |
| Templates | `sindri wf templates` | `workflow_templates` |
| Pin an output | `sindri wf pin S --node X --run R` | `workflow_pin` |

Text from a run's output, a PR title or a model answer is fenced as untrusted in every result.

## 9. API and engine requirements

| # | Need | Owner lane | Notes |
|---|---|---|---|
| E1 | Run `mode` (`live` or `test`) on the run row; test versions of the write ports; `workflow.tested` per revision; `test.pins` per revision | engine | #2969's engine plan, as meta accepted it; order: #2985 first, then ports and mode as one PR, then model and agent stubs |
| E2 | `POST /sindri/workflows/{slug}/test` with `{revision?, trigger?, triggerEvent?, outputs?, withModel?, realAgents?}`; MCP `workflow_test`; `wf test` | api | the UI button waits on it |
| E3 | `replace_node` op in `GraphOp` / `GraphOps` (`Core/Graph/Ops/GraphOp.cs`): same slot, keeps outgoing edges, one validate | engine | slot = trigger now; other kinds refused |
| E4 | Prompt block: check exit code and text before parse; keep `raw` (cut at 64 KB) on the node run; `NodeRunView` gains `Raw` | engine, api | #3079 |
| E5 | Every `Title` is the only display name; `NodeSummaryView` carries `kindTitle`; sheet titles come from the step title | engine, ui | #3074; the node id never goes to a label |
| E6 | `GET /sindri/workflows/trigger-events?kind=&connection=` (last N with a one-line summary, the example, the would-match check); MCP and CLI | api | #3032; the forge poll keeps the last 20 real events per connection (#2988's engine comment) |
| E7 | `GET /sindri/workflows/templates` (the 10, each with trigger, step count, needs, last run) | engine | replaces the hard-coded list |
| E8 | Event kinds `github.pr.opened`, `.updated`, `.ready_for_review`, `.review_requested`, `.review_submitted`, `.comment`, `.checks`, `.conflict`, each with a payload schema and a sample; an issue-labelled kind for B | api, engine | #2988; §12 gap 1 |
| E9 | Failure-to-sentence catalog (§6.1) in Core | engine | one test per row |
| E10 | The holes that make "valid" not mean "will run": #2985, #2986, #2987, #3001, #2999 (sweep for stuck `running`), #3003, #3005 | engine | W7 depends on them |
| E11 | Validate returns findings with node id, stable id, cause, fix | engine | the route exists; the shape is confirmed in P0 |
| E12 | Ui subscribes to `workflow.run.*` (payloads: `WorkflowRunFinishedPayload(RunId, Status, Error, By)`) | ui | no new route |

## 10. Goals and non-goals

Goals:

- A, B and C reach a passing test run from zero in 4 steps (W1 to W3).
- Add step, Change trigger and Test run all work from the keyboard.
- The first live run behaves as the test did.

Non-goals (this spec):

- A visual redesign of the canvas. Design owns the frames and tokens (#2978).
- Revision list, side-by-side diff and named revisions (ledger 11). Revisions exist; undo (Ctrl+Z) is the only part here.
- A public template gallery. The 10 are ours.
- Webhooks. The box is loopback only; events come from the forge poll (#2988).
- A cloud or shared workflow store.
- Re-running a live run's side effects. Re-run from here skips what already ran.

## 11. What happens to today's views

| Today | After |
|---|---|
| `GraphEditViewModel.AddStep` sends the empty step | P1: it opens the sheet first and sends one op on Save |
| Workflows list and Runs views (`Views/Workflows/WorkflowsPage.axaml`, `RunsViewModel`) | P2: they read events and keep state per run; the page stays |
| The "Start from" dropdown with its fixed list (`Labels.WorkflowsEmpty.cs:64`) | P1: the three-entry start screen |
| `WorkflowEditorSheet.axaml` | stays; it becomes the step sheet of §4.2 |
| `GraphCanvas` (one automation element) | P1: each step is an element (#3073) |
| Run now | stays, for a live run of an active workflow; Test run sits next to it |

## 12. Gaps found

1. **B needs an event #2988 does not list.** The list is all pull request kinds. "Issue labelled" has no kind. Recommend `github.issue.labeled` with `label` in its payload, polled from the issues feed (§open question 3).
2. **The test run and the "will run" promise can drift.** If validate and the test use different checks, a pass can still fail live. The test calls validate first; W7 is the property test.
3. **A stubbed model hides #3079-type bugs.** Default test mode does not call the model. "With model" exists for that, and Activate accepts either, so a passing fenced test can still meet a model that answers text. The header names the mode (§5.3); open question 2 asks whether Activate should insist on "with model" when the workflow has a Prompt step.
4. **A draft has no "unsaved" state.** Every sheet Save is a revision. A long edit makes many revisions. Acceptable now; a "save as one revision" grouping is a later idea.
5. **The ledger measured by reading docs.** Its step counts for A are inferred (its "Weakest claims"). W1 is measured in our app, not copied from them.
6. **GRAPH-NAME-002 is still a warning** (connection not checked, #3001). Until it is fixed a wrong connection name passes the test as "could not check". The test row says so.
7. **Home's run failures** (Home spec, `run-failed` row) need the unseen flag from the same store E1 touches. Do not add a second one.

## 13. Proof

Proof is the app run on seeded data with `aw-ui-driver`, not tests alone.

1. **Walk A, B and C from a fresh home** (`scripts/context-first-run.sh`, forge fixture `scripts/forge-fixture.sh`). Capture before and after shots of each of the 4 steps. Count the steps and the seconds. This is W1 to W3.
2. **Template matrix** in CI: each of the 10 templates, fake model returning the example, then plain text (W4).
3. **Add step matrix**: 10 kinds, cancel / invalid / valid (W5).
4. **Fence test**: A, B and C against the fakes; assert no write (W6).
5. **Event state**: start a run, stop the stream, resume; the row corrects within 1 s of the event or the next read (W8).
6. **Name audit**: one test over the catalog (W11).
7. **Screen reader**: `aw-ui-driver tree` lists each step with its name (W12).
8. **Adversarial design review** by the ideas lane on the shots (owner rule, 2026-11-10), judged against inspora.design.

Perf: W9 and W10 measured in the real app by the perf lane and logged in the perf inventory.

## 14. Phases and parallel plan (for /specToProvenPR)

Phases are the PRs. Every lane has work from day one; the red and stubs of each phase land first, never behind another phase's PR.

| Phase | What | Lane | Depends on |
|---|---|---|---|
| P0 | Contract PR: the new routes' request and response records, the `replace_node` op, `NodeRunView.Raw`, the findings shape, golden and `PublicAPI` entries, all as stubs | api | none |
| P1 | Build: the sheet-first Add step (#3072), Change trigger (#3075), `GraphCanvas` elements (#3073), names (#3074), start screen with the 10 templates | ui | P0 stubs |
| P1e | `replace_node`, the Title rule, the templates route, the block checks for the 10 templates | engine | P0 |
| P2 | Live state: event subscription and shared clock (#3080); chip and header (#2970) | ui | P0 |
| P3 | Engine truth: #2985, #2986, #2987, #3001, #2999, #3003, #3005 | engine | none (start now) |
| P3e | Prompt block exit-code check, `raw`, sentence table (#3079, §6) | engine | none |
| P4a | Test mode, write-port recorders, `workflow.tested`, pins | engine | P3 (#2985 first) |
| P4b | `POST .../test`, `workflow_test`, `wf test`, trigger-events route, `wf events` | api | P4a |
| P4c | Test run UI: the button and menu, the run view with "would have" rows, the Activate gate (warn, then refuse) | ui | P4b stubs |
| P5 | GitHub kinds and samples (#2988, #3008), issue-labelled kind; the connection field with "Connect GitHub" | api, engine | P0 |
| P5ui | Trigger step events picker (#3032), field picker (#3033), Prompt output as a form (#2968), flip `workflows.editor` default | ui | P4b, P5 |
| P6 | Describe it; delete the old editor path | ui, engine | P5ui, one week of green W1 to W3 |
| P7 | Re-run from here; undo | engine, ui | P4a |
| X1 | Design: frames for §4.2, §4.8, §7.1 in the Inspora bar; token names for the dots | design | none |
| X2 | UX: the walk of A, B and C at each phase's end; filing every miss as an issue `Part of #2978` | ux | each phase |
| X3 | Perf: W9, W10 in the real app; the canvas at 200 steps | perf | P1, P2 |
| X4 | QA: judge each phase against the owner's bar, the shots and W1 to W13 | qa | each phase |
| X5 | Tools: the aot-smoke lines, `.env.example`, scaffolds and docs (`docs/csharp/sindri-graph.md`, the block pages) | tools | each phase |
| X6 | Context: the context map's workflow concepts (a workflow, a run, a step) so agents find them | context | none |

Day one: api P0; engine P3 and P3e (no dependency); design X1; ux X2 baseline walk; ui P1 on the P0 stubs; context X6. Nothing waits on a dependency PR to start its red.

Gate notes: P1 to P5ui add catalog rows, action ids and API goldens, so each gate runs the Sindri integration lane and the Ui goldens, as the team rules say. A PR with a doctor check change updates `src/Sindri/aot-smoke/expected.txt`.

## Open questions

Each has a recommended answer; the lead decides.

1. **Refuse Activate until a passing test, or warn only?** Recommend refuse from P4c (meta's contract on #2969), with no override: the owner's bar is "guaranteed to work", and the test costs one click.
2. **Should Activate insist on "With model" when the workflow has a Prompt step?** Recommend no. A fenced pass plus the first-run header ("First live run: watch it") is enough, and "With model" is one menu click for the owner who wants it. Revisit if W7 misses.
3. **Which issue event for B?** Recommend `github.issue.labeled` with `{repo, number, label, author}`, polled like pull requests, plus `github.issue.opened`. Engine and api own the shape.
4. **Where do the 10 templates come from?** Recommend the 10 jobs listed in #2960, each a spec file in the repo under `src/Sindri/...` with its first-run checklist, loaded by E7. A, B and C ship first.
5. **Does Describe it use the owner's agent or a fixed one?** Recommend the profile's `workflow.agent.target` (#2986), and refuse with the fix line when it is unset.
6. **Does the test count toward Activate when the revision changed after it?** Recommend no: any new revision is "Not tested", because validate changed too.
7. **Pins on a revision or on the workflow?** Recommend per revision (engine's note on #2969); a later "carry pins to the new revision" prompt if it hurts.

### Review notes

Round 1: not run yet.
