---
type: regex
pattern: '((no|without|never|missing|not)\s+(a\s+)?(retr(y|ies)|dead[- ]letter)|(charge|payment)[^.\n]{0,80}(skipped|dropped|lost|silently)|(silently|quietly)\s+(skip|drop|lose|lost|dropped|skipped))'
flags: i
---

Planted: a failed charge is skipped with no retry or dead-letter, so a permit stays unpaid unnoticed.
