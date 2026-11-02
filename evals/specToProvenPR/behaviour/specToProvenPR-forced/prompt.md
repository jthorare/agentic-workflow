---
max_turns: 30
timeout_seconds: 600
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write, Edit]
tags: [specToProvenPR, behaviour, forced]
---

Use the specToProvenPR skill on planning/specs/toolbox/DESIGN.md, it's approved. There's no GitHub access from this machine, so go as far as writing the stage map, up to the point where you'd plan stage 1, then stop. Give me the stages as they go in the stage map with the files each one touches, tell me anything in the repo that doesn't line up with the spec, and say where you stopped and why.

Environment note: git is read-only here (no branches, worktrees, commits or pushes), so list what you would have run under "Would run:". Write files with the Write tool inside the current directory.
