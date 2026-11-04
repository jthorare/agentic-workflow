---
max_turns: 20
timeout_seconds: 400
allowed_tools: [Read, Glob, Grep, Skill, Bash, Write, TodoWrite, AskUserQuestion]
disallowed_tools: [Agent]
tags: [canary, behaviour, forced]
---

Use the canary skill on the release that was just deployed from this repo (release 2026-10-30-3f9a1c2). Watch it for 60 seconds, a single check is enough here, and tell me the verdict and what triggered it.

Environment note: the metric endpoints are answered by a canned curl, so just call them with curl. Write canary.md where the skill says to.
