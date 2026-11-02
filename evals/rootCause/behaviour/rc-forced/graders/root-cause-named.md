---
type: llm
focus: last_message
---

The real root cause: `line_discount` in app/pricing/discount.py compares `quantity > BULK_THRESHOLD`, a strict comparison, so a quantity of exactly 10 gets no discount although the spec says "10 or more".

PASS if the final reply names this: the strict `>` (an off-by-one at the boundary) in line_discount / discount.py as the cause.
FAIL if the reply blames something else (rounding, cart.total_cents, the test, billing/legacy.py) or only says "fixed it" without naming the cause.
