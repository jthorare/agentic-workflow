---
type: tool_used
tool: Bash
input_match: '(?=[\s\S]*--shout)[\s\S]*python3(?: -\w+)* -m greeter\b'
---

One Bash call that runs `python3 -m greeter` (with any interpreter flags, such as -B) and passes --shout, literally or through a loop
variable (`for a in "ADA --shout"; do python3 -m greeter $a; done`).
