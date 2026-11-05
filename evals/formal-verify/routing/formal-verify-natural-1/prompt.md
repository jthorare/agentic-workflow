---
max_turns: 8
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill, Bash, Write]
tags: [formal-verify, routing, natural]
---

Before we ship the new leader lease code, I want proof that two nodes can never both believe they are the leader at the same time. An actual model check of every interleaving, not a code read.
