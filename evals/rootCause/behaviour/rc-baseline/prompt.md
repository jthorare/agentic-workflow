---
max_turns: 30
timeout_seconds: 420
allowed_tools: [Read, Glob, Grep, Skill, Agent, Bash, Write]
tags: [rootCause, behaviour, classification, needs-2909]
---

Use the rootCause skill with --investigate-only on this: on the modern storefront, test_ten_items_get_ten_percent_off reports 2000 != 1800, so a cart of exactly 10 items gets no bulk discount. Find the root cause and tell me whether the other storefront does the same. Do not edit the code.
