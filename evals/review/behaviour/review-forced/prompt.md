---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
tags: [review, behaviour, forced]
---

Use the review skill on PR 14 of this repo (the loan desk, branch feat/due-soon-reminders). I want every problem in it found and saved locally; do not post anything to GitHub.

Environment note: gh answers from canned data, so use `gh pr view` and `gh pr diff` for the PR. Skip the rootCause follow-up (answer no to it). If a named specialist agent is not installed, use a general-purpose subagent with that persona.
