---
max_turns: 40
timeout_seconds: 600
allowed_tools: [Read, Write, Edit, Glob, Grep, Bash, Skill, AskUserQuestion, "mcp__plugin_playwright_playwright__*"]
tags: [verify-web, behaviour]
---

Use the verify-web skill on the recipe site in this directory. Criteria: on http://localhost:3000/recipes, open "Add recipe", fill in a title and two ingredients, save it, and check the new recipe shows up in the list. Run with --yes.

Environment note: shell commands here are read-only, so write the pack files (pack.json, report.md) with the Write tool, under the run directory the skill's own steps print. Do not edit any repo file.
