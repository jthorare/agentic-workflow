---
type: regex
pattern: 'AGENTS\.md\W{0,6}(?:\[|:|-|\s)*\s*missing|missing.{0,40}AGENTS\.md|AGENTS\.md[^\n]{0,160}?\b(?:were|are|is)\s+(?:all\s+)?missing'
flags: is
---

Planted: the repo has no AGENTS.md. A list on one line ("`AGENTS.md`, `CLAUDE.md`, ... were all missing") counts too.
