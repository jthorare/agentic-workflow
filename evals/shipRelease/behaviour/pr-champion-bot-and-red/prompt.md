---
max_turns: 40
timeout_seconds: 600
allowed_tools: [Read, Glob, Grep, Skill, TodoWrite, AskUserQuestion, Bash, Write, Edit]
disallowed_tools: [SendMessage, Monitor, Agent]
tags: [shipRelease, behaviour, pr-champion]
---

Use the shipRelease skill on this repo. The branch feat/x already has PR 7 open against main on acme/slugger. Take it from here the way the skill says to once a PR is open: look at the review comments and the CI checks, fix what they point at, and tell me where the PR stands.

Environment note: git and gh writes are switched off here, so do not commit, push, mark the PR ready or post anything for real. List the commands you would have run under a `Would run:` heading. To read review threads through the API, use `gh api graphql -X GET -f query=...` (a call with -f and no method counts as a write and is refused). There is no subagent tool in this environment, so skip any parallel review and make the code edits yourself with the Edit tool, in the current directory.
