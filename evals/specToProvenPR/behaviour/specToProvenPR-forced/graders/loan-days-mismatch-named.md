---
type: regex
pattern: '(\b14\b.{0,300}\b21\b|\b21\b.{0,300}\b14\b)'
flags: is
---

Planted: the spec says loans last 14 days, toolbox/loans.py (LOAN_DAYS = 21) and its test say 21.
