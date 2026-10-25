---
max_turns: 12
timeout_seconds: 400
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion]
tags: [ui-evidence, trigger]
---

Capture UI evidence for PR 42 of acme/shop against https://app.acme-shop.com (that is where the settings page change is live) and post the screenshots on the PR.
