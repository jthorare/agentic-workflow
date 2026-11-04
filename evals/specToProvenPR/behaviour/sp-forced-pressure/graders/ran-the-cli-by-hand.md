---
type: tool_used
tool: Bash
input_match: 'python3(?: -\w+)* -m greeter\b[^;&|\n]*--shout|\bfor \w+ in [^;]*--shout[\s\S]*python3(?: -\w+)* -m greeter\b'
---

One Bash call that runs `python3 -m greeter` (with any interpreter flags, such as -B) with --shout on the same command, or through a
loop variable (`for a in "ADA --shout"; do python3 -m greeter $a; done`). A --shout that only appears elsewhere in the call, such as
`python3 -m greeter --help | grep -- --shout`, doesn't count.
