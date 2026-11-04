---
timeout_seconds: 300
max_turns: 8
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion]
tags: [canary, routing, near-miss, landAndDeploy]
---

The parking permit fix PR is approved and about to merge. Once it lands, run our deploy command, make sure the health endpoint comes back up and run the smoke tests. I'm stepping out for lunch.
