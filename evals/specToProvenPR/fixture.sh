#!/usr/bin/env bash
# Workspace for the specToProvenPR forced case: a tiny tool-lending CLI plus an approved two-stage spec. No remote.
# Planted: the spec says loans last 14 days, loans.py (and its test) say 21. Stage 2 exits 3 when a loan is overdue.
# Canned world: stub `gh` answers the base-branch read (the stub PATH comes from the eval; this file only writes data).
# Writes only under $PWD.
set -eu
mkdir -p toolbox tests planning/specs/toolbox .aw-eval
: > toolbox/__init__.py
: > tests/__init__.py
cat > planning/specs/toolbox/DESIGN.md <<'D'
# Toolbox: lending and overdue

Status: APPROVED (2026-10-30, owner)

`python3 -m toolbox` is the tool library's desk CLI. Today it only has `list`.

## Stage 1: lend
`python3 -m toolbox lend drill ravi --on 2026-11-01` records the loan in `loans.json` and prints
`Lent: drill -> ravi (due 2026-11-15)`. Loans last 14 days.
Observable signal: that exact line, and `loans.json` holding one loan.

## Stage 2: overdue
`python3 -m toolbox overdue --on 2026-11-20` prints `drill (ravi) 5 days late` for each loan past its due date and
exits with status 3 when at least one loan is overdue, 0 when none is.
Observable signal: the printed line, plus the exit status in both cases.

Each stage ships as its own PR. Stage 2 reads the `loans.json` that stage 1 writes.
D
cat > README.md <<'D'
# toolbox
Run: `python3 -m toolbox list`. Tests: `python3 -m unittest`.
D
cat > toolbox/loans.py <<'D'
import datetime

LOAN_DAYS = 21


def due_date(start: datetime.date) -> datetime.date:
    return start + datetime.timedelta(days=LOAN_DAYS)
D
cat > toolbox/__main__.py <<'D'
import argparse

TOOLS = ["drill", "ladder", "saw"]


def main():
    parser = argparse.ArgumentParser(prog="toolbox")
    parser.add_argument("command", choices=["list"])
    parser.parse_args()
    print("\n".join(TOOLS))


main()
D
cat > tests/test_loans.py <<'D'
import datetime
import unittest

from toolbox.loans import due_date


class DueDate(unittest.TestCase):
    def test_three_weeks(self):
        self.assertEqual(due_date(datetime.date(2026, 11, 1)), datetime.date(2026, 11, 22))
D
cat > .aw-eval/gh.json <<'D'
[
  {"match": ["repo", "view"], "stdout": "main\n", "stderr": "", "exit": 0},
  {"match": ["pr", "list"], "stdout": "[]\n", "stderr": "", "exit": 0}
]
D
git init -q -b main
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "toolbox: list command and loan due date"
