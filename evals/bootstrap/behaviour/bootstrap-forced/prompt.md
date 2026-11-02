---
max_turns: 40
timeout_seconds: 600
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
tags: [bootstrap, behaviour, forced]
---

Use the bootstrap skill on this repo (recipe-api).

Keep it small: run the full documentation audit and show me the report, but generate only two planning docs, ARCHITECTURE and CODE_STYLE, plus AGENTS.md and .serena/project.yml. Skip .agents/rules, the design language and the sync step. There is no Docker here, so write .serena/project.yml but skip the Docker bootstrap and the Serena onboarding check. If anything asks me a question, answer no and carry on.

When you finish, print the full text of planning/TESTING.md so I can see it was left alone.
