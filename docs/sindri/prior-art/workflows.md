# Prior art: Workflow builder

This file is the prior-art ledger for the Workflows surface of the Sindri desktop app (rule: #2989).
Sources were read on 2026-11-11 from each product's own docs; "(vendor)" marks a vendor claim, "unconfirmed" marks what I could not verify.
Surface job: describe what should happen and get a running, proven workflow in under 2 minutes ("activate means it runs").
Direction issue: #2960 (gap issues #2968, #2969, #2970; engine #2258). Prior digest: `gap-bar-workflows.md` (its "(mem)" items re-checked here).
Sindri-today facts come from origin/main (`docs/csharp/sindri-graph.md`, `src/Sindri/Aw.Sindri.Ui/Views/Workflows/`).

Caveat: no screenshots were captured (research only, no browser). Links go to the doc page that describes the screen.

---

## 1. Build a first workflow from zero

**Best today**
- n8n: [Use templates](https://docs.n8n.io/build/ways-of-building-workflows/use-templates) + [n8n Assistant](https://docs.n8n.io/build/ways-of-building-workflows/ai-assistant). Start from a template or the chat agent; the result is a normal workflow you open and edit. source: docs
- Zapier: [Copilot](https://help.zapier.com/hc/en-us/articles/23503999825421). Pick trigger app and event, connect, test, add action. One step per screen, linear. source: docs
- Windmill: [flows quickstart](https://www.windmill.dev/docs/getting_started/flows_quickstart). Trigger, then step from Hub or code, then "Test this step". source: docs
- Retool Workflows: [blocks](https://docs.retool.com/workflows/guides/blocks/). Blocks added from the toolbar are NOT connected by default and show a warning until wired. source: docs

**What we take**: Zapier's one-decision-per-screen order (trigger, test it, action, test it, on). Retool's rule that an unconnected block is a visible warning, not a silent skip. Start screen offers three entries on one page: "Describe it", "Start from template", "Blank".
**Where one app wins**: the trigger list is Sindri's own objects (PR opened on a repo in the profile, item moved, agent run finished). Zapier needs an account connect for each.
**Desktop gain**: first paint of the start screen from the local store < 100 ms; entry to a drafted graph on canvas < 2 s after the agent answers.
**Sindri today**: `sindri wf create <slug> --template <name>`, UI "Start from" picker (`Labels.WorkflowsEmpty.cs:18`). No describe-it entry in the UI (#2960 stream 3a).
**Verdict**: take and change (three entries, one model).

## 2. Build from words (describe it, review a diff)

**Best today**
- n8n AI Workflow Builder: [docs](https://docs.n8n.io/build/ways-of-building-workflows/ai-workflow-builder). Natural-language prompt, live build phases, then you review credentials and parameters. Each create/modify message is one credit (vendor). Third-party reviews say it leaves placeholders for credentials (unconfirmed by n8n). source: docs
- n8n Assistant: [docs](https://docs.n8n.io/build/ways-of-building-workflows/ai-assistant). Chat agent that creates, edits, tests and troubleshoots; credentials chosen through a card in the chat, never pasted. source: docs
- Zapier Copilot: [help](https://help.zapier.com/hc/en-us/articles/23503999825421). Outline or step-by-step; Zapier's own best practices say start in "Ask as you build" mode (vendor). source: docs
- Windmill AI: [docs](https://www.windmill.dev/docs/core_concepts/ai_generation). AI chat can edit items, then opens the Compare & Deploy page with exactly those items preselected. source: docs

**What we take**: Windmill's "AI edits land in a compare view before deploy". n8n's "agent also tests and fixes". Credential via a card, not text. Pattern in all four: the AI makes a skeleton you still finish, so ours must run validate and a dry run itself before showing "done".
**Where one app wins**: the drafting agent sees real repos, real connections, real past runs in the ledger, so it can refuse a node whose repo is not in the profile at draft time.
**Desktop gain**: the draft is written through the local MCP tools (`workflow_create`, `workflow_apply_ops`); diff render of a 12-node graph < 100 ms.
**Sindri today**: MCP and CLI write paths exist (`sindri-graph.md` rows 126-128). UI never calls them for words. Missing.
**Verdict**: take and change (diff review, auto-validate, auto dry run).

## 3. Templates for the common jobs

**Best today**
- n8n: [templates](https://docs.n8n.io/build/ways-of-building-workflows/use-templates). Community library, import into canvas. source: docs
- Zapier: [Zap templates](https://zapier.com/templates) (page not fetched; unconfirmed detail). Per-app pairs. source: docs
- Windmill: [Hub](https://hub.windmill.dev). Shared scripts and flows (page not fetched; unconfirmed detail). source: docs
- GitHub Actions: [starter workflows](https://github.com/actions/starter-workflows). YAML files by language; MIT per repo. source: docs

**What we take**: a template is a full spec plus the first-run checklist (which connection, which repo to pick). The 10 jobs in #2960 are the gallery; each card shows trigger, step count and the last time it ran in this profile.
**Where one app wins**: "used by 3 of your repos, last ran 2h ago" on each card. No public gallery knows that.
**Desktop gain**: gallery of 10 cards from local files, no network, < 50 ms.
**Sindri today**: `WfTemplates` list hard-coded in UI (`Labels.WorkflowsEmpty.cs:64`, comment says there is no list route yet). Missing the 10.
**Verdict**: take and change.

## 4. Pick a trigger and test it with a real event

**Best today**
- Zapier: [use a previous Zap run as a test record](https://help.zapier.com/hc/en-us/articles/42896259263373-Use-a-previous-Zap-run-as-a-test-record). Trigger "Test" tab pulls a sample record; some apps return only generic samples, then you pick a past run. You can rename the record and edit values. source: docs
- Pipedream: [triggers](https://pipedream.com/docs/workflows/building-workflows/triggers/). "Select event" dropdown lists previously received events; "Send Test Event" fires the workflow with the chosen one. Header `x-pd-notrigger: 1` sends a test event without running production. NOTE: Pipedream's docs banner says Workflows are shutting down 2027-03-31 (vendor notice). source: docs
- Inngest: [dev server](https://www.inngest.com/docs/dev-server). "Test Event" sends a JSON event by name; Runs page shows steps. Dev server is open source. source: docs
- Windmill: [test flows](https://www.windmill.dev/docs/flows/test_flows). "Fill arguments from a request" and an input library of saved inputs. source: docs

**What we take**: the trigger step has a list of the last N real events from this box (a real PR, a real red CI run), pick one with Enter, edit JSON before use, pin it. Inngest's rule: event name decides the match, so we show "would match / would not match" for the picked event.
**Where one app wins**: the list comes from the ledger and the forge cache, so "PR #2911 opened 4 min ago on repo X" is there with zero setup and no account connect.
**Desktop gain**: list of last 50 events < 50 ms from local store; works offline.
**Sindri today**: `wf run --payload JSON` takes a hand-written payload (`sindri-graph.md:71`). No picker. Missing.
**Verdict**: take and change.

## 5. Map fields from real output (no expressions by hand)

**Best today**
- n8n: [reference data](https://docs.n8n.io/data/data-mapping/). Drag a field from the Input panel into a parameter; it writes the expression. Also the expression cliff: Latenode forum posts call expressions the stall point (third party). source: docs
- Windmill: [test flows](https://www.windmill.dev/docs/flows/test_flows). Outputs of tested steps are suggested as inputs of the next step. source: docs
- Dify: [variable inspector](https://docs.dify.ai/en/guides/workflow/debug-and-preview/variable-inspect). Panel at the bottom lists every node output after it ran; click a variable to see it all. source: docs
- Zapier: field picker shows sample values from the test record next to each field name (unconfirmed page; seen in the editor description in help articles). source: docs

**What we take**: in a field, type `/` or click "insert" to open a picker of upstream outputs with the real value beside each name (from the pinned/last run). Human captions (#2925). Raw `{{node.out}}` stays under Advanced.
**Where one app wins**: output fields carry types from the block catalog, so GRAPH-REF-004 type mismatches show as you pick, not at activate.
**Desktop gain**: picker opens < 50 ms with 200 fields.
**Sindri today**: validator has GRAPH-REF-001..005 (`sindri-graph.md:28`). UI is a text field; the picker is missing. Related #2925, #2968 (Prompt output as JSON Schema by hand).
**Verdict**: take and change.

## 6. Validate before activate ("activate means it runs")

**Best today**
- actionlint: [README](https://github.com/rhysd/actionlint). Checks workflow syntax, `${{ }}` expression types, action inputs/outputs, reusable workflow inputs, shellcheck/pyflakes on `run:`, script injection. MIT. Static only; cannot know a secret is missing or a repo is gone. source: docs
- Retool: [blocks](https://docs.retool.com/workflows/guides/blocks/). Unconnected blocks show a warning. source: docs
- Windmill: [draft and deploy](https://www.windmill.dev/docs/core_concepts/draft_and_deploy). Drafts autosave and can be tested before deploy; no stated deploy-time refusal list (unconfirmed). source: docs
- n8n / Zapier: rely on you having tested each step (digest finding; I found no refusal list in docs). source: docs

**What we take**: actionlint's catalog style (a stable id per check, one line of cause, one of fix, example input). Findings anchor on the node (problem chip on the step). Activate disabled while any blocking finding exists, with the list in the button's tooltip. Add the part nobody does: run-time-knowable holes (missing connection scope, unreachable repo, no trigger permission, code compile).
**Where one app wins**: validate can read the profile, repo list, connection grants and block catalog, so it checks what actionlint cannot.
**Desktop gain**: validate result on every edit < 150 ms for a 20-node graph (in-process).
**Sindri today**: server validator exists with SND-GRAPH findings, run at create/edit/activate (`sindri-graph.md:139`). UI never calls the validate route (#2969). Hole list is the #2960 stream 1.
**Verdict**: ours is better in engine; UI missing. Take actionlint's display.

## 7. Dry run with side effects fenced

**Best today**
- Windmill: [test flows](https://www.windmill.dev/docs/flows/test_flows). "Test flow", "Test up to step", "Test this step", "Test an iteration". Runs for real (side effects not fenced; stated nowhere that they are). source: docs
- n8n: [pin and mock data](https://docs.n8n.io/data/data-pinning/). Pinned data replaces the external fetch so triggers and reads are not repeated; manual executions are not counted in quota. Writes still happen on a manual run. source: docs
- act: [nektos/act](https://github.com/nektos/act). Runs GitHub Actions locally in Docker. MIT. Gaps per the prior digest (no service containers, matrix, artifacts, `GITHUB_TOKEN`) were not re-verified here. source: docs
- wrkflw `--dry-run`: unconfirmed (digest cites a third-party page; not fetched). source: docs

**What we take**: nobody fences side effects well, so this is ours. Test run = recorded or picked trigger event, every effect block (notify, http POST, create_item, run_agent writes) replaced by a "would do" record that shows the exact payload. Read blocks run for real. Show each node's input and output.
**Where one app wins**: Sindri knows which blocks write (block catalog), so the fence is by block, not by user promise.
**Desktop gain**: a 6-node dry run without agent nodes < 3 s end to end; no network.
**Sindri today**: none. "Run now" is a real run (#2969). Missing.
**Verdict**: ours to build; take n8n's pin idea for the input.

## 8. See each step's input and output in a run

**Best today**
- Inngest: [inspect runs](https://www.inngest.com/docs/platform/monitor/inspecting-function-runs). Run page: trigger, event payload, step timeline; expand a failed step to see each retry's error and timing. CEL search over `event.*` and `output.*`. source: docs
- Pipedream: [inspector](https://pipedream.com/docs/workflows/building-workflows/inspect/). Event list left of the workflow; click an event for data and logs per step. source: docs
- Windmill: test-flow drawer shows the DAG with results and logs per step ([docs](https://www.windmill.dev/docs/flows/test_flows)). source: docs
- Dify: ["Last run" on any node](https://docs.dify.ai/en/guides/workflow/debug-and-preview/step-run) shows input, output, timing, error. source: docs

**What we take**: run view = the canvas with a status dot per node; click a node, bottom panel shows Input / Output / Error / Time. Inngest's CEL-style search becomes a filter on output fields. Collapse large payloads at the store's 64 KB cut with a "truncated" badge.
**Where one app wins**: a node that ran an agent links to the live agent session, its worktree and the PR it opened.
**Desktop gain**: open a run with 50 nodes < 200 ms; scroll 10k log lines at 60 fps.
**Sindri today**: `workflow_node_run` stores input/output (cut at 64 KB; `sindri-graph.md:63`), `GET /sindri/workflow-runs/{run}`; UI has `NodeRunViewModel`. Depth of the UI view not verified here.
**Verdict**: take and change.

## 9. Re-run from a step with edited input

**Best today**
- Windmill: [restart from step](https://www.windmill.dev/docs/flows/test_flows). Select a top-level node in the preview page, click "Re-start from X"; for-loop and branch-all can restart at an iteration. On deployed flows only Cloud/Enterprise. source: docs
- Inngest: [rerun a run](https://www.inngest.com/docs/platform-and-operations/rerun-a-run). "Rerun from step" restores saved outputs of earlier steps and re-runs from the chosen step; some steps let you replace JSON input. Cron-triggered runs cannot be rerun. source: docs
- Dify: [variable inspector](https://docs.dify.ai/en/guides/workflow/debug-and-preview/variable-inspect). Edit a cached variable, run just the downstream node; the original "Last run" record is untouched. source: docs
- Make: [run replay](https://help.make.com/scenario-run-replay). Replays the whole scenario with the old trigger data, current scenario version, costs credits; replays all modules, even ones that succeeded. source: docs
- n8n: [debug executions](https://docs.n8n.io/workflows/executions/debug/). "Debug in editor" copies a failed execution's data and pins it on the first node. source: docs

**What we take**: Inngest's model (saved outputs of earlier nodes restored, the chosen node and later ones re-run) with Dify's edit-a-variable-first gesture. Button "Re-run from here" on the node; an Input editor opens prefilled; the new run is linked to the old one.
**Where one app wins**: since agent nodes made branches and PRs, Sindri can show "this node already opened PR #2911; re-run reuses it" (see problem 12).
**Desktop gain**: restore of earlier outputs from local rows, start of the re-run < 300 ms.
**Sindri today**: none in the UI; runs are keyed `<slug>@<rev>:<occurrence>` with a primary-key refusal on a second insert (`sindri-graph.md:63`). Takeover/handback exists for agent nodes. Missing.
**Verdict**: take and change.

## 10. Understand why a run failed, in plain words, and fix in one action

**Best today**
- Inngest: [inspect](https://www.inngest.com/docs/platform/monitor/inspecting-function-runs). "The run's error tells you what failed. The timeline tells you where."; structured error name/message; still no suggested fix. source: docs
- Temporal UI: [web UI](https://docs.temporal.io/web-ui). Event history per execution; actions on the page: cancel, signal, update, reset and terminate, and "Start Workflow Like This One". source: docs
- Make: [error handling](https://help.make.com/scenario-run-replay) (nav lists error handlers, exponential backoff, throw). Per-route handlers you build yourself. source: docs
- n8n: Botpress (a competitor) says n8n debugging means clicking through raw JSON node by node (vendor claim, via prior digest). source: docs

**What we take**: failed-run header sentence: "Step 'Check' failed: repo acme/api is not in your profile." Below it one primary button chosen from the cause (Reconnect, Add repo, Edit field, Retry from this node). Raw code stays behind "Details". Map each SND-GRAPH / node failure code to a sentence plus an action in one table, test per row.
**Where one app wins**: the fix button can run the fix (add the repo to the profile, regrant the connection) because the profile lives in the same app.
**Desktop gain**: failure view with its fix button shown in < 100 ms after the run event arrives on the stream.
**Sindri today**: failures are stored per node with text; no sentence-to-action table. Part of #2960 stream 4. Missing.
**Verdict**: ours to build.

## 11. Versions, undo and diff

**Best today**
- n8n: [workflow history](https://docs.n8n.io/workflows/history/). New version on every save, restore, and git pull; Restore / Clone to new / Open in new tab to compare / Download / Name version. Full history is Enterprise; 24 h for everyone else (vendor tiering). source: docs
- Windmill: [draft and deploy](https://www.windmill.dev/docs/core_concepts/draft_and_deploy). Drafts autosave per user; AI edits open Compare & Deploy. source: docs
- Temporal: workflow versioning/pinning by build id ([CLI reset options](https://docs.temporal.io/cli/workflow#reset)), runs stay on their version. source: docs
- Git-based self-built flows (Actions YAML): the diff is `git diff`. Plain, reviewed. source: docs

**What we take**: every save is an immutable revision (we have it); add a revision list with a side-by-side node diff (added / removed / changed fields) and "Restore as new revision". Name a revision. In-editor Ctrl+Z / Ctrl+Shift+Z for the draft.
**Where one app wins**: the revision list says who changed it (`owner` or `session:<id>`) and which runs used each revision.
**Desktop gain**: diff of two 30-node revisions < 100 ms.
**Sindri today**: revisions with spec hash, 409 on stale base, `createdBy` (`sindri-graph.md:50, 118, 139`). No revision list, diff or undo in UI (no Ctrl/undo binding found in `Views/Workflows`). 
**Verdict**: ours is partly better (store); UI missing.

## 12. Idempotent side effects on replay

**Best today**
- Inngest: [idempotency](https://www.inngest.com/docs/durable-execution/guides-and-advanced/idempotency). Event id dedups starts for 24 h; function-level idempotency expression keyed on event data; rerun doc says check completed side effects first. source: docs
- Pipedream: replay re-runs against the newest version; the prior digest cites a user thread of duplicate Airtable records after a replay (third-party thread, not re-fetched). source: docs
- Make: replay passes data through all modules, even ones that already succeeded ([docs](https://help.make.com/scenario-run-replay)). source: docs
- Temporal: reset keeps history up to the chosen event ([CLI](https://docs.temporal.io/cli/workflow#reset)); activities should be idempotent (community threads in prior digest, not re-fetched). source: docs

**What we take**: effect nodes get a deterministic key `run + node + item` (Sindri already builds launch ids from `agent-launch:<run>:<node>:<index>`, `sindri-graph.md:79`). On re-run, an effect node shows "done before: comment #5521" and skips by default, with "do again" as an explicit toggle.
**Where one app wins**: the key plus the real object (the comment, the PR, the branch) is in one ledger, so "skipped, already posted" can link to it.
**Desktop gain**: dedupe lookup is a local index read < 5 ms.
**Sindri today**: agent launches are replay-safe by id; other effect blocks (notify, http, create_item) unconfirmed. 
**Verdict**: ours is already better for agent nodes (proof: id formula above); other blocks to check.

## 13. Keyboard and canvas editing

**Best today**
- n8n: [shortcuts](https://docs.n8n.io/keyboard-shortcuts/). `N` opens nodes panel, `Enter` inserts, arrows select neighbour nodes, `F2` rename, `D` deactivate, `P` pin data, Ctrl+G group, `1` zoom to fit, `Shift+S` sticky note. source: docs
- Retool: [blocks](https://docs.retool.com/workflows/guides/blocks/). Right-click canvas for Add block; drag to connect; Run-with-previous-blocks on a block. source: docs
- Windmill: [editor components](https://www.windmill.dev/docs/flows/editor_components). Flow editor with a graph plus side panel (details not re-read). source: docs
- Node-RED / Langflow: unconfirmed here. source: docs

**What we take**: n8n's set, as is: arrow keys walk the graph, `N` or Ctrl+K add a step, `F2` rename, `D` disable a step, `P` pin, `1` fit, Enter opens. Plus the Ctrl+K command palette from the grammar (#2978) and undo.
**Where one app wins**: palette lists workflow actions beside the rest of the app (open the PR this node made).
**Desktop gain**: key to visible response < 50 ms; pan/zoom of 200 nodes at 60 fps.
**Sindri today**: canvas drag-pan and Ctrl+wheel zoom (`GraphCanvas.cs:13`); AutomationPeer exists. No node-key navigation, no undo found. #2797 (canvas space).
**Verdict**: take as is.

## 14. Secrets and connections

**Best today**
- Windmill: [variables and secrets](https://www.windmill.dev/docs/core_concepts/variables_and_secrets). All variables encrypted with a workspace key; secret ones not readable in UI. source: docs
- n8n: [credentials](https://docs.n8n.io/credentials/). One credential object per service; assistant chooses via a card, never in chat. source: docs
- Zapier: connection picked per step, with reconnect on expiry (help articles; page not fetched). source: docs
- Pipedream: connected accounts (not re-read). source: docs

**What we take**: a node's connection slot shows name, scope and state ("github:me, repo read, ok / expired"), one click to reconnect; a missing one is a validate finding with a Connect button. Never paste a secret into the describe-it box.
**Where one app wins**: one connection list for workflows, reviews and messages.
**Desktop gain**: secret in the OS keychain, no cloud store; state check local < 50 ms.
**Sindri today**: `GRAPH-NAME-001` refuses a missing connection (`sindri-graph.md:31`); sandbox adds a group per named connection (`:79`). UI slot unverified.
**Verdict**: take and change.

## 15. Know if it is on, and when it runs next

**Best today**
- n8n: [executions](https://docs.n8n.io/workflows/executions/). Manual vs production executions; only production counts toward quota; publish puts it on. source: docs
- Zapier: a toggle in the editor and list (help articles; not re-read). source: docs
- Temporal / Inngest: run list with status and filters. source: docs

**What we take**: state chip beside the title ("Active, next weekday 7:00" / "Off"), the same glyph as the list row. Run list shows manual vs scheduled.
**Where one app wins**: Home inbox (#2978) shows a workflow parked on the owner as a row.
**Desktop gain**: next-run time computed locally, correct offline.
**Sindri today**: only the button verb tells you (#2970). Scheduler computes ticks (`sindri-graph.md:153`).
**Verdict**: take as is.

---

## Closing table

| Problem | Best tool | Verdict | Sindri today (issue) |
|---|---|---|---|
| 1 First workflow from zero | Zapier steps, Retool unconnected warning | take and change | templates only, no describe (#2960) |
| 2 Build from words | n8n Assistant, Windmill compare view | take and change | missing in UI (#2960) |
| 3 Templates | n8n, Actions starters | take and change | hard-coded list, not the 10 (#2960) |
| 4 Trigger + real event | Pipedream, Zapier, Inngest | take and change | hand-written payload only |
| 5 Map fields | n8n drag, Windmill suggest | take and change | text fields (#2925, #2968) |
| 6 Validate before activate | actionlint | ours better (engine), UI missing | #2969 |
| 7 Dry run, effects fenced | none solid | ours to build | missing (#2969) |
| 8 Per-step input/output | Inngest, Dify | take and change | stored, UI depth unverified |
| 9 Re-run from step, edited | Inngest + Dify | take and change | missing |
| 10 Failure in words + fix | Inngest, Temporal actions | ours to build | missing (#2960) |
| 11 Versions, undo, diff | n8n history | partly ours, UI missing | store done, no UI |
| 12 Idempotent replay | Inngest | ours better for agents | agent ids done |
| 13 Keyboard/canvas | n8n shortcuts | take as is | pan/zoom only (#2797) |
| 14 Secrets/connections | Windmill, n8n | take and change | validator only |
| 15 On/off + next run | n8n, Zapier | take as is | #2970 |

## Licence notes
Windmill core is AGPLv3 (its README): read docs and behaviour only, copy no code. n8n is Sustainable Use License with `.ee.` files separate: docs only. act and actionlint are MIT (check LICENSE before copying code). Inngest dev server is described as open source (licence file not checked).

## Unconfirmed / thin
- Dry run with side effects fenced: nobody found; wrkflw `--dry-run` unconfirmed.
- Zapier template gallery, Windmill Hub, Node-RED, Langflow editing keys: not read.
- Temporal reset pitfalls and Pipedream duplicate-record thread come from the prior digest, not re-fetched.
- Self-built agent-workflow tools (Sim, Heym, Disp8ch, Rowboat, EpicStaff) surfaced by search; none checked for features beyond a search summary.

## What we take

1. Validate before Activate: findings sit on the step, and Activate stays off while a blocking finding exists (#2969).
2. Test run with side effects fenced: every effect step shows a "would do" record with its exact payload instead of acting (#2969).
3. "Describe it" entry: words make a skeleton, which shows as a diff, is validated and dry-run before you accept it (#2960).
4. Trigger step lists the last real events from this box; pick one, edit it, pin it, and see "would match / would not match" (#3032).
5. Failed-run header says the cause in a sentence and offers one button chosen from the cause (Reconnect, Add repo, Retry from this node) (#2960).
6. State chip beside the title ("Active, next weekday 7:00" / "Off"), the same glyph as the list row (#2970).
7. Field picker: type `/` in a field to choose an upstream output with its real value beside the name (#3033).
