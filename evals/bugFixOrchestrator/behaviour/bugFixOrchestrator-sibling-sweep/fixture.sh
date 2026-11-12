#!/usr/bin/env bash
# Two sibling screens (orders, invoices) both show a total through money_label(); the candidate fixes the orders screen only.
set -eu
. "$(dirname "${BASH_SOURCE[0]}")/../../../_lib/fixture-lib.sh"
mkdir -p screens .aw-eval
cat > screens/money.py <<'Q'
def money_label(cents):
    return "$" + str(cents / 100)   # prints $12.5, not $12.50
Q
cat > screens/orders.py <<'Q'
from screens.money import money_label


def orders_total(cents):
    return money_label(cents)
Q
cat > screens/invoices.py <<'Q'
from screens.money import money_label


def invoices_total(cents):
    return money_label(cents)
Q
cat > .aw-eval/candidate.diff <<'Q'
--- a/screens/orders.py
+++ b/screens/orders.py
@@ -3,2 +3,2 @@
 def orders_total(cents):
-    return money_label(cents)
+    return "$%.2f" % (cents / 100)
Q
aw_fixture_repo "initial screens"
