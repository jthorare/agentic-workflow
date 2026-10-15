# PR #184 (Sindri UI U0): review run 4 fixes + run 7g + gate + PR body

Worktree: /Users/thor/repos/agentic-workflow/.claude/worktrees/sindri-ui-u0 (feat/sindri-ui-u0). Code done and pushed at 9a8893f3.
Findings: ~/.agentic-workflow/jthorare-agentic-workflow/reviews/184.json (run 4, 19 issues). Proof dir:
~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-ui-u0/ (evidence.md, manual-test.md, merge-gate.md).
Commit trailer now: Claude-Session: https://claude.ai/code/session_018r3EBCdAtCCempFmH8aqEU

## Resolutions (sha red / fix)
- F6 owner decision: f19cc0a2 / 61a792d1 (ShellWindow.RegionStep; spec §8 + Status; sindri-ui.md)
- architecture-strategist-0: 5115fb42 / 81b4d16d (EndpointRecordUnreadableException + EndpointRecordFailure; contract w/ real dir + chmod 000)
- accessibility-tester-0, -1: 97371949 / 4b0a465f (also pattern-recognition-specialist-3: switch on PropertyName)
- pattern-recognition-specialist-0 + julik-frontend-races-reviewer-1: 60ec563b / 8822ed70 (InstanceMessage.TryAct; fake withdraws; OneMessage gone)
- kieran-typescript-reviewer-0: e39c8c3d / 621ae8cb
- code-reviewer-0: e7ce3e1a (run.sh clears bin; verify in 7g by planting a stale file)
- accessibility-tester-2: 4b889892 / 64513c63
- code-reviewer-1 + pattern-recognition-specialist-4: 8b4c4946 / 9a8893f3
- Replies: scratchpad rr4/replies.md (julik-2, qa-1, pattern-1, pattern-2, kieran-1)
- Blocking qa-expert-0, security-sentinel-0, julik-0: run 7g.

## Left
run 7g (tools in scratchpad/u0-run7g/tools; ev supports `key 123|124 cmdopt`; env/guard recipe in scratchpad/u0-run7e/env.sh),
merge gate -> merge-gate.md, PR body (gh pr edit 184), mark 184.json addressed, push, report.

## Update (2026-10-13)
- Merges: a2dbf2e5 (main 6c7f4b2b; ARCH-REG-003 via b018a9c8's files) and 09e2a1ac (main 6c4f8720, #194). Pushed.
- Run 7g done at a2dbf2e5 (proof raw/run7g-a2dbf2e5, shots/run7g-a2dbf2e5/), all PASS; re-running at 09e2a1ac.
- Step c procedure: sindri-ui ignores a direct SIGSTOP when launched under a waiting zsh (zsh SIGCONTs a stopped
  foreground child); launch under scratchpad/u0-run7g/tools/waiter.pl and check `ps -o stat=` shows T.
- serve needs DOTNET_ROOT=$HOME/.dotnet and temporal copied to $H/state/cache/bin/temporal-1.9.1/ (env.sh usehome).
- Gate script: scratchpad/gate7g.sh <outdir>.
