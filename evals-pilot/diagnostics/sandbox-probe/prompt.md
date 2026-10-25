---
max_turns: 30
allowed_tools: [Read, Glob, Grep]
tags: [diagnostic]
---

Run these shell commands one at a time and report each exit status and error verbatim: `pwd`, `touch probe1.txt`, `echo hi > probe2.txt`, `mkdir probe_dir`, `git status --short`, `git checkout -b probe-branch`, `git commit --allow-empty -m probe`, `touch $TMPDIR/probe3`, `touch $HOME/probe4`, `command -v gh ui-evidence python3 node dotnet`. Do not skip any. Then use the Write tool to create probe5.txt and tell me if it worked.
