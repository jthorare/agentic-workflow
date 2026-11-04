---
max_turns: 4
timeout_seconds: 120
allowed_tools: [Read, Glob, Grep, Skill, AskUserQuestion, Bash]
tags: [verify-ios, routing]
---

My plant-watering app's "water now" flow got rewritten today. Can you run it on a simulator for real: the happy path, a plant that's already watered, and make sure VoiceOver users can still reach every button? I want evidence it holds up before I open the PR, not a guess from the diff.
