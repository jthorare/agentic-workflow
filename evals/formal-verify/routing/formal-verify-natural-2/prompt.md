---
max_turns: 8
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill, Bash, Write]
tags: [formal-verify, routing, natural]
---

write a tla+ spec for the order retry flow with its idempotency key and run tlc on it, I want to know if a retried payment can be charged twice
