---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
tags: [verify-desktop, behaviour, forced]
---

Use the verify-desktop skill on the budget planner desktop app in this repo. The last few commits changed the Settings page. Check the affected screens against their mockups and write the evidence pack.

Environment note: shell writes may be blocked here, so write pack.json and report.md with the Write tool (it creates folders). The `ui-evidence` tool is on PATH; no workflow engine is installed.
