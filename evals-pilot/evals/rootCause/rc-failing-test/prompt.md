---
max_turns: 30
timeout_seconds: 420
allowed_tools: [Read, Glob, Grep, Skill, Agent]
tags: [rootCause, trigger]
---

The cart tests are failing: test_ten_items_get_ten_percent_off reports 2000 != 1800. Figure out why and fix it.

When you are done, run `python3 -m unittest 2>&1` one last time and save its full output to RESULT.txt in the repo root using the Write tool (shell redirection into the repo is blocked in this sandbox).
