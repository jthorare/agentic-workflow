# Sindri app — lane brief (for worker agents)

You are a worker in the Sindri app agent team. The main session is the orchestrator. Read these, in this order:
1. Stage file (the lanes, the team rules, the contracts): `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-app/stages.md`
   — especially "Contracts" and "Parallel execution".
2. Original handoff (process rules, tool quirks): `~/.agentic-workflow/digests/sindri-app-shell-handoff.md`
3. S1's C# contract, which is frozen for you: `docs/csharp/sindri.md` on branch `feat/sindri-s1-app-shell` (draft PR #163). S1's
   code is in `src/Sindri` on that branch.
4. Specs: `docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` (§17.28 engine, §17.29 app, §17.17 Q6/Q7,
   §17.20–17.22 seams, §17.27.1 command layer), `docs/superpowers/specs/2026-10-09-agent-readiness-design.md` §4a (UI) and
   §4b; `.agents/rules/csharp.md` and `csharp-testing.md` (read them in full).

## How you work
- Run `/specToProvenPR` (invoke the skill) for **your stage only**, end to end: stage plan → verification plan (written
  before any code, saved under `~/.agentic-workflow/jthorare-agentic-workflow/proof/<your-stage-slug>/`) → TDD
  implementation → hand test of the real binary or app → evidence → draft PR → `/review` loop → re-check → `gh pr ready`.
  **Never merge.**
- Autonomy contract: run continuously to ready. Don't ask the owner anything. If you hit a decision only the owner can
  make, stop and put the question, with your recommendation, in your final report to the orchestrator.
- Review loop: **at most 5 `/review` runs** in total (owner raised it from 3 on 2026-10-11). Fix `issue`s and any real-harm finding (data loss, a broken path, a
  false doc claim). Resolve suggestions and nits in writing. Stop on the first run with 0 blocking and 0 issue. Exploit-
  style findings are answered against the threat model (honest mistakes, `docs/csharp/bugfix.md`), not looped on.
- **Scratch files:** the scratchpad directory is shared by every lane. Keep ALL your scratch files, scripts, logs and
  hand-test homes in your own subdirectory (`scratchpad/<stage>-lane<X>/`), never at the scratchpad's top level and
  never in another lane's folder (files collided on 2026-10-11).
- Your own worktree under `~/repos/agentic-workflow/.claude/worktrees/sindri-<stage>`, and your own branch
  (named in your task). Never touch another lane's worktree, or the main checkout.
- Builds and tests only through `~/.agentic-workflow/bin-r1b/heavy <cmd>` (2 slots for the whole box; it waits for a free
  one). Scaffold with `/new-package` and `/new-slice` under heavy too (`node scripts/scaffold/...`).
- Tool quirks: a hook blocks any command containing the substring "main" (also inside "remaining", "domain",
  "maintain"), so put commit messages and PR bodies in files (`git commit -F`, `gh pr create --body-file`). Another hook
  blocks recursive or forced removal: use `find … -delete` or `unlink`, and write files that mention those commands with
  the file tool, not a shell heredoc.
- Commits: `type: description`, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. PR bodies end
  with `🤖 Generated with [Claude Code](https://claude.com/claude-code)` and follow
  `~/.agentic-workflow/toolkit/skills/_shared/pr-body.md`.
- Shared one-line files (`AgenticWorkflow.slnx`, `tests/Aw.Architecture.Tests/Packages.cs` and its csproj, `deps.yaml`,
  `AGENTS.md`, `Directory.Packages.props`, lock files): edit them only in your branch. Before each push, check against
  your base with `git merge-tree --write-tree`, and resolve any conflict with a merge commit.
- GREEN = the full local merge gate in AGENTS.md (Actions are billing-blocked). Lane-budget and statusline flakes under
  load: re-run alone and record them. Watch the architecture tests (ARCH-CYCLE-001: keep each project's root namespace a
  leaf; S1 put its `*ServiceCollectionExtensions` in a `Registration` namespace for that reason).
- Write your stage's log section in the stage file (clock: verification plan → ready; C# lines src/tests).
- Final report to the orchestrator (your last message): PR number and URL, gate results, review runs and their outcome,
  owner questions if any, and anything another lane must know (a contract you needed changed and didn't change).
