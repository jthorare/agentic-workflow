---
max_turns: 25
timeout_seconds: 420
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, Bash, Edit, Write]
tags: [specToProvenPR, behaviour]
---

Use the specToProvenPR skill on planning/system-design/greeter-flags/doc.md. It is approved. Run continuously; don't stop to ask me anything. For now, only write the stage map, then stop before you plan the first stage and tell me what the map holds.

Environment note: shell commands here are read-only, so create and change files with the Write and Edit tools directly in the current repo (git branches, worktrees and commits are not possible in this environment).
