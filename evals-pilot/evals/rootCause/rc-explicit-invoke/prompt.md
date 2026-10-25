---
max_turns: 30
timeout_seconds: 420
allowed_tools: [Read, Glob, Grep, Skill, Agent]
tags: [rootCause, explicit]
---

Use the rootCause skill on this: test_ten_items_get_ten_percent_off in the cart tests reports 2000 != 1800. Find the root cause and fix it.

When you are done, run `python3 -m unittest 2>&1` one last time and save its full output to RESULT.txt in the repo root using the Write tool (shell redirection into the repo is blocked in this sandbox).
