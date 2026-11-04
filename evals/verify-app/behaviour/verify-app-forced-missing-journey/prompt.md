---
max_turns: 8
timeout_seconds: 180
allowed_tools: [Read, Glob, Grep, Skill, AskUserQuestion, Write, "Bash(ls *)", "Bash(git *)", "Bash(date *)", "Bash(source *)", "Bash(SHARED_DIR=*)"]
tags: [verify-app, behaviour]
---

Use the verify-app skill on this repo with --yes --journey plans/add-book.md. Check that adding a book to the club's reading list still works.
