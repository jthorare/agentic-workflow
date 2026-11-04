---
type: regex
pattern: '"text":"(?:(?:[^"\\]|\\.)*?\\n)?dispatch:\s*verify-desktop\s+args='
target: trace
---

The `dispatch: <sub-skill> args=<args>` line is written in the reply text before the Skill call (a Bash `echo` of it does not count). Read over the whole trace, not last_message: the sub-skill keeps talking after the dispatch and the run often ends on max_turns, so the line is never the last message (first real run, 2026-11-04: the desktop case missed it 3/3 that way, the named case 2/3).
