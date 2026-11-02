---
type: regex
pattern: '(?:checkout.{0,240}(?:503|fail|exit 22|error|down)|(?:503|fail|exit 22|error).{0,240}checkout)'
flags: is
---

The planted failure: the checkout ping smoke test answers 503. The reply must name it.
