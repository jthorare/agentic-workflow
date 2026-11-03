---
type: regex
pattern: '"name":"(?:(?:Edit|MultiEdit|Write)"|Bash"(?=[^\n]*?(?:>|\btee |sed -i|writeFile)))(?=[^\n]*?src/export\.js)(?=[^\n]*?(?:replace|stringify|escape|quot))'
target: trace
---

Planted blocking finding: CSV cells with a comma or quote are not escaped. Any write counts (Edit, Write, or a Bash heredoc, sed -i or redirect): the first real run fixed it with Bash in 2 of 3 runs.
