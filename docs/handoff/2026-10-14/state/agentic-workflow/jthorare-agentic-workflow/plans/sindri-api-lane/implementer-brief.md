# Sindri API lane — common brief for every implementer unit

You implement one unit of an API phase in the worktree the lead names (`.claude/worktrees/sindri-api-p<N>`). Work
only there. Never switch branches in `/Users/thor/repos/agentic-workflow`, since the live toolkit links to it. Never
push, open a PR, rebase or `git clean`; the lead does all of those.

**The plan is the contract:** `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-api-lane/p<N>-plan.md`. Your
progress lives in the handoff `~/.agentic-workflow/digests/sindri-api-p<N>.md`. Read both first, then the spec sections
the plan cites:
- `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md`.
- The UI spec `docs/superpowers/specs/2026-10-11-sindri-ui-design.md` (§6.7 E-1…E-19, §18). It wins where the two
  differ (README X52).
- README X61: multiplayer later. Keep `actor` and `owner` open strings, never parsed.

Read `.agents/rules/csharp.md` and `.agents/rules/csharp-testing.md` before editing C#. Follow the neighbours of
whatever you touch.

**Toolchain:** `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"`.

**Heavy work (rules since the 2026-10-14 crash):** every build and test run goes through
`~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-api-lane/tools/heavy.sh <cmd…>`, which forwards to the
shared `~/.agentic-workflow/bin-r1b/heavy`. This session and ALL its subagents share ONE heavy slot, and whole-solution
builds/tests also take one of 2 box-wide slots. The wrapper waits for its slot by itself; exit 75 means its wait
timed out, so run it again. Never run dotnet build/test outside it, and never delete lock directories.
- Since the second tightening (2026-10-14), EVERY build takes one of the 2 box-wide slots: dotnet build, publish or
  format, `dotnet test` without `--no-build`, and npm ci or test. Iterate in two steps:
  `heavy.sh dotnet build tests/Aw.Sindri.Tests -warnaserror -v q -clp:ErrorsOnly`, then
  `heavy.sh dotnet test --project tests/Aw.Sindri.Tests --no-build --filter-…`. Never run `dotnet test` without
  `--no-build`. Build as rarely as you can, because each build waits for a box slot.
- Run the whole gate (`heavy.sh <tools>/gate.sh <worktree> <logprefix>`, then `tools/gate-summary.sh <logprefix>`) once
  before each report, not after every edit.
- Background tasks are killed after about 10 minutes: run long waits with `nohup … &` and poll the log.

**Gate before every report.** Other lanes build these branches.
- The WHOLE solution: `dotnet build AgenticWorkflow.slnx -warnaserror` with 0 warnings, and
  `dotnet format AgenticWorkflow.slnx --verify-no-changes` clean.
- `dotnet test --project tests/Aw.Sindri.Tests --filter-not-trait "Lane=heavy"`.
- `dotnet test --project tests/Aw.Architecture.Tests`.

**TDD:**
- Make one red commit per behaviour: it compiles with 0 warnings and fails when run. Then make the fix commit.
- Commit small and often. A usage limit can stop you at any moment, so never hold on to uncommitted work.

**Commit messages:** `type: description`, then a blank line, then:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01XsAiYMxeZLPQx8L78xzsfP
```

**Context limit:** stop at about 150k tokens of context. Stop at a clean commit, update the handoff with what's done, the
commits and notes for the next unit, and report.

**Constraints:**
- Proofs use `scripts/sindri-prove.sh` with a temp home only. Never touch the real `$AW_SINDRI_HOME`, `setup.sh`, the
  providers or MCP registration.
- Never edit any spec. A needed spec change is a question for the lead.
- Threat model: the gates guard against honest mistakes, not hostile agents.
- Don't publish anything (no AOT) unless the unit says to.
