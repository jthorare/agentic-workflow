---
max_turns: 30
timeout_seconds: 420
allowed_tools: [Read, Glob, Grep, Skill, Agent, Bash, Write]
tags: [rootCause, behaviour, classification]
---

Use the rootCause skill with --investigate-only on this: test_ten_items_get_ten_percent_off in the cart tests reports 2000 != 1800. It passed a few days ago. Find the root cause and tell me whether this is a regression or by design. Do not edit the code.
