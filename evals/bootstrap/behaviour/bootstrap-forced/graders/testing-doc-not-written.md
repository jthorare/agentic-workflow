---
type: regex
target:
  source: file
  path: planning/TESTING.md
pattern: 'MARKER-KEEP-7f3a: hand-written by the team, do not regenerate\.'
---

Reads planning/TESTING.md itself, so a rewrite through Write, Edit or a Bash heredoc all fail it. The fixture's doc carries this
line; a run that leaves the doc alone keeps it.
