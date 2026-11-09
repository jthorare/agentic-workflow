---
timeout_seconds: 300
max_turns: 8
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
tags: [new-toolkit, routing, natural]
---

I want my Sindri agents to be able to diff two API contract files and keep a little cache of what they have already compared. Set me up a new toolkit for that, called Contracts, with a first tool named diff_contracts. It needs a cache database.
