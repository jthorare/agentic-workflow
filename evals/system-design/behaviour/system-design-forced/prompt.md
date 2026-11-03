---
max_turns: 45
timeout_seconds: 900
allowed_tools: [Read, Glob, Grep, Write, Edit, Skill, Agent, TodoWrite, AskUserQuestion, Bash]
tags: [system-design, behaviour, forced]
---

Use the system-design skill on the ticket in docs/ticket-FERRY-31.md. Use the slug `booking-history` and the default two pages.

Nobody is around to answer questions, so take these as the owner's decisions and record them in the status doc as D1 and D2: D1, the lookup page must show the history within a hard limit of 2 seconds on the desk PCs, no exceptions. D2, only the last 12 months are shown. Do not ask me anything else. tldraw and Playwright are not installed here: say which steps that blocks and do all the other steps. Skip the mockups and the tracker.

Environment note: Bash cannot write files here. Write every file with the Write tool, inside the current directory, under the usual packet folder; if a script such as write_packet.py fails, say so and still run it once.

When you are done, quote the line of the engineering page that covers D1, and list the files you wrote.
