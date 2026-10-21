> **Newer state: read `~/.agentic-workflow/digests/sindri-ui-2026-10-18d.md` first.**

Session sindri-ui; meta sindri-meta. Lane map v13: api=sindri-api, engine=sindri-engine, context=sindri-context, ui=sindri-ui, ideas=sindri-ideas. Main ae3fd0d.
Rules and background: digests/sindri-ui-2026-10-18.md §4 and orchestrator/handoff/ui.md (its end has the follow-up list). Builder rules file (every brief points at it): /tmp/claude-1000/-home-thor-repos-agentic-workflow/b7a92a30-4136-4f13-bcd9-c06bb00c150a/scratchpad/builder-rules.md. If that scratchpad is gone, rebuild it from §4 of the -18 digest.
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
- Builder A is RUNNING: T1/T2 in WIP commit 5e026cd3c (pushed, builds clean; the Agents/Page tests were not yet run green, since the first run hung on a fake-clock await, now fixed and the re-run queued). Digest: digests/sindri-ui-u11a-page.md. Then: merge B and C, merge main, gate, live proof on Xvfb :94, then a draft PR. A has B's and C's merge notes:
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
