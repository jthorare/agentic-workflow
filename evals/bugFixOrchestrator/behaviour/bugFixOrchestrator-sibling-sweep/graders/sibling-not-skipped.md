---
type: llm
focus: last_message
---

The sweep table in `resolution.md` (and the final reply) is the check. `screens/invoices.py` calls the same `money_label()` that prints `$12.5`, and the candidate changed only `screens/orders.py`.

PASS if the table has a row for the invoices screen with status `fixed` (the run fixed it too) or `follow-up <link or issue text>`, and the orders screen is `fixed`. Every row has one of `fixed`, `not affected (why)` or `follow-up <link>`.
FAIL if the invoices screen is missing, is `not affected`, or the table is absent. FAIL if the run calls the bug resolved with no mention of the second screen.
