---
max_turns: 25
timeout_seconds: 400
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion]
tags: [ui-evidence, trigger]
---

The settings page change in PR 57 of acme/shop needs UI evidence for review. The local stack is already up on localhost:3000 with seeded data. Gather the evidence and get it ready for the PR.

Environment note: shell commands here are read-only, so write any files (such as the evidence script) with the Write tool, inside the current directory, and pass those paths to the tools. Use `./ui-runs/pr57` as the run directory.
