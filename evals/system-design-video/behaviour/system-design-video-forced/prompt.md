---
max_turns: 30
timeout_seconds: 600
allowed_tools: [Read, Write, Edit, Bash, Glob, Grep, Skill, TodoWrite]
tags: [system-design-video, behaviour]
---

Use the system-design-video skill on docs/pet-sitter-booking-design.md.

Scope, brief and storyboard are already approved: engineering cut only, and ./storyboard.json is final, so don't rewrite it. Tokens are in ./design-tokens.json. Run the machine check, render, review the stills, encode into ./out/v1 under the name `petsitter`, and tell me what you got.

Environment notes: AW_RESULT=1 is set for this run. The browser stack lives in ./.aw-state/system-design-video/node_modules/playwright-core (set PLAYWRIGHT_CORE to it if the machine check can't find it). Don't post anything anywhere.
