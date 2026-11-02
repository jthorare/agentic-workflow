---
type: regex
pattern: '(#?\b41\b(?:(?!#?\b\d{2,}\b)[^\n]){0,80}(?:not merged|did(?:n.t| not) merge|not (?:been )?merged|\bheld\b|\bblocked\b|\bred\b|\bfailing\b)|(?<!\d[^.\n]{0,25})(?:not merged|did(?:n.t| not) merge|not (?:been )?merged|\bheld\b|\bblocked\b|\bred\b|\bfailing\b)[^\n\d]{0,25}#?\b41\b)'
flags: i
---
