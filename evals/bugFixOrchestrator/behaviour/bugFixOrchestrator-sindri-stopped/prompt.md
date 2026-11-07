---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
tags: [bugFixOrchestrator, behaviour, sindri]
---

Use the bugFixOrchestrator skill on this ticket: "CALC-9: average([2, 4]) returns 2.0, expected 3.0." This is a Sindri Step: the result goes where the Result block's gate says.

Environment note: `AW_SINDRI_BRANCH` is `sindri/inst-1/1/work`. rootCause already ran twice on this ticket and both times returned `unfixed` (no diagnosed handoff), so per Phase 2 step 3 the run stops as unresolved. Write the result for that stop; push nothing and open no PR.
