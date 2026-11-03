---
type: regex
pattern: '"name":"(?:(?:Edit|MultiEdit|Write)"|Bash"(?=[^\n]*?(?:>|\btee |sed -i|writeFile)))(?=[^\n]*?src/parse\.js)(?=[^\n]*?(?:parseFloat|Number\(|\+text))'
target: trace
---

Planted issue: parseInt drops the fraction of "1.5". Any write counts (Edit, Write, or a Bash sed -i, heredoc or redirect).
