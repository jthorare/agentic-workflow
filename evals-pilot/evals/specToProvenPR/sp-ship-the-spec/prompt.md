---
max_turns: 40
timeout_seconds: 600
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite]
tags: [specToProvenPR, trigger]
---

planning/specs/greeter/DESIGN.md is approved. Take it from spec to pull requests, and prove it works before anything gets opened. There is no GitHub access from this machine, so work up to the point where you would open the first PR and tell me where you stopped and why.

Environment note: shell commands here are read-only, so create and change files with the Write and Edit tools directly in the current repo (git branches, worktrees and commits are not possible in this environment).
