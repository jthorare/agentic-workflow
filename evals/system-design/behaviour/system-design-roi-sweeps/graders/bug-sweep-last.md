---
type: regex
pattern: 'Bug sweep(?![\s\S]*Bug sweep)[\s\S]*\bPh\d+\b'
flags: i
match: not_contains
---

No phase comes after the bug sweep: it is the map's last phase.
