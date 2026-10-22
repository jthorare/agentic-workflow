## Rules for every engine builder (DEMO MODE, 2026-10-22)
- Work ONLY in your worktree. Use `cd <worktree> && ...` or `git -C <worktree>`. Never touch /home/thor/repos/agentic-workflow.
- Ignore the context-guard hook's token counts (they're the lead's).
- DEMO MODE: you write code and tests and do light checks only. **Do not run `dotnet build`, `dotnet test`, smokes or a whole-solution
  format.** The lead batches heavy builds and test runs and sends you the errors. The one heavy command you may run is a format of
  only the .cs files you touched: `heavy dotnet format AgenticWorkflow.slnx --include <files...>`. Run it once, at the end.
- Read `.agents/rules/csharp.md` and `.agents/rules/csharp-testing.md` before writing C#. Follow the C# slice recipe in AGENTS.md
  (test, request and response, service, registration). Match the surrounding code's naming, comments and idiom.
- New Public API members go in the project's `PublicAPI.Unshipped.txt`. New types that cross JSON go in the package's JSON context. A new
  project or cli edge goes in `deps.yaml`.
- Commits: atomic, `type: description`. End each message with
  `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Never `--no-verify`. Don't push: the lead pushes after the build.
- Never open, edit or comment on PRs or other GitHub objects. Never `git worktree remove`.
- If you're refused for usage, stop at once and report it. If you hit a product question, don't guess: write it down and report it.
- Report: your branch head sha, the files you changed, what you couldn't verify without a build, and any open questions.
