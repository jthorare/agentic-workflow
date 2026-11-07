---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
tags: [review, behaviour, sindri]
---

Use the review skill on PR 14 of this repo (the loan desk). This is a Sindri Step: the result goes where the Result block's gate says, and nothing is posted.

Environment note: `AW_SINDRI_PR_REF` is `refs/sindri/pr/14` (the host fetched the PR there). There is no GitHub network in this Step, so make no `gh` call. If a named specialist agent is not installed, use a general-purpose subagent with that persona. Skip the rootCause follow-up.
