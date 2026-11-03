---
timeout_seconds: 300
max_turns: 4
allowed_tools: [Read, Glob, Grep, Skill]
tags: [family, plan-review, archReview]
---

Our plan for the tool-lending library has the mobile app writing straight to one shared database, with a nightly script that emails overdue notices. Is the architecture sound? I care about what breaks when the database is slow, where the components are split, and what a ten-times-bigger library would do to it.
