---
max_turns: 8
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
tags: [ui-evidence, routing, natural]
---

Can you get the UI evidence for PR 31 of acme/bakery (the new order summary on the checkout page)? My local stack is up on localhost:3000. Last time an evidence run sat waiting on a lock for minutes and then timed out, so tell me straight away if it does that again.
