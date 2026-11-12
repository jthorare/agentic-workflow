---
type: regex
pattern: '"name":"(?:(?:Edit|MultiEdit|Write)"(?=[^\n]*?src/slug\.js)|Bash"(?=[^\n]*?(?:>>?\s*(?:\\?["\x27])?[^\s"\x27\\]*src/slug\.js|sed\s+-i[^\n]*?src/slug\.js|\btee\s[^\n]*?src/slug\.js|(?=[^\n]*?(?:writeFile|write_text|\.write\())[^\n]*?src/slug\.js)))(?=[^\n]*?TypeError)'
target: trace
---

acme-lint[bot]'s thread asks slugify to throw a TypeError for a non-string input. Any write that targets src/slug.js and carries TypeError counts (Edit, Write, or a Bash redirect, sed -i, tee or script). Reading the file or the thread does not.
