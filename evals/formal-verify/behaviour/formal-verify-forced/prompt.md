---
max_turns: 30
timeout_seconds: 900
allowed_tools: [Read, Glob, Grep, Write, Edit, Skill, TodoWrite, AskUserQuestion, Bash]
tags: [formal-verify, behaviour, forced]
---

Use the formal-verify skill on the guarantee in docs/claim.md, against the code in src/jobs/claim.py. Use the slug `job-claim`.

Nobody is around to answer questions: take the property from the doc and do not ask me anything. Do not download anything.

Environment note: Bash cannot write files here. Write every file with the Write tool. If TLC cannot run here, say which step that blocks and still write the model, the config and the properties, and run the check once anyway.

When you are done, quote the INVARIANT line of your config, and say in one sentence how two workers could both end up running the same job.
