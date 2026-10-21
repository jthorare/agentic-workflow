# L1 brief: agent-run Step on the instance frame, with Attach (sindri-ui lane)

Read ~/.agentic-workflow/digests/sindri-ui-builder-rules.md FULLY first and follow it.
Worktree: /home/thor/repos/aw-wt/sindri-ui-l1, branch feat/sindri-ui-l1-agent-step, created from origin/main AFTER #295 (the launcher) merged.
Your digest: ~/.agentic-workflow/digests/sindri-ui-l1.md (keep it current).

## Goal
The owner wants to watch an item run through workflow.line with agents doing the Steps. On the instance frame's expanded state, an agent-run Step must read clearly and offer Attach.

## What is served (read the goldens; don't trust this summary over them)
- Engine P8 (branch feat/sindri-wf-p8-agent-runtime, NOT on main yet; lands ~Monday midday): GET instance status gains an optional `run: {runtime: "agent", launch: "LCH-…"}`, omitted when null. Golden: `tests/Aw.Sindri.Tests/Golden/Api/workflow-status/agent-run.json` on that branch (read it with `git show origin/feat/sindri-wf-p8-agent-runtime:<path>`). header.detail "agent running"; commands takeover + cancel (ApplyCommand:*). Events: `step.completed` from an agent carries `via: "agent"` and `tokens: N`; `step.failed` carries reason launch|lost|exit|limit, then the instance moves to the state's desk twin or a park (no retries).
  The UI's `run` field and the via/tokens/reason fields must be OPTIONAL, so the PR is correct whether P8 is merged or not. Until P8 merges, use a UI-side test fixture copied from agent-run.json (say so in a comment naming the source path); don't add it to the api golden set.
- API #295 (on main): GET /sindri/launches/{id} → LaunchView (id, item, mode, state, target, chosenBy, requestedAt, outcomeAt, code, detail, stage, sessionId, tabTitle, viewer, late, attach, catalogSource, argvHash, ended, asOf). Events launch.requested/started/failed, session.ended, session.usage. Goldens: tests/Aw.Sindri.Tests/Golden/Api/launches-id/*, items-id-open/*, items-id/* (the `launch` block), commands/catalog.json, events-catalog.
- Attach: catalog action `OpenItem:attach` (class desk): POST /sindri/items/{itemId}/open with {mode:"attach", itemVersion}. It's ready per item: GET /sindri/items/{id} → `launch.modes` contains "attach" and `launch.attach` is non-null. The SERVER opens the viewer; the client execs nothing. With the print viewer the answer carries the tmux attach argv; show it with Copy. Refusals to show as sentences: SND-LAUNCH-006 (no live session), -008 (launch in flight), -001 (viewer failed), -009 (viewer timeout), -002 (no viewer), SND-ITEM-409 (stale/closed). A session caller gets SND-AUTH-002.

## Build
1. Expanded state (ViewModels/Instance/ExpandedStateViewModel.cs, Views/Instance/ExpandedStateView.axaml): when `run.runtime == "agent"`, show a run line: "Agent · LCH-…" plus the launch's state from GET /sindri/launches/{run.launch} (requested / started with viewer / failed with code+detail), refreshed from the launch.* and session.ended events on the stream.
2. Wire the existing Attach component (ViewModels/Components/AttachControlViewModel.cs, AttachStepKind.Agent, Views/Components/AttachButton.axaml), which nothing uses yet. Primary action Attach → OpenItem:attach for the instance's item; ready from the item's launch.modes/launch.attach; while not ready show the component's pending chip, but REPLACE its "Available with P10a" wording with the served reason (e.g. no live session). Take over and Cancel in its chevron route to the existing ApplyCommand rows (keep the served confirm sentence).
3. Events tab / band: a completed agent step reads "via agent · N tokens"; a failed one reads its reason as a plain sentence ("The agent's launch failed", "The session was lost", "The agent exited", "The agent hit its limit") and says where it went (desk twin or park), from the served state.
4. Keys, a11y names and Labels as the existing frame does it (Labels.*.cs); mind the AW1005 300-line cap (split partials as the frame already does).

## Tests (TDD, unit lane)
Red first, per behavior: run line from the fixture; launch state mapping from each launches-id golden; Attach ready/pending/refused from the items-id and items-id-open goldens (the ready case: api's follow-up adds Golden/Api/items-id/live-session.json with launch.attach populated; use it if it's on main, else a UI-side fixture named after it, and tell the lead); via/tokens and each failure reason sentence; no run → nothing new drawn (the existing frame goldens unchanged). If you add catalog rows, action ids or Golden/Api reads, run Aw.Sindri.Ui.Tests (GoldenContractTests keeps a _notRead list; update it honestly).

## Gate, then stop
Scoped per the rules: build -warnaserror of the touched projects; the unit lane of Aw.Sindri.Ui.Tests, Aw.Sindri.Ui.Rendering.Tests (if views changed) and Aw.Architecture.Tests; format verify. One heavy script. Push. Clear the build output when done. Report head sha, which tests ran with counts, and what's left (the Agents row → Step link is L2, NOT yours).
