---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, TodoWrite, AskUserQuestion, Bash, Write, Edit]
tags: [addressReview, behaviour, forced]
---

Use the addressReview skill on PR 77 of acme-kitchen/pantry-api, the shopping list CSV export. The saved review for it is already in this workspace's state dir. Fix the outstanding findings in the code, update the saved review file, and tell me what was fixed and what is left.

Environment note: git is read-only here, so do not commit, push or check out anything; list the git and gh commands you would have run under a `Would run:` heading. There is no subagent tool in this environment, so make the code edits yourself with the Edit tool, in the current directory.
