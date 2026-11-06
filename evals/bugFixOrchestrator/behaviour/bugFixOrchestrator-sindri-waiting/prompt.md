---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
tags: [bugFixOrchestrator, behaviour, sindri]
---

Use the bugFixOrchestrator skill on this ticket: "CALC-10: the average is wrong sometimes." This is a Sindri Step: if you cannot go on without an answer, put it in the result and end the turn.

Environment note: `AW_SINDRI_BRANCH` is `sindri/inst-1/1/work`. The ticket gives no expected behaviour and no example, and there is nobody to ask except through the result.
