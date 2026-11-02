---
max_turns: 6
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion]
tags: [specToProvenPR, routing, natural]
---

The spec for the cookbook overview page in our recipe app is signed off (planning/specs/cookbook-overview/DESIGN.md). It has four stages: cards, state labels, links out, and hand-added cookbooks. Take it to PRs, one per stage, and make sure each stage really works in the running app before you open its PR.
