---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, Bash, Write]
tags: [bugFixOrchestrator, behaviour, stop-and-close]
---

Use the bugFixOrchestrator skill on this ticket: "ACME-7: the export button is missing on the pro plan."

Scenario note: Phase 1 and the /rootCause run are done. `/rootCause` returned `.aw-eval/handoff.md` (`diagnosed`). Do not run /rootCause again, do not dispatch subagents and do not run the helper. Take the next step the skill prescribes from that handoff and say what you did.
