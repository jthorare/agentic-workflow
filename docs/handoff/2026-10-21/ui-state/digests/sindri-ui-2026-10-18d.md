# Sindri UI lane digest, 2026-10-18 d (read this FIRST; supersedes -18c)

Session sindri-ui; meta sindri-meta. Lane map v13: api=sindri-api, engine=sindri-engine, context=sindri-context, ui=sindri-ui, ideas=sindri-ideas. Main ae3fd0d.
Rules and background: digests/sindri-ui-2026-10-18.md §4 and orchestrator/handoff/ui.md (its end has the follow-up list). Builder rules file (every brief points at it): ~/.agentic-workflow/digests/sindri-ui-builder-rules.md
Owner rule: fix issues only; resolve suggestions and nits in writing. Review state files: ~/.agentic-workflow/joi-fairshare-agentic-workflow/reviews/<N>.json (+ .runK.json).

## In the meta's train (ready sent; do NOT merge main into them, since the train merges main itself)
- #342 U9a0 Context: `ready #342 75eb9371b`. It failed on a flaky Onyx port test and re-gates in train 21 with #345.
- #345 Answer in the child: `ready #345 01a8e4e08`. Proof PARTIAL; the live re-proof comes after engine P7.
- #351 U5' Start form: `ready #351 b8d8ef928`; it follows. Run-3 suggestions and nits are a follow-up PR (handoff list).
If the meta bounces one: fix it in its worktree (aw-wt/sindri-ui-u9a0, -answer-child, -u5p), push, and re-send `ready #N <sha>` (first line only).

## Held
- #356 (motion crash and startup focus), aw-wt/sindri-ui-motion-poll, head 607c31041, review loop done (run 3: 0 and 0, 1 nit cor-6). HOLD until #342 merges. Then resume a builder:
  - merge main;
  - OnOpened runs FocusContextAnchor through a helper shared with OnPageChanged;
  - StartupFocusTests' Context case asserts AnchorBox;
  - fold cor-6 (FocusOpenedSheet treats window focus as none, one helper);
  - gate, PR body, ready, `ready #356 <sha>`.

## U11a (Agents page): plan plans/sindri-ui/u11a-plan.md, verification plan proof/sindri-ui-u11a/verification-plan.md (Q1–Q10 defaults logged by the meta)
- T0 is at 57902dabd on feat/sindri-ui-u11a (aw-wt/sindri-ui-u11a). The seams are in digests/sindri-ui-u11a-t0.md.
- B (sheet T3/T4) is DONE at 3a1c707b1 on feat/sindri-ui-u11a-sheet (digests/sindri-ui-u11a-sheet.md).
- C (counts, tray and fixture verb T5) is DONE at 0591deafa on feat/sindri-ui-u11a-fixture (digests/sindri-ui-u11a-fixture.md).
- Builder A STOPPED at the recycle: T1/T2 WIP at **5e026cd3c** (pushed, clean, builds 0 warnings). The Agents/Page tests have NOT yet run green (heavy queue; an earlier hang on a fake-clock await was fixed). Its digest is digests/sindri-ui-u11a-page.md. NEXT: brief a fresh Sonnet builder from that digest. Run the Agents tests green, then T6 docs, merge B and C, merge main, gate, the live proof on :94 (temp AW_STATE_DIR or crop), then a draft PR.
  - ISessionRef on rows;
  - SheetName in the plan's form;
  - resume Copy on the ended rows;
  - the count live region;
  - C's one-line edits in HealthViewModel and ShellViewModel.Attention.
- Proof caveat: Sindri reads teammate-names.jsonl from the real $AW_STATE_DIR. Run the control plane with a temp AW_STATE_DIR, or crop the screenshots.
- After the draft PR: /review loop (agent), PR body, ready.
- Blocked/later: Focus send (CO-4a, U11a-f), live preview (CO-7b, then CO-7c), recipe groups (CA-6/P10a), asking on Home (owner Q3), aw:fleet hook (CO-1).

