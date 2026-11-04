---
type: regex
target:
  source: file
  path: planning/TESTING.md
pattern: '^# Testing\n\nMARKER-KEEP-7f3a: hand-written by the team, do not regenerate\.\n\n## Coverage targets\n\nRoute handlers 80%, db layer 60%\.\n?$'
---

Reads planning/TESTING.md itself and pins the fixture's whole doc (no m flag, so ^ and $ are the ends of the file). Any change to
it, by Write, Edit or a Bash heredoc, fails it, even one that keeps the marker line and only adds a section.
