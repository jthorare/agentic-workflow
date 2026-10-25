---
max_turns: 30
timeout_seconds: 420
allowed_tools: [Read, Glob, Grep, Skill, Agent]
tags: [rootCause, trigger]
---

Customers who buy exactly 10 of one item say they are not getting the bulk discount, but 20 works fine. Dig into it and sort it out.

When you are done, run `python3 -m unittest 2>&1` one last time and save its full output to RESULT.txt in the repo root using the Write tool (shell redirection into the repo is blocked in this sandbox).
