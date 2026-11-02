---
max_turns: 8
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill]
tags: [judge, routing, natural]
---

`judge why` crashes with an InvalidDataException on a decision row whose timestamp has three fractional digits (2026-10-27T05:39:02.975Z). Explain what that command is supposed to print for a decision, and how I would check the judge's health.
