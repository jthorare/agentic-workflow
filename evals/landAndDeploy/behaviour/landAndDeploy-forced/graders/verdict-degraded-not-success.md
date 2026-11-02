---
type: regex
pattern: '\A(?!.*verdict\W{0,6}success).*\b(?:DEGRADED|FAILED)\b'
flags: is
---

One smoke test failed, so the verdict is DEGRADED (or FAILED), and the reply never says "Verdict: SUCCESS".
