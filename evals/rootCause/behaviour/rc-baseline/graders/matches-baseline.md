---
type: llm
focus: last_message
---

The repo's .agentic-workflow/repo.json names `classic` as the baseline variant. The reported variant is `modern`. Both app/variants/classic/discount.py and app/variants/modern/discount.py use `quantity > BULK_THRESHOLD`, and the module docstring of both says bulk means MORE THAN 10.

PASS if the final reply gives the baseline parity verdict `matches baseline`, cites the classic variant's discount.py (file and line) as the baseline code path, and does not propose changing the modern variant before the person confirms what is wanted.
FAIL if the verdict is `differs (bug)` or `differs (intended)`, if the reply never compares with the classic variant, or if it edits or proposes an immediate fix to modern/discount.py as if it were a plain bug.
