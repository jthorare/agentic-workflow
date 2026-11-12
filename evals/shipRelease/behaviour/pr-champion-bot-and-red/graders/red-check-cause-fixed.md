---
type: regex
pattern: '"name":"(?:(?:Edit|MultiEdit|Write)"(?=[^\n]*?src/slug\.js)|Bash"(?=[^\n]*?(?:>>?\s*(?:\\?["\x27])?[^\s"\x27\\]*src/slug\.js|sed\s+-i[^\n]*?src/slug\.js|\btee\s[^\n]*?src/slug\.js|(?=[^\n]*?(?:writeFile|write_text|\.write\())[^\n]*?src/slug\.js)))(?=[^\n]*?(?:\\\\s\+|\[ \]\+| \+/g|\\\\s\*))'
target: trace
---

The failing check is test "collapses runs of spaces": slugify replaces single spaces only, so "Hello  World" gives "hello--world". The fix is a pattern that matches a run (/\s+/g, / +/g or [ ]+) in a write to src/slug.js.
