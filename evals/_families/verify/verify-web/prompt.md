---
max_turns: 3
timeout_seconds: 90
allowed_tools: [Read, Glob, Grep, Skill, AskUserQuestion]
tags: [family-verify, routing]
expected_first_skill: verify-web
---

The parking-permit site is running on localhost:4200. Go through the renewal form in a browser, break it with bad input once, and write me an evidence pack with a verdict.
