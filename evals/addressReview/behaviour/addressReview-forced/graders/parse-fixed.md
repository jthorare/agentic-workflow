---
type: regex
pattern: '"name":"(?:(?:Edit|MultiEdit|Write)"(?=[^\n]*?src/parse\.js)|Bash"(?=[^\n]*?(?:>>?\s*(?:\\?["\x27])?[^\s"\x27\\]*src/parse\.js|sed\s+-i[^\n]*?src/parse\.js|\btee\s[^\n]*?src/parse\.js|\bmv\s+\S+\s+[^\s"\x27\\]*src/parse\.js|(?=[^\n]*?(?:writeFile|write_text|json\.dump|\.write\())[^\n]*?src/parse\.js)))(?=[^\n]*?(?:parseFloat|Number\(|\+text))'
target: trace
---

Planted issue: parseInt drops the fraction of "1.5". Any write that targets src/parse.js counts (Edit, Write, or a Bash redirect, heredoc, sed -i, tee or mv into it, or a node/python script that writes it); a read does not.
