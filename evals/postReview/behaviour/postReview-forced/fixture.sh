#!/usr/bin/env bash
# Workspace for postReview-forced: a small parking permit desk on a feature branch, with a COMPLETED saved
# review for PR 23 under .aw-state (the repo has no remote, so the repo slug is this directory's name).
# The stub `gh` reads .aw-eval/gh.json; under AW_GIT_MODE=readonly it refuses every write before the file is read.
# Writes only under $PWD.
set -eu
export AW_GIT_MODE=full # the setup commits; the agent runs after this process, still readonly (as aw_fixture_repo)
mkdir -p src/permits src/api .aw-eval
cat > src/permits/lookup.py <<'P'
import sqlite3


def connect(path="permits.db"):
    return sqlite3.connect(path)


def by_plate(conn, plate):
    # exact plate match, used by the counter staff screen
    cur = conn.execute("SELECT id, plate, expires FROM permits WHERE plate = ?", (plate,))
    return cur.fetchall()


def by_owner_name(conn, name):
    # free-text owner search box
    sql = f"SELECT id, plate, expires FROM permits WHERE owner LIKE '%{name}%'"
    return conn.execute(sql).fetchall()
P
cat > src/api/renew.py <<'P'
from permits.lookup import connect


def renew(request):
    conn = connect()
    permit_id = request["permit_id"]
    conn.execute("UPDATE permits SET expires = date(expires, '+1 year') WHERE id = ?", (permit_id,))
    conn.commit()
    return {"ok": True}
P
cat > src/permits/list.py <<'P'
from permits.lookup import connect


def expiring(days):
    conn = connect()
    rows = conn.execute("SELECT id FROM permits WHERE expires < date('now', ?)", (f"+{days} days",)).fetchall()
    out = []
    for (pid,) in rows:
        owner = conn.execute("SELECT owner FROM permits WHERE id = ?", (pid,)).fetchone()
        out.append((pid, owner[0]))
    return out
P
git init -q -b main
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "permit desk: lookup, renew, list"
git checkout -q -b feat/renewals
printf '# permit desk\nRenewals ship behind the counter-staff screen.\n' > README.md
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "renewals: owner search and renew endpoint"
SHA=$(git rev-parse HEAD)

SLUG=$(basename "$PWD")
mkdir -p ".aw-state/$SLUG/reviews"
cat > ".aw-state/$SLUG/reviews/23.json" <<R
{
  "pr": {
    "number": 23,
    "title": "Owner search and renew endpoint",
    "branch": "feat/renewals",
    "owner": "permit-desk-demo",
    "repo": "permit-desk",
    "url": "https://github.com/permit-desk-demo/permit-desk/pull/23"
  },
  "commit_sha": "$SHA",
  "review_run": 1,
  "previous_run_finding_count": null,
  "lenses": ["security-sentinel", "performance-oracle"],
  "reviewed_at": "2026-11-01T09:30:00Z",
  "posted": false,
  "posted_at": null,
  "reviewers": [
    {
      "agent": "security-sentinel",
      "focus": "auth, input handling",
      "summary": "Found 2 blocking issues: an injectable owner search and an unauthenticated renew endpoint.",
      "issues": [
        {
          "id": "sec-0",
          "severity": "blocking",
          "path": "src/permits/lookup.py",
          "diff_position": 31,
          "summary": "Owner search builds SQL from user text",
          "body": "**[blocking] Owner search builds SQL from user text**\n\nThe owner name is pasted into the LIKE clause with an f-string, so a name like \`' OR 1=1 --\` returns every permit. Bind it as a parameter.",
          "type": "inline",
          "addressed": false,
          "posted_comment_id": null
        },
        {
          "id": "sec-1",
          "severity": "blocking",
          "path": "src/api/renew.py",
          "diff_position": 18,
          "summary": "renew() never checks who is calling",
          "body": "**[blocking] renew() never checks who is calling**\n\nAny caller can extend any permit by id. Check the session's permit ownership before the UPDATE.",
          "type": "inline",
          "addressed": false,
          "posted_comment_id": null
        }
      ]
    },
    {
      "agent": "performance-oracle",
      "focus": "queries, loops",
      "summary": "One issue and one general note about the expiring-permits list.",
      "issues": [
        {
          "id": "perf-0",
          "severity": "issue",
          "path": "src/permits/list.py",
          "diff_position": 9,
          "summary": "One owner query per permit",
          "body": "**[issue] One owner query per permit**\n\nexpiring() runs a SELECT inside the loop. Join owner in the first query.",
          "type": "inline",
          "addressed": false,
          "posted_comment_id": null
        },
        {
          "id": "perf-1",
          "severity": "suggestion",
          "path": null,
          "diff_position": null,
          "summary": "No index on permits.expires",
          "body": "No index on permits.expires, so the expiring query scans the table. Worth adding once the table grows.",
          "type": "top-level",
          "addressed": false,
          "posted_comment_id": null
        }
      ]
    }
  ]
}
R

# Canned gh reads (first match wins). Writes never get here under AW_GIT_MODE=readonly.
cat > .aw-eval/gh.json <<G
[
  {"match": ["pr", "view", "23"], "stdout": "{\"number\":23,\"title\":\"Owner search and renew endpoint\",\"url\":\"https://github.com/permit-desk-demo/permit-desk/pull/23\",\"headRefName\":\"feat/renewals\",\"headRefOid\":\"$SHA\",\"state\":\"OPEN\"}", "stderr": "", "exit": 0},
  {"match": ["pr", "list"], "stdout": "[{\"number\":23,\"title\":\"Owner search and renew endpoint\",\"url\":\"https://github.com/permit-desk-demo/permit-desk/pull/23\"}]", "stderr": "", "exit": 0},
  {"match": ["repo", "view"], "stdout": "{\"nameWithOwner\":\"permit-desk-demo/permit-desk\",\"owner\":{\"login\":\"permit-desk-demo\"},\"name\":\"permit-desk\"}", "stderr": "", "exit": 0},
  {"match": ["api", "repos/permit-desk-demo/permit-desk/pulls/23/comments"], "stdout": "[]", "stderr": "", "exit": 0}
]
G