## Cross-lane state
- Context #359 (S-LB1b subgraph): OK; its answers are in. U9a/b: ProductCounts gains Neighbors/Edges ("first 100 of n"); a conflict card is summary-only when its decisions aren't in the bands. Do NOT bind U7 to the subgraph POST (the owner is weighing HTTP QUERY).
- Context #365 (S-CL code lens): OK; context adds goldens capped.json and deadline-152.json and will send the sha. The U9d reuse list is in the -18b digest.
- Engine P7 (children): after it merges (P8 then P7), one ui PR. Needs-you rows open `NeedsYouRow.Child` (the address, only on via=child); `children[i].state` "unreachable" is its own case, any other value is plain text; plus #345's live re-proof. Goldens needs-you/park-mirrored-child and a status with an unreachable child come in the P7 PR.
- U9 next stages after #342 merges: U9a, U9c, U9d per u9-plan §4 (builders merge main first).

## Owner items open
- Delete proof/sindri-ui-u5p/raw/bin-host and raw/bin-ui (rm -r is hook-blocked; asked the user).
- #351 a11y-r2-3 waits on the owner's VoiceOver pass.

## Gotchas this session
- A setsid wrapper's PID is not the app's: find the app by its cwd before killing it.
- The heavy queue is slow (10–60 min waits); scratchpad gate.sh is shared, so builders use prefixed script names.
- `--filter-method` repeats AND (they match nothing); use a trait or one filter.
- Disk floor 30 GB: `dotnet clean` idle worktrees (~4 GB each).

