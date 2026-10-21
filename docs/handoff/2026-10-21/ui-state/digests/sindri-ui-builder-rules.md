# Builder rules (sindri-ui lane) — read fully before any command

- Ignore the context-guard hook's token numbers: they show the LEAD session's context, not yours.
- Work ONLY in your own worktree (given in your brief). Never touch ~/repos/agentic-workflow (the shared checkout): no build, test, git or edit there. Always `cd <worktree> && ...` or `git -C <worktree> ...`. Run only scripts from your own worktree.
- Every dotnet build/test goes through the heavy wrapper, bounded from outside:
  `timeout 3600 ~/.agentic-workflow/bin-r1b/heavy dotnet ...` (queue wait counts toward the bound; 1 heavy job at a time; with 4 builders sharing one slot, the wait can pass 15 min, so never use 900).
  Bound waits inside tests with CancelAfter; never write an unbounded wait.
- Never override HOME for dotnet/heavy. Never `rm -r` (hooks block it). Never git stash. Never rebase: merge origin/main.
- Kill processes by exact PID only; never pkill/pgrep patterns. tmux: `unset TMUX` and `tmux -L <unique>`; never kill-server.
- Push via `git -C <worktree> push origin <branch>` after EVERY commit. A command must not contain both "push" and "main".
- Commits: `type: short description`, atomic; trailer:
  Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>   (no Claude-Session line any more)
- Main is strict: CA1508, CA2000, CA1802, RS0030 (Guid.NewGuid, DateTime.UtcNow, Thread.Sleep in tests; use RealTime) are errors. Fix where flagged; never suppress; never grow the .editorconfig ratchet.
- Avalonia: never read Dispatcher.UIThread from a pool thread; never touch Avalonia outside Dispatch in tests. TextBlock peers ignore AutomationProperties.Name (use the Border wrapper pattern). Test a11y with a peer-tree walk (Heard.Tree / GetChildren).
- Lane gate (owner 2026-10-18: every merge goes through the meta's batch, no single-PR gates): `dotnet build -warnaserror` of the touched projects, then the Aw.Sindri.Ui.Tests unit lane (ALWAYS, meta 2026-10-19), the touched Ui.Tests integration classes once, Aw.Sindri.Ui.Rendering.Tests when views change, and Aw.Architecture.Tests, `dotnet format --verify-no-changes --no-restore` on touched files. No integration lane and no full gate; the batch gate runs those. Say in the report exactly which tests ran.
- Format: ONLY `dotnet format <the touched project .csproj> --verify-no-changes --no-restore --include <touched files>`. NEVER a solution-wide format (AgenticWorkflow.slnx): it takes 3+ GB of memory, and the batch gate runs the full format (meta 2026-10-20).
- INTEGRATION LANE (meta 2026-10-20, after batch #461 went red on 11 Ui.Tests integration failures): every ui gate ALSO runs the full Aw.Sindri.Ui.Tests integration lane: `dotnet test --project tests/Aw.Sindri.Ui.Tests --filter-trait "Lane=integration"` (not just the touched classes). Unit lane alone is NOT enough.
- Test first (TDD): write the red test, see it fail, then fix.
- No work-app names/URLs/stacks anywhere.
- Keep a digest current at the path in your brief (state, commits, gate numbers, what's left) so a fresh session can resume. Update it before you finish.
- Final report to the lead: head sha (pushed), gate numbers, what's done, what's left, anything that needs a decision. Short.

- PR bodies end with exactly:
  🤖 Generated with [Claude Code](https://claude.com/claude-code)
- RULE (meta, after #527 at main 6823eaa: CI runs dotnet format --verify-no-changes): run scoped format on the touched files before EVERY push.
- After sending ready, run dotnet clean in the worktree (meta 2026-10-20; frees ~3.6 GB each). Lead does it, or the builder after its final gate.

## MSBuild node reuse OFF (meta, box-wide, 2026-10-20)
Every dotnet build/test/format you run: `export MSBUILDDISABLENODEREUSE=1` (or pass -nodeReuse:false). Reused MSBuild workers ate ~15 GB and starved the heavy slots.
