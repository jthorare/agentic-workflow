---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, Bash, Write]
tags: [bugFixOrchestrator, behaviour, sweep]
---

Use the bugFixOrchestrator skill on this ticket: "SHOP-5: the orders screen shows a total as $12.5, it should be $12.50."

Scenario note: you are at the end of Phase 5. The only candidate (diff in `.aw-eval/candidate.diff`) passed the frozen check and the judge said `resolved` for the orders screen. Do not dispatch subagents or run the helper. Do the sweep from `skills/_shared/prevent-the-class.md` on this repo and write `resolution.md` in the repo root with the "Where else we checked" table. There is no `repo.json` and no data access.
