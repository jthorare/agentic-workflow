#!/usr/bin/env bash
# Workspace for rc-baseline: a two-variant shop. The "classic" variant is the baseline and the "modern" variant was copied
# from it, so a 10-item cart earns no bulk discount on either. The report is about "modern" only.
# Needs the per-repo config reader (#2909): .agentic-workflow/repo.json is the file it reads.
set -eu
. "$(dirname "${BASH_SOURCE[0]}")/../../../_lib/fixture-lib.sh"
mkdir -p app/variants/classic app/variants/modern tests .agentic-workflow
: > app/__init__.py
: > app/variants/__init__.py
: > app/variants/classic/__init__.py
: > app/variants/modern/__init__.py
: > tests/__init__.py
cat > .agentic-workflow/repo.json <<'P'
{
  "variants": {
    "axis": "storefront",
    "values": [
      { "name": "classic", "baseline": true },
      { "name": "modern" }
    ]
  }
}
P
for v in classic modern; do
cat > app/variants/$v/discount.py <<'P'
"""Bulk discount rules for this storefront. Bulk means MORE THAN 10 of one item (10% off that line)."""

BULK_THRESHOLD = 10
BULK_RATE = 0.10


def line_discount(quantity: int, unit_cents: int) -> int:
    if quantity > BULK_THRESHOLD:
        return int(quantity * unit_cents * BULK_RATE)
    return 0
P
cat > app/variants/$v/cart.py <<P
from app.variants.$v.discount import line_discount


def total_cents(lines):
    """lines: list of (quantity, unit_cents). Returns the payable total in cents."""
    total = 0
    for quantity, unit_cents in lines:
        total += quantity * unit_cents - line_discount(quantity, unit_cents)
    return total
P
done
cat > tests/test_modern_cart.py <<'P'
import unittest

from app.variants.modern.cart import total_cents


class ModernCartTotal(unittest.TestCase):
    def test_ten_items_get_ten_percent_off(self):
        self.assertEqual(total_cents([(10, 200)]), 1800)


if __name__ == "__main__":
    unittest.main()
P
aw_fixture_repo "initial two-variant shop"
