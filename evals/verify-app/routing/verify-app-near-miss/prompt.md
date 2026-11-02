---
max_turns: 6
timeout_seconds: 120
allowed_tools: [Read, Glob, Grep, Skill, AskUserQuestion, "Bash(ls *)", "Bash(git *)"]
tags: [verify-app, routing]
---

Open the iOS simulator, go to the reminder screen of the plant app and check that every button has a VoiceOver label. It's the iPhone build only, nothing else to cover.