## Safe point at the recycle (verified)
No ui builder, review or background job is running. Every ui worktree is clean and pushed:
| worktree | branch | head |
|---|---|---|
| sindri-ui-u9a0 | feat/sindri-ui-u9a0 (#342) | 75eb9371b |
| sindri-ui-answer-child | feat/sindri-ui-answer-child (#345) | 01a8e4e08 |
| sindri-ui-u5p | feat/sindri-ui-u5p (#351) | b8d8ef928 |
| sindri-ui-motion-poll | fix/sindri-ui-motion-poll (#356) | 607c31041 |
| sindri-ui-u11a | feat/sindri-ui-u11a | 5e026cd3c |
| sindri-ui-u11a-sheet | feat/sindri-ui-u11a-sheet | 3a1c707b1 |
| sindri-ui-u11a-fixture | feat/sindri-ui-u11a-fixture | 0591deafa |
Leftover: /tmp/aw-sindri-u9a0rc-146715 (29 MB re-check home; rm -r is hook-blocked).
The builder rules file is in this session's scratchpad, which the next session may not see. Its content is §4 of the -18 digest plus: ignore the context-guard numbers; unique scratch script names; push after every commit; trailer Co-Authored-By: Claude Sonnet 5.5 plus the current Claude-Session line.

## Update 10:55 (session after recycle)
- main cd42890a0: #342 and #345 MERGED. Lane map v14 (same names). #351 gates next.
- Builder A (U11a) resumed after a Sonnet rate limit; digest digests/sindri-ui-u11a-page.md.
- #356 builder launched: merge main, OnOpened anchor helper, AnchorBox assert, cor-6, gate, body, ready. Digest digests/sindri-ui-356.md. Lead sends `ready #356 <sha>`.
- #345 live re-proof + P7 follow-up wait on engine's P7 PR number.
## Update ~11:20: OWNER DEMO TOMORROW (meta asked)
- Demo: owner's Claude Code → Sindri MCP → scope → run; the app shows it read-only (Workflows, frame, Home).
- Running: #356 builder (digest sindri-ui-356.md); #351 merge-main builder (dropped from train 22 on a conflict; re-send `ready #351 <sha>` after); demo-check prover (worktree aw-wt/sindri-ui-demo-check, Xvfb :93, evidence proof/sindri-ui-demo-check/evidence.md); U11a builder A.
- Told the meta: U8 shows scoping read-only, facts as payload trees (U13 not built), no workflow.line on main, #351 not on the demo path, #356's crash only when cwd is deleted. Send the prover's result to the meta tonight.
## Update: meta says NO SLIMMING for the demo
Priorities: #356 → #351 → P7 follow-up (when engine sends the PR #) → U11a demoable → styled scope-map fact viewer (schema schemas/scoping/scope-map.json, golden tests/Aw.Sindri.Tests/Golden/Api/workflow-instance-fact/scope-map.json; register in FactViewerRegistry) in the next free slot. Tonight: send the meta a "demo tour" list (pages, what each shows, anything broken).
- Context lane offered help for a demoable Context page. Told them: U9a not started (L); queue = scope-map viewer, then a demo-sized U9a (head, anchor box, three bands with edges, coverage; plan u9-plan.md §3 U9a tasks). Their builder makes a seeded demo home (port 7481, ~/.agentic-workflow/rehearsal/demo-ctx-*) and sends the path plus anchors.
- Scope-map viewer builder launched (brief digests/sindri-ui-scopemap-brief.md, worktree aw-wt/sindri-ui-scopemap, branch feat/sindri-ui-scopemap-viewer). #351 at 77242c46f (has e035daf); its gate is queued as m351-gate.sh. The lane's heavy slot (1 per session) is the bottleneck.
- DEMO 2026-10-19 09:00 PT. Ready lines by 06:00 PT; the meta stops merging at 08:00 PT. Main edb2681 (#364 CO-7a, no Ui files: no re-gate needed). #351 at 0bf8a9149 (AW1005 300-line fix), re-gate m351b-gate.log running.
- Context demo home READY: state ~/.agentic-workflow/rehearsal/demo-ctx-20261018-110150 (AW_SINDRI_HOME=$AW_STATE_DIR/sindri-app, port 7481, map v4, 18 nodes/22 edges). Start: `source <dir>/env.sh && <dir>/bin/sindri --headless` (agent shell: AW_SINDRI_PROVER=1 for the start only); stop: `sindri stop`. Anchor surface:command/context-sources-status; set + surface:api/mcp-context-sources. CLI: `sindri context lenses <a> [<b>] --json` (positional), `sindri context map status --json`. Recorded: proof/demo-ctx/evidence.md.
- U9a-demo builder launched (worktree aw-wt/sindri-ui-u9a, branch feat/sindri-ui-u9a, digest digests/sindri-ui-u9a.md).
- MEMORY LOW (meta): no new heavy jobs or app runs until the meta sends MEMORY OK. Told U9a, scope-map and U11a to hold; #356 and #351 gates (already queued) proceed. When MEMORY OK arrives, release them.
- Engine has aw-wt/engine-line with recipes/sindri/workflow.line.yaml (in progress).
- Live check DONE (proof/sindri-ui-demo-check/evidence.md): UI faithful; blockers went to other lanes (scoping approve SND-WF-016 → context PR-B/S-LB2; steps descriptor-only → demo is desk-driven; child pin and the glyph pending → engine/api).
- Demo polish builder launched (aw-wt/sindri-ui-demo-polish, fix/sindri-ui-demo-polish, digest digests/sindri-ui-demo-polish.md): Workflows columns, the clipped cli, nested scroll, payload double, and the subject/done checks. It builds at MEMORY OK.
- #351: meta wants `ready #351 abf662634` when m351c gate is green (it gates after #367). Train 24's #351 result is stale.
- Lane agents waiting on MEMORY OK: U9a (1e9c711e3, unbuilt), scope-map, polish; U11a told to hold new heavy jobs.
- Polish at 98084624e (unbuilt): fixes 1, 2 and 4 written; 3 (nested scroll) is not in the source, so assertions only; 5 (subject label = key) and 6 (counts.done 1) are API asks. Resume it with MEMORY OK.
- MEMORY OK (meta): released polish, U9a, scope-map and U11a. Disk 53 GB: dotnet clean idle worktrees before any new builder (candidates: sindri-ui-u11a-sheet, -u11a-fixture, -answer-child, -u9a0 once merged and swept).
