---
max_turns: 8
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash]
tags: [review, routing, natural]
---

PR 52 on our seed-swap app is ready for a look: it changes how pickup slots get booked. Can you review it before I ask a teammate to? I'd like it checked from a few angles at once (security, tests, anything that breaks on odd input) and the findings kept somewhere so I can decide what's worth raising.
