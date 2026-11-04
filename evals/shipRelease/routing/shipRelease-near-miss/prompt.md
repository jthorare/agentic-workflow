---
max_turns: 6
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
disallowed_tools: [SendMessage, ListAgents, Monitor]
tags: [shipRelease, routing, near-miss, landAndDeploy]
---

PR #58 on the seed-catalog site is approved. Wait for it to merge, then run the deploy and check the health endpoint once it's live.
