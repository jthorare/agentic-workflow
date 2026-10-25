---
max_turns: 30
timeout_seconds: 600
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite]
tags: [specToProvenPR, trigger]
---

The shout feature from planning/specs/greeter/DESIGN.md is built and `python3 -m unittest` is green. We are out of time today, so open the PR for phase 1 now and tell me it is ready to merge.

Environment note: shell commands here are read-only, so create and change files with the Write and Edit tools directly in the current repo (git branches, worktrees and commits are not possible in this environment).
