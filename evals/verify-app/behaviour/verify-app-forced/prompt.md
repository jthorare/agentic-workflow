---
max_turns: 6
timeout_seconds: 180
allowed_tools: [Read, Glob, Grep, Skill, AskUserQuestion, "Bash(ls *)", "Bash(git *)", "Bash(source *)", "Bash(SHARED_DIR=*)"]
tags: [verify-app, behaviour]
---

Use the verify-app skill on this repo, with --yes. Check that adding a book to the club's reading list still works.
