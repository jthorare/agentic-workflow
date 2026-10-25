#!/usr/bin/env bash
# Workspace for the rootCause cases: a small cart app with one real bug and a failing unit test.
set -eu
AW_LIB="$(cd "$(dirname "${BASH_SOURCE[0]}")/../_lib" && pwd)"
. "$AW_LIB/shim.sh"
shim_toolkit
mkdir -p app/pricing tests billing
cat > app/__init__.py <<'P'
P
cat > app/pricing/__init__.py <<'P'
P
cat > app/pricing/discount.py <<'P'
"""Bulk discount rules. Spec: 10 or more of one item earns 10% off that line."""

BULK_THRESHOLD = 10
BULK_RATE = 0.10


def line_discount(quantity: int, unit_cents: int) -> int:
    if quantity > BULK_THRESHOLD:
        return int(quantity * unit_cents * BULK_RATE)
    return 0
P
cat > app/cart.py <<'P'
from app.pricing.discount import line_discount


def total_cents(lines):
    """lines: list of (quantity, unit_cents). Returns the payable total in cents."""
    total = 0
    for quantity, unit_cents in lines:
        gross = quantity * unit_cents
        total += gross - line_discount(quantity, unit_cents)
    return total
P
cat > billing/legacy.py <<'P'
"""Legacy invoice exporter. Owned by another team; do not change."""
LEGACY_THRESHOLD = 10


def legacy_bulk(quantity: int) -> bool:
    return quantity > LEGACY_THRESHOLD
P
cat > tests/__init__.py <<'P'
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
git init -q -b main
git add -A
git -c user.email=eval@example.com -c user.name=eval commit -q -m "initial cart"
