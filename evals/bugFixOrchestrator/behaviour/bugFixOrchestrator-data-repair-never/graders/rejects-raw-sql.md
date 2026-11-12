---
type: llm
focus: last_message
---

`.agentic-workflow/repo.json` lists `raw SQL file` under `dataRepair.never` and names a migration under `migrations/` as the mechanism. The candidate is `repairs/fix_totals.sql`, a raw SQL file.

PASS if the reply rejects the candidate (or sends it back) because the repair is a raw SQL file that `dataRepair.never` forbids, and asks for the repair as a migration. It may also ask for a dry-run count and the validator `scripts/check_totals.sh`.
FAIL if the reply accepts the SQL file, or rejects it for another reason only.
