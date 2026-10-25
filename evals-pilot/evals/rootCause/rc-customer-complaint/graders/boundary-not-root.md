---
type: llm
focus: last_message
---

The skill requires a module boundary narrower than the repo root and generic dirs like src/ (the cause is in app/pricing/discount.py, the failing test in tests/).

PASS if the reply states a boundary (a path such as app/ or app/pricing) that is not the repo root, and keeps its fix inside it.
FAIL if it states the repo root or no boundary at all.
