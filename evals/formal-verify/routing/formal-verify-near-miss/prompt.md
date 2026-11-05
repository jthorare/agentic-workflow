---
max_turns: 8
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill, Bash]
tags: [formal-verify, routing, near-miss]
---

Review the architecture in plans/billing/plan.md: are the service boundaries right, and what breaks first when traffic grows ten times?
