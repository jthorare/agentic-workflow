#!/usr/bin/env bash
# Workspace for the review cases: a small tool-lending desk with PR 14 checked out on a feature branch.
# Offline, no remote. The stub `gh` answers the reads /review makes from .aw-eval/gh.json; writes are refused.
# Planted defects in PR 14 (a correct review names both):
#   1. src/reminders.py due_soon: `< cutoff` drops loans due on the last day of the 3-day window (the docstring says the last day counts).
#   2. src/members.py reminder_address: `member["email"].strip()` crashes for a member with no email (data/members.json has one: Odile, null).
# Writes only under $PWD.
set -eu
mkdir -p src tests data .aw-eval
cat > README.md <<'P'
# loan-desk
Lending desk for a community tool library: who has which drill, and when it is due back.
P
cat > src/loans.py <<'P'
def is_overdue(loan, today):
    return loan["due"] < today
P
cat > src/members.py <<'P'
def display_name(member):
    return member["name"].strip()
P
cat > data/members.json <<'P'
[
  {"id": 1, "name": "Marta Lindqvist", "email": "marta@example.org"},
  {"id": 2, "name": "Joao Pereira", "email": "joao@example.org"},
  {"id": 3, "name": "Tamsin Ode", "email": "tamsin@example.org"},
  {"id": 4, "name": "Odile Brandt", "email": null}
]
P
git init -q -b main
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "initial lending desk"
git checkout -q -b feat/due-soon-reminders
cat > src/reminders.py <<'P'
from datetime import timedelta

from members import reminder_address

REMINDER_WINDOW_DAYS = 3


def due_soon(loans, today):
    """Loans due today or within the next REMINDER_WINDOW_DAYS days (the last day counts)."""
    cutoff = today + timedelta(days=REMINDER_WINDOW_DAYS)
    return [loan for loan in loans if today <= loan["due"] < cutoff]


def send_reminders(loans, members, today, send):
    sent = 0
    for loan in due_soon(loans, today):
        member = members[loan["member_id"]]
        send(reminder_address(member), "Your %s is due %s" % (loan["tool"], loan["due"].isoformat()))
        sent += 1
    return sent
P
cat >> src/members.py <<'P'


def reminder_address(member):
    return member["email"].strip().lower()
P
cat > tests/test_reminders.py <<'P'
from datetime import date

from reminders import due_soon

TODAY = date(2026, 11, 2)


def test_due_tomorrow_is_included():
    assert due_soon([{"tool": "drill", "due": date(2026, 11, 3)}], TODAY)


def test_due_next_week_is_skipped():
    assert not due_soon([{"tool": "drill", "due": date(2026, 11, 9)}], TODAY)
P
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "reminders: email members whose loans are due soon"
git diff main..HEAD > .aw-eval/pr14.diff
SHA=$(git rev-parse HEAD)
SHA="$SHA" python3 - <<'PY'
import json, os
diff = open(".aw-eval/pr14.diff").read()
sha = os.environ["SHA"]
adds = sum(1 for l in diff.splitlines() if l.startswith("+") and not l.startswith("+++"))
pr = {
    "number": 14,
    "title": "Email members whose loans are due soon",
    "body": "Adds due_soon() and send_reminders(): a member gets one email for each loan due within the next 3 days, the last day included.",
    "additions": adds,
    "deletions": 0,
    "headRefName": "feat/due-soon-reminders",
    "baseRefName": "main",
    "url": "https://github.com/toolshed/loan-desk/pull/14",
    "headRepository": {"name": "loan-desk", "owner": {"login": "toolshed"}},
}
def ok(stdout):
    return {"stdout": stdout, "stderr": "", "exit": 0}
rules = [
    dict(match=["pr", "list"], **ok(json.dumps([{"number": 14, "title": pr["title"], "url": pr["url"]}]))),
    dict(match=["pr", "diff", "14", "--name-only"], **ok("src/members.py\nsrc/reminders.py\ntests/test_reminders.py\n")),
    dict(match=["pr", "diff", "14"], **ok(diff)),
    dict(match=["pr", "view", "14", "--json", "files"], **ok(json.dumps(["src/members.py", "src/reminders.py", "tests/test_reminders.py"]))),
    dict(match=["pr", "view", "14", "--json", "commits"], **ok(sha + "\n")),
    dict(match=["pr", "view", "14"], **ok(json.dumps(pr))),
    dict(match=["repo", "view"], **ok(json.dumps({"nameWithOwner": "toolshed/loan-desk"}))),
]
json.dump(rules, open(".aw-eval/gh.json", "w"), indent=1)
PY
