---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
disallowed_tools: [SendMessage, Monitor]
tags: [shipRelease, behaviour, forced]
---

Use the shipRelease skill on this repo with --no-deploy. The feature branch is checked out and the base is main.

Environment note: git and gh writes are switched off here, so do not try to push or open a pull request for real. Put the commands you would have run under a `Would run:` heading in your reply, and say what the tests showed.
