---
type: regex
pattern: 'Would run:(?=[\s\S]*security-sentinel)(?=[\s\S]*performance-oracle)[\s\S]*pulls/23/reviews[\s\S]*pulls/23/reviews'
flags: i
---

Planted: two reviewers in the saved file, so two batched `gh api .../pulls/23/reviews` calls after `Would run:`, one per agent.
