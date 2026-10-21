# L2 brief: Agents page rows tied to their Step (sindri-ui lane)

Read ~/.agentic-workflow/digests/sindri-ui-builder-rules.md FULLY first and follow it.
Worktree: /home/thor/repos/aw-wt/sindri-ui-l2, branch feat/sindri-ui-l2-agents-step. It is STACKED: L1's branch (#397, 323ff6ae1) + origin/main bb26d9a + api's #398 branch (feat/sindri-api-launch-fields, 8a35a6831), already merged in by the lead (head 9f46bdaaf). Neither #397 nor #398 is on main yet. If api pushes more to #398, merge `origin/feat/sindri-api-launch-fields` again (never rebase). Don't change files that belong to #397 or #398 except where this brief says to.
Your digest: ~/.agentic-workflow/digests/sindri-ui-l2.md (keep it current).

## Goal
The owner watches an item run through workflow.line with agents doing the Steps. On the Agents page, a session that an engine launch started must say which instance and Step it works on, and take you there. The frame's agent Step (L1, #397) must link back to its session.

## What is served (read the goldens; they win over this summary)
- Session rows (GET /sindri/sessions, GET /sindri/sessions/{id}): `session.launch { instance, state, attempt }` next to the existing `launchId`. It is null when no launch row is known; its members are null for an attended (human-started) launch. Golden: tests/Aw.Sindri.Tests/Golden/Api/sessions-id/cursor-engine-launch.json (instance "ITM-…/step/1", state "build", attempt 2).
- GET /sindri/launches/{id}: `step { instance, state, attempt }`, always present, members null when absent. Goldens: Api/launches-id/started-engine.json, failed-engine.json.
- launch.started / launch.failed events: flat `instance`, `state`, `attempt`.
- Items with a live session: Api/items-id/live-session.json (attach, lastStarted, confirm; an ended resumable session; one blocker).

## Build
1. Agents row (ViewModels/Agents/SessionRowViewModel.cs and its view): when `session.launch.instance` is set, add a line or chip "on <instance> · <state>", plus "attempt N" when N > 1. Its accessible name says the same. Attended launches and no-launch rows draw nothing new.
2. Open the frame from the row: a key (O, which the fixed keys already define as "Open with the resolved target") and a click on the chip open that instance's frame focused on the state, the way the Children panel opens a child frame (reuse its event/route, don't add a new one). The session sheet (Enter) shows the same instance/state/attempt as fields, with the open action.
3. Frame → session: on L1's agent-Step run line, when a live session for that launch is known (match a session row's `launchId` to `run.launch`), add "Session ▸" that opens the Agents page's session sheet for it. Use the shell's existing page navigation.
4. Optional, only if cheap: on the Agents page, filter "In a workflow" (sessions with launch.instance set), via the existing filters.
5. Move L1's UI-side Attach-ready fixture onto Api/items-id/live-session.json, if L1 left a fixture named after it (see tests/Aw.Sindri.Ui.Testing/AgentRunFixtures.cs). If P8 has merged by then, also swap the agent-run.json copy for the real golden. Update GoldenContractTests' _notRead honestly.
6. Labels, keys and a11y as the page already does them; mind the AW1005 300-line cap (partials).

## Tests (TDD, red first, unit lane)
Row line from cursor-engine-launch.json; nothing for a null launch or an attended launch (all-null members); attempt shown only above 1; O and the chip raise the open-frame request with the right address and state; the sheet fields; the frame's "Session ▸" appears only when a session with that launchId is known, and opens its sheet.

## Gate, then stop
Scoped per the rules: build -warnaserror of the touched projects; the Aw.Sindri.Ui.Tests unit lane (Golden/Api reads change, so run it); the touched integration classes once; Aw.Architecture.Tests; format verify. Push. Clear the build output. Open a PR "feat: Agents rows tied to their Step, and the frame links back" ending with:
🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01QWf7FzJiDWjUhPk4QZBTGX

Report the PR number, head sha, which tests ran with counts, what's left, and any decisions.
