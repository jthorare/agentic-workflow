---
max_turns: 6
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
disallowed_tools: [SendMessage, ListAgents, Monitor]
tags: [shipRelease, routing, natural]
---

The tide-chart widget branch is finished. Rebase it on main, run the tests, and if they're green push it and open the PR. If the diff touches the UI, make sure there's evidence on the PR before it goes up.
