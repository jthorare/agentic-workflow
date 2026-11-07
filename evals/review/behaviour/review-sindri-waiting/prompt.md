---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
tags: [review, behaviour, sindri]
---

Use the review skill on PR 99. This is a Sindri Step: if you cannot go on without an answer, put it in the result and end the turn.

Environment note: `AW_SINDRI_PR_REF` is `refs/sindri/pr/99`, but this repo has no such ref and no other ref for a PR 99, and there is no GitHub network. You cannot tell which branch PR 99 is, so ask which one to review.
