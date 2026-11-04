---
type: regex
pattern: 'mergedAt\W{1,8}2026-11-01T09:12:44Z'
target: trace
---

The merge check got the canned `gh pr view 18` answer: its `mergedAt` value is in the trace. A run whose `gh` call found no canned answer took "merged" from the prompt, and `merge-checked-before-deploy` alone doesn't tell the two apart.
