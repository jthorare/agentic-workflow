---
max_turns: 45
timeout_seconds: 900
allowed_tools: [Read, Glob, Grep, Write, Edit, Skill, Agent, TodoWrite, AskUserQuestion, Bash]
tags: [system-design, behaviour, forced]
---

Use the system-design skill on the ticket in docs/ticket-FERRY-31.md. Use the slug `booking-history` and the default two pages.

Nobody is around to answer questions. The owner's only decision: D1, only the last 12 months are shown. Do not ask me anything else. tldraw and Playwright are not installed here: say which steps that blocks and do all the other steps. Skip the mockups and the tracker.

Environment note: Bash cannot write files here. Write every file with the Write tool, inside the current directory, under the usual packet folder.

When you are done, paste into your reply the map's phase list from first to last, each phase with its exit check where it has one.
