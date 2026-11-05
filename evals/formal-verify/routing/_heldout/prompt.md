---
max_turns: 4
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill, Bash, Write]
tags: [formal-verify, routing, heldout]
---

Can a job in our worker pool get processed twice if a worker dies halfway through? Model it and check all the orderings.
