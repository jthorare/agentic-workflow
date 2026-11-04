---
max_turns: 30
timeout_seconds: 480
allowed_tools: [Read, Glob, Grep, Skill, Agent, TodoWrite, AskUserQuestion, Bash, Write]
tags: [landAndDeploy, behaviour, forced]
---

Use the landAndDeploy skill on PR 18 of this repo (toollib). It is already merged. Deploy it with the repo's deploy config, hit the health endpoint, go through the smoke tests after it, and tell me the verdict and why.

Environment note: `gh` and `curl` here are canned stand-ins and the deploy command is a local script; run them exactly as the config says. Nothing may be committed, pushed or merged. This is a non-interactive run, so if the skill would ask you a question, put the question and your recommendation in the reply instead.
