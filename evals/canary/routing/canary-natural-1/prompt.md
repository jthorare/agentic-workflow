---
timeout_seconds: 300
max_turns: 8
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
tags: [canary, routing, natural]
---

We pushed the new permit renewal build to prod about ten minutes ago. Can you keep an eye on it for a while and tell me if the error rate or the response times drift away from where they were before the push? If it looks bad I want to know right away.
