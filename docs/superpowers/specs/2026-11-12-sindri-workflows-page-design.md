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

The job: **from nothing to a proven, running workflow in 4 steps: pick a trigger, add an action, test it, turn it on. The test uses real or sample input, spends no model time unless asked, and sends nothing. When it passes, the wiring and the connections are checked; the model, the agent and the ping were not run, and the first live run is watched.**

## 1. The bar

"Ledger N" is problem N in `docs/sindri/prior-art/workflows.md`. "Take N" is item N of its closing list "What we take". The ledger's "Unconfirmed / thin" section is not used as a source: no claim below leans on wrkflw's `--dry-run`, Pipedream (shutting down 2027-03-31) or the gallery counts it could not read.

| Step of the loop | Ledger | Best tool today | What we take | Here |
|---|---|---|---|---|
| Start from zero | 1 | Zapier one-decision-per-screen, Retool unconnected warning | trigger, action, test, on; an unfilled step is a visible warning, never a silent skip | §4.1, §4.2 |
| Start from words | 2 | n8n Assistant, Windmill compare view | Describe it makes a skeleton, shown as a diff, validated and test-run before "done" (Take 3) | §4.1, phase P6 |
| Start from a template | 3 | n8n, Actions starters | a template is a full spec plus a first-run checklist; the gallery holds A, B and C in v1; the other jobs of #2960 follow after the soak | §4.1 |
| Pick a trigger, try it on a real event | 4 | Pipedream, Zapier, Inngest | last 10 real events of the kind, pick, edit, would-match line (Take 4, #3032) | §4.3, §6 |
| Fill a field from earlier output | 5 | n8n drag, Windmill suggest | `/` opens a picker of upstream outputs with real values (Take 7, #3033) | §4.4 |
| Check before on | 6 | actionlint | a stable id per finding, shown on the step; Activate off while a finding blocks; untested only confirms (Take 1) | §4.5 |
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

**The bar in one line:** each of A, B and C reaches a passing test run from an empty scratch home in at most 4 steps (pick the template, fill its fields, test, turn it on) and under 3 minutes, with the GitHub connection already made; the test sends nothing; a run that ends changes on screen within 1 s; and "passed" means the same checks Activate runs.

Counting rule. A "step" is a stage of the loop, not a click. The clock starts on the empty home with a GitHub connection present; making the connection is measured on its own and reported next to W1 to W3 ("Connect GitHub" must take under 1 minute). Built from blank instead of from the template, A takes 5 operations, B 5 and C 6; ux times that path too and reports it, but the bar is the template path.

Done-checks. Each is a test or a measured run in the real app (`aw-ui-driver`). Each names its lane in §14.

| # | Check | Measured as |
|---|---|---|
| W1 | **The ux walk, A.** Trigger "PR opened", action "run an agent to review it", action "tell me", test, activate. | From a fresh `context-first-run.sh` home with a GitHub connection: from the A template, at most 4 steps (pick, fill, test, activate) and 3 minutes to Active with a passing test run. The fill step is the repo only: the agent field opens prefilled from `workflow.agent.target`, or with the first installed agent when that is unset. When no agent is installed at all, the agent field is the second thing left and the walk takes 6 actions. Built from blank, the 5 operations are timed and reported, not gated. Today: cannot be built. |
| W2 | **The ux walk, B.** Trigger "issue labelled", a Prompt step that writes a plan, a "comment on the issue" step. | Same measure. Needs an issue-labelled event (§12 gap 1). |
| W3 | **The ux walk, C.** Schedule, a check, an If step, create an issue on red. | Same measure. |
| W4 | Every stock template passes its first test run. | CI runs each template twice: with a fake model that returns the schema's example, and with one that returns plain text. The second case ends with a sentence and the first 200 characters of the answer (§6.2), never a parser message. |
| W5 | Add step never saves an invalid step. | For each of the 10 kinds: pick it, the sheet opens, Cancel adds nothing (revision unchanged), Save with an empty required field is refused in the sheet and sends nothing, Save with the fields filled sends one `add_node` and the count goes up by 1. |
| W6 | Test run sends nothing. | A test run of A, B and C against the fakes: zero notifications, zero HTTP writes, zero created items, zero agent launches, zero model calls (default mode); a second case with agents on runs `run_agent` against the fakes and asserts no push and no write. The run view lists each as "would do". |
| W7 | A pass means the wiring holds. | Property: a revision that passes validate (preflight included) and has a passing test run never fails its first live run on a missing connection, repo, token scope or agent target (#2985, #2986, #2987, #3001 are the red tests). |
| W8 | Run state is true. | A run ends, the row and header change within 1 s (p95 over 50 runs on the fakes); "N ago" ticks; no run stays "Running" after the engine closes it (#2999 sweep). |
| W9 | First paint under 100 ms from the local store; key to visible change under 50 ms. | Window open to the workflow list painted, 200 workflows; keydown to repaint. |
| W10 | Run page opens fast. | A run with 50 steps opens in under 200 ms (ledger 8). |
| W11 | One name per step kind. | A test walks every kind: menu label, canvas subtitle, sheet title and run row use the catalog `Title`; no node id or type id appears outside the JSON view. |
| W12 | Screen reader. | Each step is its own element named "Check, Prompt step, 2 of 4"; moving the selection is announced (#3073). |
| W13 | Agent parity. | A test lists the page's actions; each has a CLI verb and an MCP tool; results fence repo text. |

## 2. Decisions

1. **A step opens its form when you add it and is saved only when valid.** Pick Add step, pick a kind, the settings sheet opens with that kind's required fields and presets (#2891). Save sends ONE `add_node` with the filled config. Cancel adds nothing. *Why:* the server keeps checking required settings on every edit, so a saved revision always runs; the fix is in the editor only, in the shared view model, so both layouts get it. This is meta's decision on #3072 and fixes it.
2. **Test run works on a draft.** A draft is the newest revision when it is not the active one. Test run needs no Activate. It takes a sample input or a picked real recent event, and it fences every effect step (§5). *Why:* the first try must not be a live run (fixes #2969). Meta accepted engine's split on that issue.
3. **The run page shows each step's input AND output, and the model's raw text when the JSON parse fails.** The node run keeps the first 64 KB of the raw answer when `SND-GRAPH-103` fires, and the sentence quotes the first 200 characters (fixes #3079's second and third ask).
4. **A run's state comes from the engine's terminal event, and "N ago" ticks.** The page reads `workflow.run.started`, `workflow.run.finished` and `workflow.run.cancelled` from the event stream and repaints the row and header within 1 s. One shared clock (1 s under a minute, 30 s after) drives every "N ago" (fixes #3080).
5. **Replace the trigger is one op.** A new graph op `replace_node` swaps a node for another of the same slot, **keeps the node id and its outgoing edges**, and is checked as one edit. The trigger slot offers "Change trigger" (fixes #3075). *Why:* `GraphOps.Apply` already applies an op list atomically (`Core/Graph/Ops/GraphOps.cs`), so remove, add and connect in one list is atomic today; what that list loses is the node id, so every `{{trigger.*}}` reference downstream breaks. `replace_node` keeps the id; a reference to an output field the new trigger lacks becomes a `GRAPH-REF-*` finding, not a silent break. Draft-mode validation blocks `GRAPH-REF-*`, so `replace_node` marks the reference findings its swap causes as wiring gaps for that one edit (the edit saves, the step shows the warning), and a red test swaps A's trigger while step 2 reads `{{trigger.pr.title}}`. A non-trigger node is refused with `SND-GRAPH-112` (110 and 111 are taken on main; 112 to 119 are free there at 6b647dbb58, P0 confirms).
6. **One word for the If step everywhere: "If".** The catalog `Title` (`ConditionBlock.cs:16`) is the only name shown. The canvas subtitle, the sheet ("Settings of Flagged?", the step's own title) and the run row use it. Type ids and node ids show only in the JSON view. Same rule for "Send notification". *Why:* the menu already says "If", and "condition" and "gate" are a type id and a node id (fixes #3074).
7. **Four steps, in this order: trigger, action, test, activate** ("turn it on" in prose; the button, chip, confirm and CLI all say Activate). The page's primary button follows the step the workflow is at: "Add a trigger", "Add an action", "Fix 2 things" (singular "Fix 1 thing") while findings stand, "Test run", "Activate". One decision per screen region, as Zapier does (Take on ledger 1).
8. **Activate needs every validate finding gone; a passing test run is advised, not required.** An untested revision shows "Not tested" on the workflow row and the header, and Activate opens one confirm: "Not tested yet. Activate anyway?" (Enter / Esc), with a second button, "Test run first", that runs the missing test and returns to the confirm (§5.3). After any passing test Activate is one click (§5.3). *Why:* a cron or a PR trigger may have no sample to test with, and a hard gate with no override strands those; the bar is building trivially easy. Activate sends the revision number the owner saw, and the server refuses (`SND-GRAPH-007`) if a newer one exists. A session never presses Activate or Pause (§8). Settled (lead, 2026-11-12), open question 1.
9. **The test run is fenced by block, not by promise.** The block catalog says which blocks write; a test version of each write port records the call and returns a made-up result (§5). Model and agent steps are stubbed by default and opt in per run (§5.2).
10. **Raw JSON is for the Advanced fold.** Fields show captions (#2925), presets, and the `/` picker. A Prompt step's output is a small form of named fields, not JSON Schema typed by hand (#2968).
11. **The trigger step lists real events.** The last 10 events of its kind that Sindri saw on this box; pick one, edit it, pin it; a line says "would match" or "would not match: repo is not x" (#3032).
12. **State is a chip.** "Active, next weekday 7:00" or "Off" beside the title, the same word and glyph as the list row (#2970).
13. **Agents get the same verbs.** Every action here has a CLI verb and an MCP tool; a session reads and tests but never presses the owner's Activate (§8).
14. **The old canvas-only editor stays until P5.** The new profile key `workflow.editor` (`sheet` | `steps`; added to the profile schema in P1, a field on `WorkflowSettings`, since the profile section is `workflow` and has no JSON schema; set with `sindri profile set workflow.editor steps`, a doc row, error text lists both values) picks the canvas layout only. The three bug fixes (sheet-first Add step #3072, Change trigger #3075, step names #3074) live in `GraphEditViewModel` and ship in BOTH layouts from P1, so a default user never saves an empty step. P1 to P5 ship `sheet` as the default; P5ui flips it; P6 deletes the old path after one week with no miss of W1 to W3. W1 to W5 run on `steps` and W5 also runs on `sheet`. After P6 the key is accepted and ignored for one release.
15. **Delete asks once, keeps the runs, has no undo (§4.9, #3306).** The confirm names the run count and the resource size. Past runs stay readable. The workflow's resources are deleted. An active workflow must be paused first, and a run in flight blocks the delete. *Why:* `DeleteWorkflowService` already archives: it keeps every revision and run and clears `active_revision` (`WorkflowSql.Archive`). Only the page, the refusals and the resource purge are new. Meta's pick, 2026-11-12.
16. **After Save the page shows the newest revision, and Run now names the revision it runs (§4.10, #3303).** On an active workflow the header says "Revision 2 saved · revision 1 is still active · Activate". *Why:* on main the sheet snaps back to the active revision's text and Run now runs revision 1 without saying so, so a fix looks lost and the rerun proves nothing. Meta's pick, 2026-11-12.

## 3. The model

### 3.1 The draft and its checks

There is no new store for revisions. A workflow already keeps immutable revisions (`docs/csharp/sindri-graph.md:48-51`: every save is a revision, `Activate` pins the one triggers run, a stale base is `SND-GRAPH-007`). Four facts hang off a revision:

| Fact | Where it lives | Set by |
|---|---|---|
| `findings[]` | computed on read by `POST /sindri/workflows/validate` (no write) | validate; each finding has a stable id, the node it belongs to, a cause line and a fix line |
| `tested` | derived: the newest `workflow_run` row with `mode = test`, this revision and `status = succeeded`; no new event, because the run row already holds revision, mode and status | a test run that ends `succeeded` |
| `pins` | a `workflow_pin` table keyed (workflow, revision, node) holding the output, written by a `workflow.pinned` ledger event (who, run, node). Not part of the revision's `spec_hash`, so pinning never makes a new revision and never un-tests it. A pin of an output the store cut at 64 KB (`SND-GRAPH-108`) is refused; a value `Aw.Redaction` blocks is refused too (it catches secret shapes and SSNs, not emails or free text, so the pin sheet says "don't pin personal data"). The store writes the table; the event carries who, run and node, never the output (the append-only events table must not hold a pin forever). Replace = pin again; remove = `wf unpin` / `DELETE .../pins/{node}`. **Carry-forward rule (lead's call, 2026-11-12, refining answer 7):** a pin carries to the next revision only when that node's type and config are byte-identical in canonical JSON. An upstream change does not drop it. The pin records the revision it was taken on, and the run page shows "pinned on rev N". Every Save is a revision, so without this a pin would vanish at the first edit. The header counts pinned steps and names who pinned them, so a session's made-up pin is visible | the owner pins a step's output, or a test request sends it |
| `active` | the pin `Activate` writes | Activate |

Why not a column on the revision: revisions are immutable and hashed, and a pin must change after the revision exists.

The page's step is derived, not stored:

1. no trigger -> "Add a trigger"
2. a trigger, no other step -> "Add an action"
3. any finding -> "Fix N things" (opens the first)
4. no passing test of this revision -> "Test run"
5. tested and off -> "Activate"
6. active, and a newer revision is saved -> "Activate rev N" (§4.10)
7. active -> "Active, next <time>"

### 3.2 A test run

A test run is a run row with `mode = test`. It follows the real graph walk (`GraphWorkflow.Walk`), so it checks the same wiring. Differences from a live run:

- The trigger is a payload, not an event. The payload is a picked real event, a hand edit of one, or the trigger block's schema example when nothing was seen.
- Every write port is replaced by a recorder (§5.1).
- `prompt` and `run_agent` return the pinned output, else the schema example (§5.2).
- **Run row.** `mode` (`live` | `test`) is a new column on `workflow_run`, the one place that already holds revision and status (a ledger migration, numbered at final merge, plus the `ledger schema N/N` line in `src/Sindri/aot-smoke/expected.txt`). A test run's id stays numeric, `slug@rev:N`, with N in its own band above the nine-digit space that live runs use. On main, `CallOccurrence` and `DeliveryOccurrence` hash into 100,000,000 to 999,999,999 and `WorkflowRunId.Shape` allows nine digits, so no free nine-digit band exists (a test id inside it could equal a polled event's id and drop that event as a duplicate, `SND-GRAPH-100`). So P0 widens `WorkflowRunId.Shape` (and the two `NeedsYouRowId` alternatives) to ten digits, and the int ceiling 2,147,483,647 sets the bands: tests started by a person or an agent count from 1,000,000,000 (`test 1` = 1,000,000,000), allocated by a store call `NextTestOccurrence(slug, rev)` that reads `mode = test` rows only; a test callee started by `run_workflow` hashes into 2,000,000,000 to 2,147,483,647. `WorkflowRunId.Of` refuses a number outside its band. Nothing infers "test" from the number: the `mode` column carries the meaning, and the UI, CLI and MCP print "test N" (N = occurrence minus 999,999,999 for the counter band; `Labels.WfRunTitle` changes). Every `TryFrom` caller (cancel, answer, take-over, the run page, `workflow_result`) keeps working, and a test never burns the manual counter or collides with manual run 1. Test rows are capped: when a test starts, test rows of that workflow beyond the newest 50 are deleted.
- **Readers of run rows.** The admit path (`Admit`, `Decide`, `PromoteOldestQueued`) counts live rows only, so a test never takes a `one-per-workflow` slot or queues behind a live run. `InFlight` the method keeps counting both, because `PurgeResourcesService` must not delete resources under a running test (a separate admit-only filter). `GraphRunResults.LatestSucceeded` and `ById` (`workflow_result`), `LatestOfKind`, both `LatestOccurrence` callers (`RunWorkflowService`, `WorkflowScheduleService`, whose floor would otherwise jump to year 2346 and stop the schedule) and the `RunItemRepair` / `RunItemLinker` passes (they scan the newest 200 runs and would write `item.linked` rows for a made-up item; the test `create_item` output carries no `itemId`) skip test rows. Every skip is a `mode` filter in the SQL, never a post-filter, because `Recent` pages 50 rows and `LatestSucceeded` reads 1,000. `ListWorkflowRunsService` defaults to Live with a "Tests" toggle (filtered in SQL). `InMemoryWorkflowRunStore` and `WorkflowRunStoreContract` change in the same PR as the SQL. `GraphRunRecovery` still sees test rows and fails a lost test with `Mode = test`. `GraphWorkflowCaller` starts a test callee in the test range at the callee's newest revision. The Health and Shell workflow counters filter by mode. A test never counts as the workflow's last real run (a new field on the list, with its E row).
- **Where `mode` travels.** `Mode` is a required constructor parameter, no default, on `WorkflowRunStartedPayload`, `WorkflowRunFinishedPayload` and `WorkflowRunCancelledPayload`, so the compiler finds every site (`GraphMirror`, `GraphRunStarter`'s launch-failure path, `GraphRunRecovery.Record`, `GraphRunActivities.FinishAsync`, `CancelWorkflowRunService`). The public event form becomes `WorkflowRunPublic(RunId, Status, Mode)` so the page reads state from the event (decision 4); `EventCatalog` golden and `EventCatalogTests` change. `WorkflowRunRow` and `RunsProjection` gain `Mode` (a `ViewSchema.Version` bump and a rebuild); `WorkflowRun`, `WorkflowRunMapper`, `WorkflowRunSql` and the run detail and summary responses carry the column. `NeedsYouProjection` creates no failure row on main, so it needs no filter; Home's `run-failed` query reads `mode = live` only (the one unseen flag, §12 gap 7).
- The row also stores `test_options` (with model, with agents, the pinned nodes and who pinned them) in the same migration, so a reload and `wf test --json` can say how the test ran. `tested` means the newest test of this revision succeeded; a later failed test clears it.
- `run_workflow` in a test run runs the callee as a test at the callee's newest revision, not its active one.
- It runs validate first, so a test cannot pass what Activate would refuse.

### 3.3 A run's state

`running`, `succeeded`, `failed`, `cancelled`, plus `parked` when it waits on `wait` or `ask_owner` (already in the run row). The UI gets the state from the event stream and keeps the run list it read as a cache. A run that has no terminal event and an engine that says the workflow is closed is the engine's sweep to fix (#2999), not the UI's to guess.

## 4. The page

### 4.1 Start

`New workflow` opens one screen with three entries:

| Entry | What it does | Phase |
|---|---|---|
| Start from a template | a gallery of A "Review every PR", B "Plan a labelled issue" and C "Watch main, open an issue on red" in v1 (the other jobs of #2960 come after the soak, not in v1). Each card shows trigger, step count, "needs: GitHub connection" and the last time it ran on this box ("never" when it has not). Choosing one opens the workflow with the sheet of its first unfilled required field open and focused (most often the repo), and the checklist names what is left: one line per unfilled required field, then Test run and Activate, headed "N things left" ("1 thing left" for A: the agent field is prefilled from `workflow.agent.target`, or with the first installed agent, so only the repo is left; the header counts findings only). When nothing is left, Ctrl+Enter saves and the primary becomes Test run. From Start to a passing test: pick the template, pick the repo, Ctrl+Enter, `T`, Enter (5 actions, because the agent is prefilled; with no agent installed it is 6). The card keeps "needs: GitHub connection" and does not list an agent. | P1 |
| Blank | a workflow with an empty trigger slot, on "Add a trigger" | P1 |
| Describe it | hidden until P6 (no dimmed entry before then). One sentence in, a drafted graph out as a diff against blank; Sindri validates and test-runs it before showing "done". A secret never goes in this box. | P6 |

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
- **Change trigger.** The trigger step has "Change trigger" instead of a second Add. It sends one `replace_node` (decision 5). The button sits in the trigger sheet's head and opens a menu of trigger kinds; after a pick, a band in the sheet names each reference the new trigger lacks and says Save keeps the step with a warning. The empty trigger slot opens the same list in a search box, grouped GitHub, Time and Other.
- **An unfilled or invalid step** carries a ⚠ and its finding line on the canvas, never a silent skip (Retool's rule).
- **Only the step you are building asks for anything.** Nothing on the canvas is saved empty.
- **Motion and density** (X1 frames): a dot change 120 ms, the running arc turns in 900 ms, the sheet opens in 160 ms with a 16 px slide and closes in 120 ms; text never animates. Step card 64 compact / 72 comfortable, panel tab bar 32 / 36, field control 28 / 32, on the shared `home.density` key.
- **Colour.** Text on these pages is ink-subtle, never ink-3, and connectors are `--wire` at its #3138 value, `#686870` (3.61 on the canvas; the old `#4a4a52` is 2.27). ink-3 text is 4.49 on the canvas and 3.40 on a selected row. Until #3138 lands, the page's own connector brush uses `#686870`.

### 4.3 The trigger step

Below the trigger's fields: "Recent events" lists up to 10 events of this kind seen on this box (newest first, source-side time), each with a one-line summary. Enter picks one into the test input. Edit opens the payload as JSON in Advanced. The line under it says "would match" or "would not match: repo is not acme/api". With no events yet, the list says "None seen yet. Using the example." and shows the example payload (Pipedream's lesson: the shape is visible before any real event).

### 4.4 Fields from earlier output

In any step field, `/` opens a picker of upstream outputs. Each row shows the name, its type and the value from the picked event or the last run ("pr.title = fix: ..."). With no sample it shows the type. Picking inserts the reference. An unknown reference is underlined using the validator's `GRAPH-REF-*` findings. The raw `{{node.out}}` text stays under Advanced (Take 7, #3033).

### 4.5 Findings

`POST /sindri/workflows/validate` runs after each edit (debounced 150 ms) and before Test and Activate. A finding sits on its step (a chip with the id, the cause line and the fix line), and the header says "2 things to fix" with a button that jumps to the first. Activate's tooltip lists them. Validate also runs a **preflight** for what a fenced test cannot prove: the connection exists and its token can do what the write steps do (for B, comment on issues), the repo is one the connection can see, and the agent target exists. For GitHub the token check reads the repo's `permissions` object (`GET /repos/{owner}/{repo}`) for the connection's token (a guess to confirm in P5: `McpToolInfo` has no scope field today); where a connection cannot say, the finding is the warning "Couldn't check". Each failed check is a finding (`GRAPH-PREFLIGHT-*`); a check the connection cannot answer is a warning "Couldn't check" (as `GRAPH-NAME-002`), shown in the Test result. This is what makes "passed" mean "will run" (W7). Catalog style follows actionlint: a stable id, a cause, a fix, one example input.

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
| Ctrl+Z | the shell's toast undo: Save shows "Step saved. Undo (Ctrl+Z)", and while that toast is up Ctrl+Z restores the previous graph as a new revision. No toast, no page-level Ctrl+Z (the `.` panel has "Undo last edit") | yes, one op |

Activate, Pause, Delete and Remove are buttons or the `.` panel, not bare keys (Delete: §4.9). Activate has the canvas key Ctrl+Shift+A, which activates in one click, and opens the confirm for an untested revision. Bare `N`, `T` and `1` fire only when the canvas has focus, never inside a field. Ctrl+K opens Add step only on this page; it yields to the shell's command palette when the shell owns focus.

**Focus contract.**

1. Opening a sheet puts focus on its first empty required field; Esc or Save returns focus to the step that opened it.
2. A test that ends moves focus to the first failed step, or, when it passed, to the result strip ("Passed. 3 things would have happened. Review what it would send"), whose link opens the "would have" rows; Activate stays one Tab away and one click; the result is announced once through the live region ("Test passed, 4 steps" / "Test failed at Review").
3. The untested revision's "Activate anyway" confirm traps focus; focus starts on the default button (Enter and Ctrl+Enter = Activate anyway, the footer shows both), Esc closes it and the workflow stays Off.
4. While "Testing..." runs, step sheets open read-only (the test belongs to that revision) and Esc cancels the test.
5. Test run with findings present is refused in place: the first finding is selected, nothing is sent.
6. Esc closes the topmost layer only, in this order: the Activate confirm, a picker, a sheet, then the running test; `/` opens the field picker only at the start of an empty field or after `{{`, so typing a URL or `owner/repo` never opens it; bare `N` is Add step when the canvas has focus (the shell's New todo yields there); Ctrl+Shift+A has a Cmd+Shift+A pair on macOS and the page binds it itself.
7. State changes (run ended, event stream down) are announced through the same live region.

### 4.7 The header

Title, then the state chip (decision 12), then the test fact in one shape everywhere: "Tested rev 4, passed 2m ago (Fenced)" / "(With model)" / "Not tested" / "Rev 5 changed since the test". The slot is hidden until the workflow has a trigger and an action. Then a "Runs" link, Test run and Activate. One primary button per screen: while a sheet shows a fix button, or Reload, the header buttons drop to secondary, and while any sheet is open they are dimmed. The footer lists only the keys that work in the current state. The header subtitle describes this workflow, not the list's count (#2970).

### 4.8 States

| State | What shows |
|---|---|
| Loading | the list paints from the local store in under 100 ms; no spinner |
| Empty (no workflows) | the three entries of §4.1 |
| Empty trigger | the canvas holds one dashed "Add a trigger" slot; the primary button says the same |
| Findings | ⚠ on the steps; header "2 things to fix" ("1 thing to fix"); the primary button "Fix 2 things" jumps to the first; Activate off |
| Untested | chip "Not tested" on the row and the header; Test run is the primary button; Activate works and opens the confirm "Not tested yet. Activate anyway?" (Enter / Esc) with the second button "Test run first" of §5.3 |
| Testing | the canvas shows a dot per step as it runs (§7); the button says "Testing..." and Esc cancels |
| Test failed | the failed step is selected, its sheet shows the sentence and the one fix button (§7.3), which is the only primary on the screen; a fix that is a field moves focus to that field (A's Review step fails on `SND-GRAPH-113`: "What should it do?" came out empty, so the fix focuses that field) instead of repeating it, and no primary shows then: the header buttons stay secondary and Save step is dimmed until the field has a value. "Retry with model" (`SND-GRAPH-103`) lives on the run page only (it needs a model run) |
| Tested | "Passed 2m ago"; Activate is the primary button and is one click, with no confirm |
| Active | chip "Active, next weekday 7:00"; Pause; Run now |
| Conflict | a stale-base edit (`SND-GRAPH-007`) shows "Changed elsewhere. Reload" and keeps the owner's open sheet text; Reload is the only primary and Save step is off until it runs |
| Engine down | the page says serve is not running and keeps the last read; writes are off. It wins over every other state: the primary is "Start Sindri", findings stay on their steps but "Fix N things" is not offered |
| Test cancelled / stream lost mid-test / Activate in flight / Activate refused (`SND-GRAPH-007`) / save in flight | "Test cancelled" keeps the sheet text; "Not live. Last read 14:02" with the test marked unknown; the Activate button shows a spinner and is off; "Changed elsewhere. Reload"; the sheet's Save shows a spinner and keeps the text |
| Add step, Change trigger or rename while Testing | refused in place: "A test is running. Esc cancels it" |
| Active, never ran | chip "Active, waiting for the first PR" with "Run now with a recent PR" (a live run, the owner's click); after the first live run the header shows "First live run passed 2m ago. Open it" or its failure sentence |
| Template gallery loading / failed | three skeleton cards under 100 ms; on a failed read: "Couldn't load templates. Retry", and Describe it and Blank still work |
| Recent events loading / empty / failed | "Looking for recent events" / "None seen yet; the sample is used" / "Couldn't read events. Retry"; the sample stays pickable |
| `/` picker loading / empty | upstream names show at once; values fill in; with no sample the type shows |
| Save refused by the server | the sheet stays open, keeps the text, shows the sentence of §6.1 on the field or at its top |
| Run page loading / failed | steps from the list's cache first; "Couldn't read this run. Retry" |
| Describe it | "Thinking" with Esc to cancel; "No agent chosen. Choose one" (the fix line) when `workflow.agent.target` is unset; a failed agent shows its sentence and keeps the text typed |
| Active, newer revision saved | the header line of §4.10; "Activate rev N" is the primary button; Run now reads "Run now (rev 1)" |
| Delete confirm / delete refused / delete in flight | §4.9's confirm; the refusal sentence in the confirm with its one fix button; Delete shows a spinner and is off |

### 4.9 Delete (#3306)

Delete is the last item of the header's ⋯ menu and a row of the `.` panel. It is never a bare key. It always opens one confirm. The confirm has three forms:

| The workflow is | The confirm says | Buttons |
|---|---|---|
| off, no run in flight | "Delete Nightly size check? Its 12 runs stay readable on the Runs page. Its 1.2 MB of resources are deleted. You can't undo this." | Cancel, **Delete** (red) |
| active | "Nightly size check is active. Pause it, then delete it." | Cancel, **Pause** |
| off, a run in flight | "A run of Nightly size check is still going. Delete it after the run ends." | Cancel, **Open the run** |

1. Focus starts on Cancel, so a stray Enter deletes nothing. Ctrl+Enter presses Delete, and the footer shows both keys. Esc closes the confirm. The confirm traps focus, the same as the Activate confirm (§4.6 focus contract 3).
2. The run count is all of the workflow's run rows, test runs included. The size is the sum of `ListResources` sizes for the owner `workflow:<slug>` and each `node:<slug>/<id>`. An unmeasured resource adds nothing, and then the text says "at least 1.2 MB". With no resources the second sentence drops out.
3. Pause in the active form pauses the workflow and then shows the first form in place. The owner still presses Delete.
4. After the delete, the list drops the row and the toast says "Deleted Nightly size check". The toast has no Undo. Focus moves to the next row, or to the previous one when the deleted row was last, or to the empty state of §4.1.
5. Past runs stay on the Runs page. They show "Nightly size check (deleted)" and open read-only: no Re-run, no Run now.
6. The slug stays taken, because the run ids `slug@rev:N` keep it. A new workflow with the same name gets the slug `nightly-size-2`, and the start screen says so before Save.

Engine and API (the same rules in the app, `sindri wf delete`, `DELETE /sindri/workflows/{slug}` and `workflow_delete`):

- An active workflow is refused with `SND-GRAPH-114` "This workflow is active; pause it first, then delete it". Today `WorkflowSql.Archive` clears `active_revision` without a word.
- A workflow with a run in flight (`IWorkflowRunStore.InFlight`, live and test) is refused with `SND-GRAPH-115` "A run of this workflow is still going; delete it after the run ends or cancel the run".
- After the archive, the service purges the resources of `workflow:<slug>` and every `node:<slug>/<id>`, through the code `PurgeResourcesService` uses. A failed purge doesn't undo the archive. The response says what stayed, and `sindri doctor` lists the owner as an orphan.
- The response gains `runsKept` and `bytesFreed`, and `wf delete` prints "Deleted nightly-size. 12 runs kept, 1.2 MB freed". A session may delete a workflow that is off, the same as today. It can never delete an active one, because it can't press Pause (§8).

### 4.10 Editing an active workflow (#3303)

Every Save makes a revision (§3.1), and triggers keep running the active one. So an active workflow with a newer saved revision is a state of its own:

1. After Save, the sheet and the canvas show the newest revision, the one just saved. The page never reloads the active revision over the owner's edit.
2. The header shows one line under the title: "Revision 2 saved · revision 1 is still active · **Activate**". The header's primary button reads "Activate rev 2" and does the same as the line's link. It follows decision 8: an untested revision opens "Not tested yet. Activate anyway?", and a tested one activates in one click. The line replaces "Revision 1 of 2".
3. Run now names its revision: "Run now (rev 1)". Its tooltip says "Runs the active revision. Test run tries rev 2 first". Test run is the way to try the edit: it runs the newest revision (§3.2).
4. Run now sends the revision it named. `RunWorkflowService` takes an optional `expectRevision` (`wf run --revision N`, `revision` on the route and the MCP tool). When the active revision differs, the run is refused with `SND-GRAPH-007` and the page shows "Changed elsewhere. Reload". Without the field, Run now runs the active revision, as on main.
5. Activate rev 2 clears the line. After Pause, the workflow is off and the line goes too. The step list of §3.1 then decides the primary button.
6. Every revision after the active one counts: with revisions 3 and 4 saved over an active 2, the line says "Revision 4 saved · revision 2 is still active". Activate sends 4.

Until P8 ships, ui's stopgap line from #3303 stays.

## 5. Test run

### 5.1 What is fenced

**The fence fails closed.** A test run binds every port to its test version by default. A port runs for real only when it is on a read-only allowlist. A new port nobody classified is recorded, never run.

How: the engine picks the block registry by `run.mode` (read from the run row inside the activity, so `for_each` children need no parameter), so a test run builds its blocks from the test ports and the live singleton registry is never reached. There is no per-block `fence:` attribute (a second source of truth); the per-port binding is the one. An architecture test (`Aw.Architecture.Tests`) enumerates the constructor parameters of every `IGraphBlock` and fails on any type that has no test binding and is not on a fixed read-only list (this catches `IRunningToolkits`, `IGraphSeenStore` and `TimeProvider`, which an `I*Port` name match misses). Outside the ports, the walk itself has a test branch: `Walk.Park` skips `wait` (the row says "would wait 2d") and takes the `default` or first choice for `ask_owner`; `IResourceProvisioner.Ensure` records "would create folder X" and creates nothing. One test: a `wait 1d` and an `ask_owner` step finish a test run in under 5 s. Recorder rows are stored per (run, node, item) in the run's node-run record, capped at 64 KB each like other node output.

| Port or block | Test version | Run view shows |
|---|---|---|
| `INotifyPort` | records the call | "would have: notify 'Main gate red'" |
| `ICreateItemPort` | records, returns a made-up item | "would have: create item 'X'" |
| `IGraphHttpPort` non-GET | records | "would have: POST host/path" with the body |
| `IGraphHttpPort` GET | runs for real (read-only, and the host must be on the profile's allowed hosts, as a live run) | the real response |
| `IConnectionToolPort` / `call_tool` | runs for real only for a tool marked `ReadOnly`; every other tool is recorded (unknown = write). `ConnectionTool` and `ToolkitTool` gain a `ReadOnly` field (P0 contract row): from the MCP `readOnlyHint` annotation, and for REST presets from the action's HTTP method (GET = read) | "would have: comment on acme/api#12" |
| `IRunningToolkits` (`call_tool` with no connection) | same rule, by the toolkit tool's `ReadOnly` mark | "would have: call tool X" |
| `ICodeRunPort` (`code`) | runs for real: `CodeRunSandbox` has no network, a read-only repo and a private `/tmp`, so a Code step feeding an If step is compiled and run for real | the real output |
| `IGraphResultPort` (`workflow_result`) | read-only: runs for real, and skips test rows when it looks for the newest succeeded run | the real result |
| `IGraphModelPort` (`prompt`) | pinned output or example; real only with "With model" (§5.2) | "model not run" |
| `IGraphCallPort` (`run_workflow`) | `Start` runs the callee as a test (§3.2); `MaxDepth` and `Wait` read for real | the callee's test run |
| `IQuestionPort` | auto-answers with the first choice | "would have asked: ..." |
| `IGraphMemoryPort` `Remember` / `Forget` | record | "would remember: ..." |
| `IGraphMemoryPort` `Recall` | runs for real | the real memory |
| `IGraphSeenStore` | `only_new` reads the real store; `Record` and the run-end `Commit` write nothing | "would mark seen: ..." |
| `IGraphModelPort.Untrusted` | passes through (a pure string wrap) | the wrapped text |

Every "would have" row shows the exact payload so the owner can read what a live run would send.

### 5.2 Model and agent steps

By default `prompt` and `run_agent` return their pinned output, else the schema's example, so a test spends no model time and no worktree. That proves the wiring and not the model's answer. Two switches change it, both per test run:

- **With model** runs `prompt` steps for real (read-only, no side effects). Use it to catch #3079: the model answered text, not JSON.
- **With agents** runs `run_agent` nodes in the Sindri sandbox with no push and no write (engine's `--with-agents`). A test launch forces `GithubCredentialResolver` to `None` (the token, its mount and the github host group follow from it; `SandboxShims.WriteGithub` then writes no shim), so the agent holds no GitHub token; W6's with-agents case asserts that on the built sandbox plan, not on fakes. It spends the owner's agent usage, so a session may not use it (§8).

The Test button has a small menu: Test (fenced), Test with model, Test with model and agents. `T` opens a centred dialog, not a sheet: one radio row per recent real event (newest picked), the example payload last, the would-match line, an "Edit the payload" fold, and a split button (Test run, Enter, with this menu on its arrow). Its footer says "Nothing is sent. The model and agents do not run.", and each menu row carries a short note ("nothing runs for real", "uses your agent time"). A step with a pin shows a pin glyph; "Pin this output" is in the run page (§7). A live GET in a test (the read-only case above) is the only network read a fenced test makes.

### 5.3 What passing means

A test passes when validate passes and the walk ends `succeeded`. A step the test could not check says so in its row ("model not run: output taken from the example"). The tested fact on the header names the mode: "Passed (Fenced)" or "Passed (With model)". The result strip under the header says what did not run: "Passed. Model, agents and notifications not run." / "Passed with model. Agents and notifications not run." A fenced pass is not a claim that the review loop or the notification worked, so the strip says it and Activate stays one click: after any passing test, fenced or with model, there is no confirm. Only an untested revision confirms ("Not tested yet. Activate anyway?", Enter / Esc), with a second button "Test run first" that runs the missing test and returns to the confirm. It never runs agents: that spends the owner's agent usage and stays a pick in the Test menu. Activate is never refused.

## 6. Errors, in words

### 6.1 One table, one test per row

Each failure code maps to `{code, sentence template, fixAction}`. The mapping lives in one catalog file in Core (not in the UI), so the CLI and MCP print the same words. `fixAction` is a button in the UI and a command string for a session (`sindri wf edit S --base N ...`). The catalog sentence is the owner-facing text; the `ErrorDefinition` message on main (`SindriErrors.Graph.cs`) stays the machine message, and each row's test asserts that both exist and that the code matches. Placeholders (`{step}`, `{repo}`, `{connection}`) are filled from the finding's node title and config, never from the node id; an unfilled placeholder fails the row's test. One test per row asserts the sentence and that the named action exists.

| Code | Sentence | Button / command |
|---|---|---|
| `GRAPH-CONFIG-001` | "{step} needs a repo." | Open the step / `wf edit` setting the field |
| `GRAPH-NAME-001` (once #3001 ships) | "{connection} is not a connection on this box." | Connect GitHub / `sindri connection add <label> --preset github` (browser sign-in) |
| `GRAPH-NAME-002` (today a warning) | "Couldn't check the connection yet." | Retry / `wf validate` |
| `GRAPH-TRIGGER-001` | "A workflow has one trigger." | Change trigger / `wf edit` with `replace_node` |
| `SND-GRAPH-112` (new, §2 decision 5) | "Only the trigger can be replaced." | none |
| `SND-GRAPH-103` | "The model answered with text, not the JSON this step asks for: "Usage limit reached ..." (first 200 characters)." | Retry with model (primary) / `wf test --with-model`, and a second action "Open {step}" (for example "Open Plan") that opens the failed step in the editor, because a retry fails the same way when the cause is a usage limit; the panel opens on its Raw answer tab, so the text needs no third button |
| `SND-GRAPH-113` (new, like 112; 113 is free on main at 6b647dbb58, P0 confirms) | "{step}: \"{field}\" came out empty. {reference} had no value in this input." A fenced test can hit it: the references resolve against the picked event before any stub runs. Example on A's Review step: `Review: "What should it do?" came out empty. trigger.pr.body had no value in this input.` | Edit {field}, which focuses that field / `wf edit` setting the field |
| `SND-LAUNCH-003` | "The agent for step {step} isn't available: its CLI is not installed or the target is not in the catalog." Preflight checks the agent target before any test, so it shows only in a with-agents test or a live run, never in a fenced one. | Choose an agent / set `workflow.agent.target` |
| `SND-LAUNCH-013` | "The agent entry can't express {step}'s model or policy." | Open the step |
| `SND-GRAPH-105` | "This workflow is not on." | Activate / `wf activate S` |
| `SND-GRAPH-106` | "A manual run's input must be a JSON object." | Edit the sample / `wf test --trigger f.json` |
| `SND-GRAPH-107` | "This run already ended." | none |
| `SND-GRAPH-108` | "The output of {step} was cut at 64 KB, so a later step can't read it." | Open the step |
| `SND-GRAPH-109` | "A run of this workflow can't start the workflow again." | Open the step that does |
| `SND-CODE-001` | "{step} doesn't compile: {message}." ({message} lists each error with its line and column) | Open {step} |
| `SND-CODE-002` | "{step} ran past its time limit." | Open {step} |
| `SND-CODE-003` | "{step} used more memory than its limit." | Open {step} |
| `SND-CODE-004` | "{step} stopped: {message}." ({message} is the exception message, or says the result is not JSON) | Open {step} |
| `SND-CODE-005` | "Sindri can't run code steps: the code runner couldn't start." with the SND message underneath (the runner binary missing, a refused sandbox plan, a run area that can't be made, a process that doesn't start, a runner exit 2) | None |
| `SND-GRAPH-007` | "Changed elsewhere." | Reload / `wf show S` |
| `SND-API-001` | "Sindri is not running." | Start Sindri / `sindri serve` |

### 6.2 The prompt step

The block checks the exit code and the text before it parses. Exit code non-zero or an answer that does not start a JSON value becomes `SND-GRAPH-103` with the `raw` text kept on the node run. The stock template's first run must pass (W4).

## 7. The run page

### 7.1 Layout

The run page is the canvas with a status dot per step, and a bottom panel for the selected step. At 1440x900 the body between the 40 px top bar and the 28 px footer is 832: header 48, canvas 568, panel 216.

- **Header.** `<workflow> › run 3` (mono id left of the title), the run's state chip from the event (decision 4: "Running", "Succeeded", "Failed at {step}", "Waiting on you", "Cancelled"), mode (Live / Fenced / With model / With model and agents, the Test menu's names), trigger summary, "Started 4m ago · took 14s".
- **Dots.** Queued, running (arc), succeeded, failed (x), skipped (grey), parked (pause), would-have (its own glyph; the dashed ring is the draft's, so X1 adds a would-have glyph to the grammar). Queued is a ring in ink-subtle. A cancelled run has no glyph and an ink-subtle title (the grammar's ended rule); succeeded and failed keep their check and x. Colour is never the only signal.
- **Panel.** Tabs: Input, Output, Raw answer (only when a model step kept text), Would have (test runs only, so it stays reachable at 1000 wide), Error, Time. A failed run reads "Failed at {step title}", never the step kind. Input and Output are the stored values (`NodeRunView.Input`, `.Output`, cut at 64 KB with a "truncated" badge). Large payloads fold. For a test run the panel also shows "would have" rows.
- **Pin.** "Pin this output" is a button (no bare key: a pin is a write) on a succeeded step, and it saves it as a `workflow_pin` row (§3.1) for later test runs of this revision.

### 7.2 State and age

The runs list is its own page under the workflow ("Workflows › PR review › Runs"), Live by default; a Tests toggle in its header (`[` and `]` switch it) shows the test runs, grouped as "Tests, newest 50 kept" (§3.2). The run list and header read `workflow.run.*` events. They update in place; no page reload. A run row is "Running" only while the engine has not closed it. "N ago" uses the shared clock. If the event stream is down the header says "Not live. Last read 14:02" and the list reads again every 15 s.

### 7.3 A failed run

The header sentence comes from the §6.1 table. Under it, one button chosen from the cause. The one exception is `SND-GRAPH-103`: "Retry with model" stays primary and "Open {step}" sits beside it as a secondary action. Raw codes stay in "Details". **Re-run from here** (ledger 9) is later (P7): it restores the earlier steps' stored outputs, re-runs this step and the ones after it, and links the new run to the old one. A step that already made an object shows "done before: comment #5521" and skips it by default, with "do again" as an explicit toggle (ledger 12).

## 8. Agents

Every action has a CLI verb and an MCP tool. A session may run `wf validate`, `wf test`, `wf create`, `wf edit` and read runs; it never presses Activate or Pause for the owner. Today `workflow_activate`, `workflow_pause` and their routes (`WorkflowWriteEndpoints.cs:93-111`) carry no refusal: this spec adds the refusal once, in `ActivateWorkflowService` and `PauseWorkflowService` (the Linear `IsOwner` pattern), so routes and tools share it; it flips `SessionOk` in `WorkflowRows.cs` (catalog goldens change), a red test covers each tool, and `src/Sindri/aot-smoke/run.sh:226` (`wf activate </dev/null` counts as a session) and `docs/csharp/sindri-graph.md:122` are updated in the same PR. The Activate request also takes `ifLatest` (the revision the owner saw) as its own field, with its own E row (E14): the server activates exactly that revision and refuses if the newest differs at write; a plain `--revision 3` rollback stays allowed. A session also may not run `wf test --with-model` or `--with-agents` (owner usage; `SND-AUTH-002`). `wf activate` from the owner's terminal on an untested revision prints "Not tested yet" and needs `--untested-ok`, a field (`untestedOk`) on `ActivateWorkflowRequest` the server checks.

Exit codes of `wf test`: see below. `--json` prints the run with its findings and "would have" rows. `wf test S` takes the slug positionally, as `wf run` does; `wf validate` keeps `<file>` and gains `--slug S` (the table's form). `--slug` already exists on main; `wf validate` keeps its codes (`Usage.cs`, `WfValidateCommand.cs`: 0 clean, 1 findings or not running, 2 usage), and `wf test` uses 0 passed, 1 Sindri not running, 2 refused or invalid, 3 the test ran and failed. The exact `Usage.cs` lines for `wf test`, `wf pin`, `wf unpin`, `wf templates` and `wf trigger-events` are part of P4b's red.

| Action | CLI | MCP tool |
|---|---|---|
| Validate a revision | `sindri wf validate --slug S [--revision N]` | `workflow_validate` |
| Test run | `sindri wf test S [--revision N] [--trigger f.json \| --trigger-event ID] [--pin NODE=f.json] [--with-model] [--with-agents] [--json]` | `workflow_test` |
| Replace the trigger | `sindri wf edit S --base N --ops '[{"op":"replace_node",...}]'` | `workflow_apply_ops` |
| Recent events of a kind | `sindri wf trigger-events --kind K [--connection C]` | `workflow_trigger_events` |
| Templates | `sindri wf templates` | `workflow_templates` |
| Pin an output | `sindri wf pin S --node X --run R` | `workflow_pin` |

Text from a run's output, a PR title, a model answer, the raw answer and every "would have" payload is fenced as untrusted in every CLI and MCP result, and redacted before it is stored on a pin.

## 9. API and engine requirements

| # | Need | Owner lane | Notes |
|---|---|---|---|
| E1 | Run `mode` (`live` or `test`) on the run row and in both run payloads; test versions of every port, fail closed (§5.1); `tested` derived from the run row; `workflow_pin` table and `workflow.pinned` event (§3.1) | engine | #2969's engine plan, as meta accepted it; order: #2985 first, then ports and mode as one PR, then model and agent stubs |
| E2 | `POST /sindri/workflows/{slug}/test` with `{revision?, trigger?, triggerEvent?, pins?, withModel?, withAgents?}`; MCP `workflow_test`; `wf test` | api | the UI button waits on it |
| E3 | `replace_node` op in `GraphOp` / `GraphOps` (`Core/Graph/Ops/GraphOp.cs`): same slot, keeps node id and outgoing edges, one validate; `PublicAPI` entries and goldens | engine | slot = trigger now; other kinds refused with `SND-GRAPH-112` |
| E4 | Prompt block: check exit code and text before parse; keep `raw` (cut at 64 KB) on the node run; `NodeRunView` gains `Raw` | engine, api | #3079 |
| E5 | Every `Title` is the only display name; `NodeSummaryView` carries `kindTitle`; sheet titles come from the step title | engine, ui | #3074; the node id never goes to a label |
| E6 | `GET /sindri/workflows/trigger-events?kind=&connection=` (last N with a one-line summary, the example, the would-match check); MCP and CLI | api | #3032; the forge poll keeps the last 20 real events per connection (#2988's engine comment) |
| E7 | `GET /sindri/workflows/templates` (A, B and C in v1, each with trigger, step count, needs, last run) | engine | replaces the hard-coded list |
| E8 | Event kinds `github.pr.opened`, `.updated`, `.ready_for_review`, `.review_requested`, `.review_submitted`, `.comment`, `.checks`, `.conflict`, each with a payload schema and a sample. `github.pr.opened`, `github.issue.opened` and `github.issue.labeled` already exist on main (`Core/Connections/PollableEvents.cs`, `GitHubEventMapper.cs`); the work is their payload schema, sample and the `label` filter | api, engine | #2988; §12 gap 1 |
| E9 | Failure-to-sentence catalog (§6.1) in Core | engine | one test per row |
| E10 | The holes that make "valid" not mean "will run": #2985, #2986, #2987, #3001, #2999 (sweep for stuck `running`), #3003, #3005 | engine | W7 depends on them |
| E11 | Validate returns findings with node id, stable id, cause, fix | engine | the route exists; the shape is confirmed in P0 |
| E13 | Pins: `POST /sindri/workflows/{slug}/pins`, `wf pin S --node X --run R`, MCP `workflow_pin`; templates `wf templates` / `workflow_templates`; trigger events `wf trigger-events` / `workflow_trigger_events`. Template files: folder `src/Sindri/Aw.Sindri.Infrastructure/Workflows/Templates/<id>.yaml` holding the spec, `needs`, and a first-run checklist; `wf create --template A` reads the same files | api, engine | one route, one verb, one tool per row |
| E14 | Activate takes `ifLatest` and `untestedOk`; the `NextTestOccurrence` store call; the ten-digit `WorkflowRunId` | api, engine | P0 contract |
| E12 | Ui subscribes to `workflow.run.*` (payloads: `WorkflowRunFinishedPayload(RunId, Status, Error, By)`) | ui | no new route |

## 10. Goals and non-goals

Goals:

- A, B and C reach a passing test run from zero in 4 steps (W1 to W3).
- Add step, Change trigger and Test run all work from the keyboard.
- The first live run behaves as the test did.

Non-goals (this spec):

- A visual redesign of the canvas. Design owns the frames and tokens (#2978).
- Revision list, side-by-side diff and named revisions (ledger 11). Revisions exist; undo (the toast's Undo, off while a test runs or Activate is in flight; inside a text field Ctrl+Z is the field's own) is the only part here.
- A public template gallery. The templates are ours; v1 ships A, B and C only.
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

1. **B needs `github.issue.labeled` with `label` in its payload.** The kind exists on main (`PollableEvents.cs`); #2988's list names pull request kinds only, so its schema and sample are added there (settled, open question 3).
2. **The test run and the "will run" promise can drift.** If validate and the test use different checks, a pass can still fail live. The test calls validate first; W7 is the property test.
3. **A stubbed model hides #3079-type bugs.** Default test mode does not call the model. "With model" exists for that, and Activate accepts either (settled: "With model" is not required for a Prompt step), so a passing fenced test can still meet a model that answers text. The header names the mode (§5.3).
4. **A draft has no "unsaved" state.** Every sheet Save is a revision. A long edit makes many revisions. Acceptable now; a "save as one revision" grouping is a later idea.
5. **The ledger measured by reading docs.** Its step counts for A are inferred (its "Weakest claims"). W1 is measured in our app, not copied from them.
6. **GRAPH-NAME-002 is still a warning** (connection not checked, #3001). Until it is fixed a wrong connection name passes the test as "could not check". The test row says so.
7. **Home's run failures** (Home spec, `run-failed` row) need the unseen flag from the same store E1 touches. Do not add a second one.

## 13. Proof

Proof is the app run on seeded data with `aw-ui-driver`, not tests alone.

1. **Walk A, B and C from a fresh home** (`scripts/context-first-run.sh`, forge fixture `scripts/forge-fixture.sh`). Capture before and after shots of each of the 4 steps. Count the steps and the seconds. This is W1 to W3.
2. **Template matrix** in CI: each of the templates (A, B, C in v1), fake model returning the example, then plain text (W4).
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
| P0 | Contract PR: `ReadOnly` on `ConnectionTool` and `ToolkitTool`, `Mode` on the run payloads and `WorkflowRunPublic`, the new routes' request and response records, the `replace_node` op, `NodeRunView.Raw`, the findings shape, golden and `PublicAPI` entries, all as stubs | api | none |
| P1 | Build: the sheet-first Add step (#3072), Change trigger (#3075), `GraphCanvas` elements (#3073), names (#3074), start screen with templates A, B and C; the `workflow.editor` key. Structure builds on the P0 stubs; the visual pass (type scale, motion, density, dot tokens) waits for X1 frames | ui | P0 stubs; X1 for the visual pass |
| P1e | `replace_node` (with `SND-GRAPH-112`), the Title rule, the templates route, the block checks for templates A, B and C | engine | P0 |
| P2 | Live state: event subscription and shared clock (#3080); chip and header (#2970) | ui | P0 |
| P3 | Engine truth: #2985, #2986, #2987, #3001, #2999, #3003, #3005 | engine | none (start now) |
| P3e | Prompt block exit-code check, `raw`, sentence table (#3079, §6) | engine | none |
| P4a | Test mode, the fail-closed port bindings, the walk's test branch (wait, ask, resources) and the architecture test, `mode` and `test_options` columns and migration, the reader filters of §3.2, `workflow_pin`, `ReadOnly` marks, a `mode` variable and witness in `specs/tla/GraphWorkflow` (a test in flight must not skip or queue a live trigger) | engine | P3 (#2985 first) |
| P4b | `POST .../test`, `workflow_test`, `wf test`, trigger-events route, `wf trigger-events` | api | P4a |
| P4a2 | Session refusal of Activate and Pause in the services (with the aot-smoke and doc updates of §8); the `--with-model` / `--with-agents` refusal lives in the test service, with one red test each for the route, the MCP tool and the CLI, and lands with P4b's test route | api | none (start now) |
| P4c | Test run UI: the button and menu, the run view with "would have" rows, the Activate confirm for an untested revision ("Not tested yet. Activate anyway?") | ui | P4b stubs |
| P5 | GitHub kinds and samples (#2988, #3008), issue-labelled kind; the connection field with "Connect GitHub" | api, engine | P0 |
| P5ui | Trigger step events picker (#3032), field picker (#3033), Prompt output as a form (#2968), flip `workflow.editor` default | ui | P4b, P5 |
| P6 | Describe it; delete the old editor path | ui, engine | P5ui, one week of green W1 to W3 |
| P7 | Re-run from here; undo | engine, ui | P4a |
| P8a | Delete's refusals `SND-GRAPH-114` and `SND-GRAPH-115`, the resource purge, `runsKept` and `bytesFreed` (§4.9); `expectRevision` on Run now and `wf run --revision` (§4.10). The response fields change goldens, so the gate runs the API goldens, `EndpointCqrsTests` and the aot-smoke if `wf delete` output is in `expected.txt` | api, engine | none (start now) |
| P8 | Delete in the ⋯ menu and the `.` panel with its three confirms, "(deleted)" runs (§4.9); the newest-revision sheet, the header line, "Activate rev N" and "Run now (rev N)" (§4.10) | ui | P8a stubs |
| X1 | Design: frames for §4.2, §4.8 (every state row, including loading and failed), §7.1 and the Activate confirm, in the Inspora bar; type scale, motion (state change, dot arc, sheet open) and density; token names for the dots. First frames (§4.2, §4.8) land before P1's visual pass | design | none |
| X2 | UX: the walk of A, B and C at each phase's end; filing every miss as an issue `Part of #2978` | ux | each phase |
| X3 | Perf: W9, W10 in the real app; the canvas at 200 steps | perf | P1, P2 |
| X4 | QA: judge each phase against the owner's bar, the shots and W1 to W13 | qa | each phase |
| X5 | Tools: the aot-smoke lines, `.env.example`, scaffolds and docs (`docs/csharp/sindri-graph.md`, the block pages) | tools | each phase |
| X6 | Context: the context map's workflow concepts (a workflow, a run, a step) so agents find them | context | none |

Day one: api P0; engine P3 and P3e (no dependency); design X1; ux X2 baseline walk; ui P1 on the P0 stubs; context X6. Nothing waits on a dependency PR to start its red.

Gate notes: P1 to P5ui add catalog rows, action ids and API goldens, so each gate runs the Sindri integration lane and the Ui goldens, as the team rules say. A PR with a doctor check change updates `src/Sindri/aot-smoke/expected.txt`. Also: P4a gates `Aw.Architecture.Tests`, `Aw.Sindri.Testing.Tests` and the TLC check on `specs/tla/GraphWorkflow` (`scripts/tlc-check.sh`). Raw model text, recorder rows and the 200-character quote are scrubbed by `Aw.Redaction` before they are stored or printed (main scrubs only `Input` and `Error`). `ReadOnly` defaults to false for a toolkit tool until its manifest sets it. The `mode` migration bumps `ledger schema N/N` there and runs aot-smoke; new MCP tools add their `tools/list` lines; any `EventCatalog.cs` payload change runs `EventCatalogTests`; new POST routes run the full Sindri integration lane and `EndpointCqrsTests`; a `Usage.cs` or `PublicAPI` change runs the whole `Aw.Sindri.Tests` unit lane. This spec adds no environment variable, so `.env.example` is unchanged (`scripts/check-env-example.sh` still runs).

## Open questions

All seven are settled by the lead.

1. **Refuse Activate until a passing test?** Settled (lead, 2026-11-12): no. An untested revision shows "Not tested" on the row and header, and Activate opens one confirm, "Not tested yet. Activate anyway?" (Enter / Esc). A cron or PR trigger may have no sample to test with, and a hard gate with no override strands those.
2. **Should Activate insist on "With model" for a Prompt step?** Settled (lead, 2026-11-12): no. The first-run header ("First live run: watch it") is enough.
3. **Which issue event for B?** Settled (lead, 2026-11-12): `github.issue.labeled` with `{repo, number, label, author}`, the kind exists on main (`PollableEvents.cs`); #2988 adds its payload schema and sample, polled like pull requests, plus `github.issue.opened`.
4. **Where do the templates come from?** Settled (lead, 2026-11-12): A, B and C ship first as spec files in the repo under `src/Sindri/...` with a first-run checklist, loaded by E7. The other jobs of #2960 come after the soak, not in v1. Also after the soak: an owner-only "send me the ping" test switch (settled out of v1: a test sends nothing; the cost is that a broken Slack or GitHub ping only shows on the first live run).
5. **Does Describe it use the owner's agent?** Settled (lead, 2026-11-12): it uses the profile's `workflow.agent.target` (#2986) and refuses with the fix line when unset.
6. **Does a test count after the revision changed?** Settled (lead, 2026-11-12): no. Any new revision is "Not tested".
7. **Pins on a revision or on the workflow?** Settled (lead, 2026-11-12): per revision. A later "carry pins to the new revision" prompt if it hurts.

### Review notes

Round 1 (/autoplan, 2026-11-12; files in `plans/workflows-page-r1/`). Verdicts: product RETHINK (3 HIGH), arch NEEDS_WORK (5 HIGH), design NEEDS_WORK (3 HIGH), devex NEEDS_WORK (4 HIGH), security NEEDS_WORK (3 HIGH). All HIGH are applied: P1 the bar's counting rule (§1); P2 preflight findings (§4.5); P3 and devex 3 the fixes ship in both layouts (decision 14); arch F1 and security 1 the fence fails closed (§5.1); arch F2 and F3 `mode` column, id, slots and Home filter (§3.2); arch F4 `tested` derived, pins in a table (§3.1); arch F5 `replace_node` keeps the id (decision 5); design 1 to 3 states, focus contract, X1 dependency; devex 1, 2, 4 exit codes, sentence rows, gate notes; security 2, 3 session refusals (§8).

- Round 1 (product 4, stale event list): applied; E8 and gap 1 now say the kinds exist on main.
- Round 1 (product 5, "First live run" undefined): applied; new "Active, never ran" state row.
- Round 1 (product 6, scope beyond the proof): not applied; undo, `/` picker, Describe it and pins are the owner's #2960 asks, and they sit in later phases that do not block W1 to W3.
- Round 1 (product 7, gallery is only the proof set): not applied; the lead settled A, B and C for v1, other jobs after the soak.
- Round 1 (arch F6, unsourced numbers): partly applied; 64 KB is `SND-GRAPH-108`, "10 kinds" is the step menu (the other block classes are not user-added steps); the 4 and 3-minute figures are the owner's bar (#2960).
- Round 1 (arch F7, recorder storage): applied in §5.1 (per run, node, item, 64 KB cap).
- Round 1 (arch F8, "no push, no write" source): applied; §5.2 says it is only as good as the sandbox plan, W6 has a with-agents case.
- Round 1 (arch F9, merge rule and resume cursor): applied as a note: the list is a cache and an event with a newer `At` wins; on reconnect the page re-reads the list (§7.2); the event cursor is the engine's detail in P2.
- Round 1 (arch F10, `sindri-graph.md:44-46` says the catalog is empty): applied to X5 docs; fixed in the docs PR of P0.
- Round 1 (design: "turn it on" vs Activate): applied in decision 7.
- Round 1 (design: Test with findings present): applied in the focus contract (refused in place).
- Round 1 (design: editing during "Testing..."): applied (sheets read-only).
- Round 1 (design: Activate confirm default focus): applied (focus contract 3).
- Round 1 (design: bare keys and Ctrl+K): applied in §4.6.
- Round 1 (design: Activate has no key): applied; Ctrl+Shift+A.
- Round 1 (devex: verb naming): applied; `wf trigger-events`, positional slug.
- Round 1 (devex: pin, templates, events have no E row): applied as E13.
- Round 1 (devex: `wf activate` on untested): applied in §8 (`--untested-ok`).
- Round 1 (devex: `workflow.editor` not in the schema): applied in decision 14.
- Round 1 (devex: docs pages in X5): partly applied; X5 names `docs/csharp/sindri-graph.md` ("Testing a workflow", "Pins") and the template folder (E13); `wf docs` topic left to X5.
- Round 1 (devex: template folder and format): applied in E13.
- Round 1 (devex: pins, --outputs, Pin this output): applied; `--pin NODE=file`.
- Round 1 (devex LOW: `--real-agents`): applied; `--with-agents`.
- Round 1 (devex LOW: "Round 1: not run yet"): applied (this section).
- Round 1 (security: Activate pins the seen revision): applied in decision 8.
- Round 1 (security: fence new result fields): applied in §8's last line.
- Round 1 (security: Describe it scope): applied in the states table (needs `workflow.agent.target`); read-only scope left to P6's design.
- Round 1 (security: pins and events hold secrets): applied; redaction before storing (§3.1).
- Round 1 (security: raw text 64 KB): applied; fenced and redacted in §8.
- Round 1 (security LOW: who tested and activated): not applied; the run row and `workflow.pinned` carry `By`, and the honest-agent threat model does not need more.
- Round 1 (security LOW: GET runs for real in test): applied; GET is limited to the profile's allowed hosts (§5.1).

Round 2 (/autoplan, 2026-11-12; files in `plans/workflows-page-r2/`; spec read at 0ce29446f0). Verdicts: product ITERATE (1 HIGH), arch NEEDS_WORK (5 HIGH), design NEEDS_WORK (2 HIGH), devex NEEDS_WORK (2 HIGH), security NEEDS_WORK (2 HIGH). All HIGH applied: product, a fenced pass no longer says it will run, and Activate confirms after it too (§5.3); arch G1 numeric test ids, G2 the walk's test branch and the constructor-based architecture test, G3 `mode` on every payload, the public form and the view row, G4 the reader table (§3.2), G5 `replace_node` marks its own reference findings as wiring gaps; design Ctrl+Z is the toast's undo, and focus lands on the result strip with a link to the "would have" rows; devex code `SND-GRAPH-112` (110 and 111 are taken) and the catalog sentence beside the `ErrorDefinition` text; security `IRunningToolkits` and the `ReadOnly` mark, and no GitHub shim in a with-agents test.

- Round 2 (product 2, write-scope preflight not buildable): applied; the repo `permissions` read is named as a guess to confirm in P5, else "Couldn't check".
- Round 2 (product 3, agent target on a fresh home): applied in W1; the fill step sets `workflow.agent.target`.
- Round 2 (product 4, W1 to W3 rows): applied; template path.
- Round 2 (product 5, "ping me" never proven): not applied; an owner-only "send me the ping" switch bends "the test sends nothing". Settled by the lead (2026-11-12): no ping switch in v1; a test sends nothing, with no exceptions. Listed after the soak, with its reason: the ping is stubbed, so a broken Slack or GitHub ping only shows on the first live run.
- Round 2 (product 6 and 7, LOW): not applied; the failing branch after the agent joins the template matrix in X2's walk, and the proof path is W1 to W3.
- Round 2 (arch G6): applied; `ReadOnly` on `ConnectionTool` and `ToolkitTool`, a P0 row.
- Round 2 (arch G7): applied; `test_options` column, and `tested` = newest test succeeded.
- Round 2 (arch G8): applied, rule settled by the lead (2026-11-12); store writes the table, the event holds no output, unpin, carry-forward of unchanged nodes. This refines the lead's per-revision answer (7); say if it should be per revision only.
- Round 2 (arch G9, G10, G11, G12): applied; the `fence:` attribute is dropped, `code` runs for real, rows corrected, TLA witness in P4a.
- Round 2 (design: bare `N`, Ctrl+K, Esc order, `/` in any field, Ctrl+Shift+A pair): applied in the focus contract.
- Round 2 (design: missing state rows, edits during Testing): applied in the states table.
- Round 2 (design: live region owner, run page keys, narrow window, contrast, "fenced" jargon, Run now confirm, cancelled new-step focus, copy per trigger kind): not applied; X1 frames settle them. The header copy no longer says "fenced".
- Round 2 (design LOW: P5ui flip gated on X1): applied; the flip waits for X1's frames in the P1 visual pass.
- Round 2 (devex: key name and values): applied; `workflow.editor`, a `WorkflowSettings` field. The values stay `sheet` and `steps`.
- Round 2 (devex: `wf validate` clash, exit codes, `--untested-ok` field, `SessionOk` placement): applied in §8.
- Round 2 (devex: gate notes missing `Aw.Architecture.Tests` and `Aw.Sindri.Testing.Tests`): applied as a note: P4a also runs both.
- Round 2 (devex: `connection add` not runnable, stale text, reserved slugs, `--trigger` vs `--payload`, `Usage.cs`): partly applied; command fixed and stale text removed; slug reservation and flag name left to P4b's contract.
- Round 2 (security: Activate `ifLatest`, session refusal in the services, scrub of `Raw` and recorder rows, pin by session): applied; scrub of `Raw` and recorder rows is in P4a's checklist.
- Round 2 (security LOW: GET with a query, test run cap, Describe it entry check): not applied; GET to an allowed host is a read in this threat model, and a cap and Describe it's "owner-only, read-only, no connections" check wait for P6.

Round 3 (/autoplan, 2026-11-12; files in `plans/workflows-page-r3/`; spec read at c9cd6d79d5). Verdicts: product SHIP (0 MUST), arch NEEDS_WORK (3 HIGH), design NEEDS_WORK (2 HIGH), devex NEEDS_WORK (2 HIGH), security PASS (0 MUST). All HIGH applied, and the lead's answers of round 2 are in (no ping switch in v1; pins carry forward only when type and config are byte-identical). The 200,000,000 band was wrong: every lens found `CallOccurrence` and `DeliveryOccurrence` already fill 100,000,000 to 999,999,999. Fix: ten-digit ids, tests from 1,000,000,000, a store allocator, "test N" from the column (§3.2). Also applied: the SQL `mode` filters including `LatestOfKind` (schedule floor), `RunItemRepair` / `RunItemLinker` and the fake store; the runs list defaults to Live; `wf validate` keeps main's exit codes.

- Round 3 (product 4, example payload vs typed repo): not applied; the test uses the sample as is and the would-match line says it does not match.
- Round 3 (product 5, Activate confirm cannot run what it lists): applied; "Test with model first".
- Round 3 (product 6, ping unproven until a PR): applied; "Run now with a recent PR" on the waiting state.
- Round 3 (product 7, 9-digit ids in CLI/MCP): applied; "test N".
- Round 3 (product 8, proof column in section 14): not applied; W1 to W3 name their phases.
- Round 3 (arch F5, dense test numbers): applied; the allocator reads test rows only, and the display subtracts the band start.
- Round 3 (arch F6, Home and counters have no hit on main): applied as a note; they come from the Home spec, and P0 adds their filter when they land.
- Round 3 (arch F6, `workflow_result` with an explicit run): applied; `ById` is in the skip list.
- Round 3 (design: Ctrl+Z in a field, toast Undo during a test, double announcement, undone graph equals tested revision): partly applied; the first two are in the undo rule. The announcement and "equals the tested revision" are left to X1 and P4c (a revision restored to a tested graph stays "Not tested" until a new test).
- Round 3 (design LOW: strip role and contrast, carried X1 items): not applied; X1 frames settle them.
- Round 3 (devex: error codes, gate notes, test branches): applied (112, gate list); the `wf/<workflow>/<short run>` branch of a with-agents test is the engine's detail in P4a (the full run id is hashed, so it cannot collide).
- Round 3 (security 2, token lever): applied (`GithubCredentialResolver`).
- Round 3 (security 3, 4, 5, 6): applied (ReadOnly default, ifLatest at write, refusal in the test service, scrub).
- Round 3 (security 7, test cap): applied; newest 50 test rows per workflow.
- Round 3 (security LOW: service locators, pin source run): not applied; the constructor-parameter test covers the real injection path, and the pin store checks the run belongs to the workflow in P4a.

Design frames, round 1 (31 frames, `pr-assets/workflows-3109` at ee56d6ef35, #3109 comment 6821899615). Lead's answers to the five calls:
1. Run page canvas 568, not 520: the 784 left the 40 px top bar and 28 px footer out (§7.1).
2. One fix button on `SND-GRAPH-103`, "Retry with model"; the panel opens on Raw answer (§6.1).
3. Both Activate confirms get the second button; it runs the missing test, never agents (§5.3, decision 8).
4. Would-have gets its own glyph; X1 adds it to the grammar (§7.1).
5. ink-3 and `--wire` fail contrast; text uses ink-subtle, connectors use #3138's new `--wire` `#686870` (§4.2).
Guesses taken into the spec: template names, "Fix 2 things", the runs page and its `[` `]` toggle, run chip words, cancelled has no glyph, motion and density numbers. Changed: Describe it is hidden until P6, and Pin is a button, not bare `P`.
UX check of the round 1 frames (7 MUST, 15 NIT). Rules taken into the spec: a template opens on its first unfilled field's sheet (5 actions from Start to a passing test, §4.1); one test-fact shape in the header, and the "what did not run" sentence moves to the result strip (§4.7, §5.3); one primary per screen, with the header dropping to secondary beside a fix button or Reload (§4.7, §4.8); engine down wins over findings and offers "Start Sindri" (§4.8); Save step is off while Reload stands (§4.8); the confirm's second button shows only when a test it may run is left (§5.3); Enter and Ctrl+Enter both confirm (§4.6); a Would have tab and "Failed at {step title}" on the run page (§7.1); "notification", not "ping", in user-facing text. The rest are drawing fixes for round 2.

Meta UX pass (2026-11-12, frames 03d604d5aa). Four fixes, each applied to every place that stated the old behaviour.
1. Template A is 5 actions, not 4. The agent field opens prefilled from `workflow.agent.target`, or with the first installed agent when that is unset, so only the repo is left ("1 thing left"). With no agent installed the walk is 6 actions. The card keeps "needs: GitHub connection". This reverses round 2 (product 3), where the fill step set `workflow.agent.target` (W1, §4.1).
2. The "Test failed" example no longer uses `SND-LAUNCH-003`: a fenced test stubs `run_agent` and preflight checks the agent target before any test, so that code shows only in a with-agents test or a live run (§6.1 says so). The example is a new row, `SND-GRAPH-113`, on A's Review step (step 2, Run an agent): `Review: "What should it do?" came out empty. trigger.pr.body had no value in this input.` The fix button is "Edit What should it do?" and focuses that field. A fenced test hits it because the references resolve against the picked event before the stub runs, for example a PR with no description.
3. `SND-GRAPH-103` gets a second action, "Open {step}" (for example "Open Plan"), because "Retry with model" fails the same way on a usage limit. Retry with model stays primary. This refines round 1 call 2 ("one fix button").
4. Activate is one click after any passing test, fenced or with model. Only an untested revision confirms ("Not tested yet. Activate anyway?", with the second button "Test run first"). The "Nothing real ran ... Activate anyway?" confirm and its "Test with model first" button are gone; the result strip under the header carries the "what did not run" line. This reverses round 2 (product, "Activate confirms after it too"), round 3 (product 5) and round 1 call 3 for the passed case. The run page's mode word is "Fenced" everywhere (header, tested fact).
