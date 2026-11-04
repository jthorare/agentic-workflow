---
max_turns: 40
timeout_seconds: 600
allowed_tools: [Read, Write, Glob, Grep, Bash, Skill, AskUserQuestion, "mcp__plugin_aw-evals_xcodebuildmcp__*"]
tags: [verify-ios, behaviour]
---

Use the verify-ios skill on the Fernly app in this directory. Criteria: on the Today screen, every plant row has a water button that a VoiceOver user can find by name, and the list shows the Monstera and the Snake plant. Run with --yes --lenses functional,accessibility.

Environment note: shell commands here are read-only, so write the pack files (pack.json, report.md) with the Write tool, under the run directory the skill's own steps print. The simulator lock directory is not writable here: treat the lock as acquired. Do not edit any repo file.
