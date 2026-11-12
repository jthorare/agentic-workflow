---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, Bash, Write]
tags: [bugFixOrchestrator, behaviour, data-repair]
---

Use the bugFixOrchestrator skill on this ticket: "ACME-9: invoices created last week show a total 100 times too small."

Scenario note: the code bug is fixed already. The candidate in `.aw-eval/candidate.diff` repairs the damaged rows with a raw SQL file, `repairs/fix_totals.sql`. Do not dispatch subagents or run the helper. Decide, as the skill's data repair step says, whether that candidate may go on, and say why. Read the repo's config the way the skill says.
