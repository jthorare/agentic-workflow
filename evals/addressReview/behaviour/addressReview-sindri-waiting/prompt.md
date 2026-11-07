---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, TodoWrite, AskUserQuestion, Bash, Write, Edit]
tags: [addressReview, behaviour, sindri]
---

Use the addressReview skill on PR 77 of acme-kitchen/pantry-api. This is a Sindri Step: if you cannot go on without an answer, put it in the result and end the turn.

Environment note: `AW_SINDRI_BRANCH` is `sindri/inst-1/1/work`. git is read-only here (list git commands under `Would run:`), and there is no subagent tool. The saved review's finding about parseInt asks the owner to choose between rounding quantities like 1.5 and rejecting them; that is a product call, so do not pick one: ask.
