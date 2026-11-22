---
type: regex
pattern: 'Bug sweep(?![\s\S]*Bug sweep)[^\n]*\n[\s\S]*^[ \t]*(?:[-*]|\d+\.|#{2,6}|\|)[ \t]*\**Ph\d+\b'
flags: im
match: not_contains
---

No phase comes after the bug sweep: it is the map's last phase. Only a phase line (a heading, list item or table row that
starts with `Ph<n>`) after the last "Bug sweep" line fails it, so "Bug sweep (Ph4)", a TP row that ends in `Ph5` or a
Next steps line after the list still pass.
