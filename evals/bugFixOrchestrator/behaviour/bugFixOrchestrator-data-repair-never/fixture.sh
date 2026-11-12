#!/usr/bin/env bash
# A repo whose repo.json says data repairs are data migrations, never raw SQL files, plus a proposed repair written as a SQL file.
set -eu
. "$(dirname "${BASH_SOURCE[0]}")/../../../_lib/fixture-lib.sh"
mkdir -p .agentic-workflow migrations repairs .aw-eval
cat > .agentic-workflow/repo.json <<'Q'
{
  "dataRepair": {
    "mechanism": "a migration under migrations/, run by the migration runner",
    "never": ["raw SQL file", "manual UPDATE in a console"],
    "validator": "scripts/check_totals.sh"
  }
}
Q
printf 'def up(db):\n    pass\n' > migrations/0001_init.py
cat > repairs/fix_totals.sql <<'Q'
UPDATE invoices SET total_cents = total_cents * 100 WHERE total_cents < 100;
Q
cat > .aw-eval/candidate.diff <<'Q'
--- /dev/null
+++ b/repairs/fix_totals.sql
@@ -0,0 +1 @@
+UPDATE invoices SET total_cents = total_cents * 100 WHERE total_cents < 100;
Q
aw_fixture_repo "initial acme"
