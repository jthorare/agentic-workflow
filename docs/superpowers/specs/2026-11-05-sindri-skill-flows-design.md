# Sindri skill flows: chain repo skills from one trigger, and jump into any running one

Status: SKELETON (2026-11-05). Engine is writing it; ui writes §6. No ready until 3 /autoplan rounds, meta's UX check and the owner's read. Lane: engine (lead) + ui. Related: the engine spec (2026-10-10), the session launcher (2026-10-15), the sandbox (2026-10-18), the Linear hub (#2077, context).

Research behind this note (file:line refs): `~/repos/aw-wt/briefs-engine/flow/flow-internal.md` (Sindri today), `flow-external.md` (n8n, Node-RED, Windmill, Kestra; Avalonia node editors; reference languages), `flow-decisions.md` (canvas and draft rules agreed with ui).

## 0. The owner's ask (2026-11-05, via meta)

"I was trying to take the types of skills that I have in my repo and wire them together to create workflows that require one trigger from me and I'm able to interject myself into an agent session whenever I need to." First release:
1. Skills as blocks: each repo skill (officeHours, system-design, autoplan, specToProvenPR, bugFixOrchestrator, review, formal-verify...) is a block that runs as an agent session with typed inputs and outputs (the artifacts it writes).
2. Wire them into a chain that starts from one owner trigger (a button, a CLI verb; a Linear issue later).
3. Jump in anytime: open a running block's live agent session (Warp + the agent CLI), work in it, hand back. Built on the existing takeover / handback.
The n8n-style palette (Slack, cron, data and code blocks) is a later phase.

## 1. Side by side (one page)

TODO: three columns: what the owner wants / what Sindri has today (with file refs) / the n8n-style pattern. Rows: block unit, typed IO, wiring, trigger, run history, test one block, jump in, human question, loops, parallel, versioning of edits under running runs, editor.
Facts to use (flow-internal.md):
- Sindri runs every instance as one Temporal `RecipeWorkflow`, decisions from the pure `TransitionDecision` (same code as `recipe eval`), facts as sha256 files, every transition in the ledger. 28 shipped recipes, 163 blocks; no Slack / cron / webhook / code block; only explicit starts.
- No Sindri block runs a skill today. Sindri's recipes re-implement the skills (scoping ~ officeHours, line ~ specToProvenPR, bugfix recipes ~ bugFixOrchestrator, review-loop ~ review).
- Skills already have a machine-readable result: `skills/_shared/output-contract.md`, `AW_RESULT=1` -> `$AW_DIR/results/<skill>-<ts>.json` + an `aw-result` block (`skill`, `artifacts`, `summary`, extras like `verdict`, `feature_dir`, `handoff_path`). Skills chain today by "Next steps" text + newest-dir discovery, not typed inputs.
- Takeover already opens the step's tmux session (socket `sindri-launch`) in the launcher's viewer (Warp), pauses the state timer, refuses the agent's own `complete` while taken over; the agent keeps running; handback re-arms the timer.
- n8n: connections keyed by node name (renames break), runtime-only JS expressions with no static types, DB-polled waits. Windmill / Kestra reference earlier outputs by step id. Borrow ideas, not code (n8n is Sustainable Use License).

## 2. Decisions so far (engine's picks; each gets tested in the autoplan rounds)

D1. **A skill is a block made from a small manifest, not C#.** `skills/<name>/block.yaml` declares `inputs` (typed: text, path, pr, issue, packet dir), `outputs` (keys mapped from the `aw-result` JSON: `artifacts[...]`, `feature_dir`, `verdict`, `handoff_path`), and `outcomes` (from `verdict`, else `done`). Sindri loads the manifests at serve start as descriptors `skill.<name>` (runtime agent). The descriptor hash covers the manifest only; the launch records the SKILL.md sha for audit (owner Q: pin SKILL.md too? pinning means every skill edit drift-parks running flows).
D2. **The block's session:** an agent Step through the existing launcher, seeded with `/<skill> <args from inputs>` and `AW_RESULT=1`. It reports back with `sindri workflow complete` per its closing instruction; NEW fallback: when the session goes idle or ends, Sindri reads the newest `results/<skill>-*.json` written after the launch and completes the step from it (beside today's `.sindri-out` recovery).
D3. **Asking is not idle.** Skills ask the user (AskUserQuestion). Today an idle session is re-prompted, then failed after 10 min (sindri-workflow.md ~1470-1475). The session collector already reports `asking` (`docs/csharp/sindri-sessions.md:24`). A skill block in `asking` becomes a needs-you item "<block> asks you something" with **Open session** (= takeover + viewer), and is never idle-failed. Handback when done.
D4. **The flow format compiles to a recipe; it doesn't replace recipes in release 1.** A flow file (`flows/<id>.yaml`: trigger inputs, blocks with `use` + `with` refs `{ ref: <block>.<output> }`, routing on outcomes, capped back-edges) compiles to the recipe long form, so the validator (74 RCP rules), evals, approval, pins, Temporal and the ledger all apply unchanged. Refs are by block id (not display name). Positions live in a sidecar `<id>.layout.json`, outside the hash (today the hash covers every text field, flow-internal.md §9). Meta's "graph replaces recipe YAML" is the later phase if the compiler hits a wall; §8 says what would prove that.
D5. **Sequential with branches and capped loops in release 1.** A recipe has one active state at a time; parallel blocks need child workflows (later phase). Default routing: `done` -> next block; `failed` -> a desk "retry / open session / stop".
D6. **Trigger:** "Run" on the flow in the app and `sindri flow run <id> [--input k=v]`, both through `workflow start`. Linear issue ("Run flow on this issue") after the hub spec lands (#2077).
D7. **Skills that push or open PRs** (specToProvenPR, bugFixOrchestrator, shipRelease): agent Steps today get scrubbed GitHub tokens and a pre-push hook that refuses pushes (sindri-launcher.md:238-262). Recommendation: a per-block `push` capability the owner grants at approval (shown in the preview), which passes the token to that block's session only. Owner Q.
D8. **Sandbox:** skill blocks write `$AW_DIR` (`~/.agentic-workflow/<slug>/`), outside the worktree: the plan mounts it writable. Check per skill against `requires.yaml` (bins, mcp).
D9. **Canvas (ui, §6):** custom, on `ZoomBorder`, `GraphEdgesControl`, `MiniMapControl` and the run canvas; no third-party node editor unless one passes the UI AOT smoke clean. Server-side drafts, a candidates call at drag start, keyboard wiring, optimistic edits, undo as inverse ops, no ledger rows for draft edits, `draft.changed` events, one draft per graph (the assistant edits it too), Health lists drafts idle > 14 d, test results keyed to (draft, rev). Full list in flow-decisions.md.
D10. **References:** structured `{ ref: block.output[.path] }` checked against the producing block's declared outputs at validate time. Computed values (a CEL subset) only in the n8n phase.

## 3. The skill block (D1, D2, D3, D7, D8): TODO

## 4. The flow format and the compiler (D4, D5, D10): TODO, with one worked example: idea -> officeHours -> autoplan (NEEDS_WORK loops back, cap 2) -> specToProvenPR, and its compiled recipe.

## 5. Trigger, run page and jumping in (D3, D6): TODO

## 6. The canvas (ui writes this; D9): TODO

## 7. API (candidates, drafts, flow run): TODO

## 8. Phases and the parallel plan: TODO
Draft: F-1 skill manifests for 6 skills + `skill.*` descriptors + the result fallback (engine); F-2 asking -> needs-you + Open session (engine + ui); F-3 flow format + compiler + `flow validate|eval|run` (engine); F-4 run page for flows (ui); F-5 canvas editor + drafts + candidates API (ui + api); F-6 Linear trigger (context); later: parallel via children, n8n palette (cron, Slack, code, logic), native graph runtime if the compiler hits a wall.

## 9. Owner questions: TODO
Known so far: pin SKILL.md in the hash (D1); push capability per block (D7); release 1 editor = canvas from day one, or flow file + run view first with the canvas in F-5.
