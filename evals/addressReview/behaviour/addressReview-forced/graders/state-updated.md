---
type: regex
pattern: '"name":"(?:(?:Edit|MultiEdit|Write)"(?=[^\n]*?77\.json)|Bash"(?=[^\n]*?(?:>>?\s*(?:\\?["\x27])?[^\s"\x27\\]*77\.json|sed\s+-i[^\n]*?77\.json|\btee\s[^\n]*?77\.json|\bmv\s+\S+\s+[^\s"\x27\\]*77\.json|(?=[^\n]*?(?:writeFile|write_text|json\.dump|\.write\())[^\n]*?77\.json)))(?=[^\n]*?addressed\W{1,6}true)'
flags: i
target: trace
---

The review state file marks the fixed findings addressed: true, through Edit, Write or a Bash command that writes the file (jq into a temp file then mv, sed -i, a redirect). A jq read that selects `addressed == true` does not count.
