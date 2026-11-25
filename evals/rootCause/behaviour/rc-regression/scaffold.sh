#!/usr/bin/env bash
# Workspace for rc-regression: the cart app with three commits. PR #17 ("tidy discount constants") introduced the bug.
set -eu
. "$(dirname "${BASH_SOURCE[0]}")/../../../_lib/fixture-lib.sh"
mkdir -p app/pricing tests .aw-eval
: > app/__init__.py
: > app/pricing/__init__.py
: > tests/__init__.py
cat > app/pricing/discount.py <<'P'
"""Bulk discount rules. Spec: 10 or more of one item earns 10% off that line."""

BULK_THRESHOLD = 10
BULK_RATE = 0.10


def line_discount(quantity: int, unit_cents: int) -> int:
    if quantity >= BULK_THRESHOLD:
        return int(quantity * unit_cents * BULK_RATE)
    return 0
P
cat > app/cart.py <<'P'
from app.pricing.discount import line_discount


def total_cents(lines):
    """lines: list of (quantity, unit_cents). Returns the payable total in cents."""
    total = 0
    for quantity, unit_cents in lines:
        total += quantity * unit_cents - line_discount(quantity, unit_cents)
    return total
P
cat > tests/test_cart.py <<'P'
import unittest

from app.cart import total_cents


class CartTotal(unittest.TestCase):
    def test_nine_items_pay_full_price(self):
        self.assertEqual(total_cents([(9, 200)]), 1800)

    def test_ten_items_get_ten_percent_off(self):
        self.assertEqual(total_cents([(10, 200)]), 1800)

    def test_twenty_items_get_ten_percent_off(self):
        self.assertEqual(total_cents([(20, 200)]), 3600)


if __name__ == "__main__":
    unittest.main()
P
aw_fixture_repo "feat: bulk discount at 10 or more (#12)"
(
  export AW_GIT_MODE=full GIT_AUTHOR_DATE="2026-10-01T10:00:00Z" GIT_COMMITTER_DATE="2026-10-01T10:00:00Z"
  python3 - <<'P'
import re
p = "app/pricing/discount.py"
s = open(p).read()
s = s.replace("quantity >= BULK_THRESHOLD", "quantity > BULK_THRESHOLD")
s += '''

def describe_discount(quantity: int) -> str:
    return "bulk" if quantity > BULK_THRESHOLD else "none"
'''
open(p, "w").write(s)
P
  git add -A
  git commit -q -m "refactor: tidy discount constants and add describe_discount (#17)" -m "Adds describe_discount for the receipt line. Intended: no change to who gets the bulk discount."
  export GIT_AUTHOR_DATE="2026-10-05T10:00:00Z" GIT_COMMITTER_DATE="2026-10-05T10:00:00Z"
  printf '# Cart\n\nBulk discount: 10 or more of one item earns 10%% off that line.\n' > README.md
  git add -A
  git commit -q -m "docs: describe the cart in the readme (#18)"
)
# The stub gh answers `gh pr view 17` from .aw-eval/gh.json (no remote: nothing to push to, and the PR number comes from the subject).
cat > .aw-eval/gh.json <<'P'
[
 {"match": ["pr", "view", "17"], "stdout": "title:\trefactor: tidy discount constants and add describe_discount\nstate:\tMERGED\nnumber:\t17\n--\nAdds describe_discount for the receipt line. Intended: no change to who gets the bulk discount; 10 or more of one item still earns 10% off.\n", "exit": 0}
]
P
