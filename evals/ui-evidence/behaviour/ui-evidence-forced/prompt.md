---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
tags: [ui-evidence, behaviour, forced]
---

Use the ui-evidence skill on PR 57 of acme/shop, which adds a notification preferences page (src/settings/NotificationPreferences.tsx). The local stack is up on localhost:3000. Capture the evidence for that page, say what it shows, and get it ready for the PR.

Environment note: write any files (such as the evidence script) with the Write tool, inside the current directory, and pass those paths to the tools. Use `./ui-runs/pr57` as the run directory. There is no qa-runner agent in this environment, so write the script yourself.
