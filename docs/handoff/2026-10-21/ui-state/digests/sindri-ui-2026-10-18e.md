# Sindri UI lane digest, 2026-10-18 e (read FIRST; supersedes -18d, whose "Update" lines are history)

Session sindri-ui; meta sindri-meta (uds .../3383568.sock); engine 2728336.sock; context 2728370.sock. Lane map v15 (same names). Builder rules: ~/.agentic-workflow/digests/sindri-ui-builder-rules.md. Main edb2681 at last check.

## OWNER DEMO 2026-10-19 09:00 PT
Ready lines by 06:00 PT; the meta stops merging at 08:00 PT. Demo: owner's Claude Code → Sindri MCP → scope → line, with the app showing it read-only, plus Agents. It's desk-driven: steps are descriptor-only, which the meta accepted. Tonight, send the meta a "demo tour" list: pages, what each shows, and anything broken. The live-check evidence is the base: ~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-ui-demo-check/evidence.md.

## PRs / builders (each one Sonnet, background; heavy = ONE slot per lane session, so batch the gate as one script)
| unit | branch / worktree (aw-wt/) | head | state | next |
|---|---|---|---|---|
| #356 motion+focus | fix/sindri-ui-motion-poll | ede8bc33b | READY SENT | meta trains it |
| #351 Start form | feat/sindri-ui-u5p | bf15aadb8 | DROPPED from train 31 (conflicts with #369); m351d stopped | AFTER #369 merges: tell the #351 builder to merge main, run gate m351e, then re-send `ready #351 <sha>` |
| demo polish | fix/sindri-ui-demo-polish | a301f53fc | #372 READY SENT (review inline: 0 issues) | meta trains it; do not push |
| U9a demo lens stack | feat/sindri-ui-u9a | afe23e5b5 (gate 2 running) | gate + live proof :92 on the context demo home + PR | digest digests/sindri-ui-u9a.md |
| scope-map viewer | feat/sindri-ui-scopemap-viewer | ee6a535e1 | #381 READY SENT (review run 1: 1 issue fixed, full gate green) | meta trains it; do not push |
| U11a Agents | feat/sindri-ui-u11a | 83807d8f3 | READY SENT (review run 1 clean: 0 issues, 3 suggestions, 4 nits) | meta trains it; do not push |
| P7 follow-up | feat/sindri-ui-p7-children (aw-wt/sindri-ui-p7), stacked on engine draft #371 | ? | builder running; Xvfb :90 | PR base feat/sindri-wf-p7-children; retarget to main after #371; digest sindri-ui-p7.md |
Send a ready line for each PR once its gate is green and the PR is open (verify with gh pr view: head, isDraft, mergeable).

## Context demo home (don't reseed)
~/.agentic-workflow/rehearsal/demo-ctx-20261018-110150, port 7481. Start: `AW_SINDRI_PROVER=1 bash -c 'source <dir>/env.sh && <dir>/bin/sindri --headless'`; stop: `sindri stop`. Anchor surface:command/context-sources-status; second anchor surface:api/mcp-context-sources. Context will send a clean-box up/down script.

## Cross-lane blockers (routed by the meta)
- workflow.scoping as shipped fails approve with SND-WF-016 (input.context.lenses has no runtime) → context PR-B, then S-LB2.
- The child recipe isn't pinned (idea-to-brief), and visited states are served as glyph pending → engine (P7 / visited-states PR).
- subject.label = "fact:scopeInput", and counts.done 1 with nothing finished → engine (or api).
- Engine is building workflow.line in aw-wt/engine-line.

## Memory / disk
The meta sends MEMORY LOW / OK; on LOW, builders hold new heavy jobs. Disk ~53 GB: `dotnet clean` idle worktrees (u11a-sheet, u11a-fixture, answer-child; u9a0 once swept) before any NEW builder. Displays in use: :91 polish, :92 U9a, :94 U11a.

## Owner items open
Delete proof/sindri-ui-u5p/raw/bin-host and bin-ui (rm -r is hook-blocked). #351 a11y-r2-3 waits on the owner's VoiceOver pass.
- #356 is gating in train 26: DON'T move its head. Owner: usage is the binding limit, so only demo work and no extra review rounds.
- MEMORY OK again; all four released. OWNER (via meta): champion EVERY open PR in, including #369: after gate3 and gh pr ready, run ONE review pass (owner rule: fix issues only; suggestions and nits are resolved in writing), then send ready #369.
- STANDING RULE tonight (meta): before any NEW builder or heavy job, `free -m` available must be 8000 or more, else wait (re-check every 5 min). It replaces the MEMORY LOW/OK messages. All five builders told.
- main 1d1a562 (#356 MERGED). #351 is gating in train 27 (don't touch it). git merge-tree: u11a, u9a, polish (5b2cbe915) and scopemap (0921e5c65) all merge CLEAN with main, so no re-merge needed. Send ready #369 after its review is clean.
- Disk: freed about 5 GB (u11a build output; removed the u11a-sheet and -fixture worktrees). Meta: DISK OK at 49 GB. Clean each worktree's build output once its PR merges.
- main ce7836c (#371 P7 MERGED). merge-tree: u5p (bf15aadb8), u9a, polish, scopemap and p7 all CLEAN. P7 ui PR to base main. #369 is gating in the next train: don't touch it.
- main 62ad6cd (#367 merged). #369 is gating in train 30. All five branches CLEAN (u9a now 9d1ce5598, p7 e3b136d82). Gave polish the flaky PayloadFactViewerTests.The_not_running_message_is_said_a_turn_after_the_viewer_is_shown.
- Engine re-check (proof/demo-bug3-recheck/evidence.md): children work on ce7836c; approve idea-to-brief BEFORE scoping (else RCP-CHILD-001); the shipped scoping is still blocked by SND-WF-016 until context PR-B. Passed to the P7 builder. Demo tour must mention the approve order.
- main bea4be2 (#354 CO-1 merged). #369's only red is the failopen flake (meta re-checking). All five branches CLEAN (polish now a301f53fc).
- ideas: CO-1 and CO-7a merged. U11a deps still open: CO-7b (preview route; api builds it next; then ui's CO-7c live preview) and CO-4a (Focus send; parked on the owner's CO-S answers).
- U9a = PR #377 at 9d1ce5598 (gate green: 2356 pass, 10 skipped; live proof :92 matches the CLI for one anchor). Review run 1 agent running (reviews/377.json); after it, send ready #377. OWNER: the demo home on 7481 is still up (sindri stop refused to a session, SND-AUTH-002), plus 3 junk queue items there to close with --cancelled.
- #369's train red: Aw.Sindri.Tests HarnessVersionsContract Stderr_output_does_not_hide_the_version; #369 doesn't touch it (told the meta). The meta is rerunning it to classify.
- #377 READY SENT at 1d9aee357 (review run 1: 2 issues fixed, gate green). Demo tour note: click into the anchor box before typing.
- Demo tour DRAFT: digests/sindri-ui-demo-tour.md (keys checked against Labels.Palette.cs). Finalize it after the polish, scope-map and P7 PRs land, then send it to the meta tonight.
- DISK standing rule: df -h / must show more than 35 GB free before any new builder. Cleared the polish and u9a build output (30.0 → 44.1 GB). Clear each worktree once its builder finishes.
