---
type: regex
pattern: '"name":"(?:(?:Edit|MultiEdit|Write)"(?=[^\n]*?src/export\.js)|Bash"(?=[^\n]*?(?:>>?\s*(?:\\?["\x27])?[^\s"\x27\\]*src/export\.js|sed\s+-i[^\n]*?src/export\.js|\btee\s[^\n]*?src/export\.js|\bmv\s+\S+\s+[^\s"\x27\\]*src/export\.js|(?=[^\n]*?(?:writeFile|write_text|json\.dump|\.write\())[^\n]*?src/export\.js)))(?=[^\n]*?(?:replace|stringify|escape|quot))'
target: trace
---

Planted blocking finding: CSV cells with a comma or quote are not escaped. Any write that targets src/export.js counts (Edit, Write, or a Bash redirect, heredoc, sed -i, tee or mv into it, or a node/python script that writes it): the first real run fixed it with Bash in 2 of 3 runs. A command that only reads the file (`grep ... 2>/dev/null`) does not.
