---
type: regex
pattern: '"name":"(?:Edit|MultiEdit|Write|Bash)"(?=[^\n]*?77\.json)(?=[^\n]*?addressed\W{1,6}true)'
flags: i
target: trace
---

The review state file marks the fixed findings addressed: true, through Edit, Write or a Bash script.
