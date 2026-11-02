---
max_turns: 6
timeout_seconds: 120
allowed_tools: [Read, Glob, Grep, Skill, AskUserQuestion, "Bash(ls *)", "Bash(git *)"]
tags: [verify-app, routing]
---

Can you smoke-test the plant watering app I just changed? The reminder screen is the part to check. Give me a verdict I can paste in the PR.
