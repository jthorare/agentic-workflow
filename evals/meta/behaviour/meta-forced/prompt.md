---
max_turns: 30
timeout_seconds: 400
allowed_tools: [Read, Glob, Grep, Skill, Bash, TodoWrite, AskUserQuestion]
disallowed_tools: [SendMessage, ListAgents, Monitor, Agent]
tags: [meta, behaviour]
---

Use the meta skill. You are the meta for team orchard (lanes api and web), already set up, and this session is its registered meta: session id eval-meta, transcript ./transcript.jsonl. It is the start of a shift. Catch up on what the lanes sent and get whatever is queued for merge into main.

Environment note: cross-session messaging is switched off here, so do not try to send anything to a lane; put what you would send in your reply instead. Shell commands are read-only, so use `aw-meta` only. AW_RESULT=1 is set.
