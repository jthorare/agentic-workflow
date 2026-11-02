#!/usr/bin/env bash
# Workspace for the addressReview cases: a small recipe-app repo on a made-up PR branch (PR 77, shopping list export),
# plus the saved review for it under .aw-state/WORKSPACE-DIR-NAME/reviews/77.json (no remote, so the repo slug is the
# directory name, as skills/_shared/repo-slug.sh derives it). Offline; the stub gh reads .aw-eval/gh.json.
# Planted: src/export.js does not escape commas or quotes in CSV cells (blocking), src/parse.js uses parseInt so
# "1.5" becomes 1 (issue), and a nit (rename toCsv) that a default run must leave alone. Writes only under $PWD.
set -eu
export AW_GIT_MODE=full # the setup commits; the agent runs after this process, still readonly (as aw_fixture_repo)
slug=$(basename "$PWD")
mkdir -p src test .aw-eval ".aw-state/$slug/reviews"
cat > package.json <<'P'
{ "name": "pantry-api", "private": true, "scripts": { "test": "node --test" } }
P
cat > README.md <<'P'
# pantry-api
Recipe and pantry service. Shopping lists can be exported as CSV.
P
git init -q -b main
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "initial service"
git checkout -q -b feat/shopping-export
cat > src/parse.js <<'P'
// Reads a quantity typed by a person, such as "2" or "1.5".
function parseQuantity(text) {
  return parseInt(text, 10);
}

module.exports = { parseQuantity };
P
cat > src/export.js <<'P'
// Shopping list CSV export.
function toCsv(items) {
  const rows = items.map((i) => [i.name, i.quantity, i.unit].join(","));
  return ["name,quantity,unit", ...rows].join("\n");
}

module.exports = { toCsv };
P
cat > test/export.test.js <<'P'
const test = require("node:test");
const assert = require("node:assert");
const { toCsv } = require("../src/export");

test("exports a plain row", () => {
  assert.strictEqual(toCsv([{ name: "flour", quantity: 2, unit: "kg" }]), "name,quantity,unit\nflour,2,kg");
});
P
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "shopping list: CSV export and quantity parsing"
sha=$(git rev-parse HEAD)
git diff main..HEAD > .aw-eval/pr77.diff
SHA="$sha" SLUG="$slug" python3 - <<'P'
import json, os
sha, slug = os.environ["SHA"], os.environ["SLUG"]
diff = open(".aw-eval/pr77.diff").read()
view = {"number": 77, "title": "Shopping list CSV export", "headRefName": "feat/shopping-export",
        "baseRefName": "main", "url": "https://github.com/acme-kitchen/pantry-api/pull/77",
        "headRepository": {"id": "R_1", "name": "pantry-api"}}
gh = [
    {"match": ["pr", "view", "77"], "stdout": json.dumps(view), "stderr": "", "exit": 0},
    {"match": ["pr", "list"], "stdout": json.dumps([{"number": 77, "title": view["title"], "url": view["url"]}]), "stderr": "", "exit": 0},
    {"match": ["pr", "diff", "77"], "stdout": diff, "stderr": "", "exit": 0},
    {"match": ["pr", "checkout", "77"], "stdout": "", "stderr": "", "exit": 0},
    {"match": ["api", "repos/acme-kitchen/pantry-api/issues/77/comments"], "stdout": "[]", "stderr": "", "exit": 0},
    {"match": ["api", "repos/acme-kitchen/pantry-api/pulls/77/comments"], "stdout": "[]", "stderr": "", "exit": 0},
]
json.dump(gh, open(".aw-eval/gh.json", "w"), indent=2)
issue = lambda i, sev, path, summary, body: {"id": i, "severity": sev, "path": path, "diff_position": 5,
    "summary": summary, "body": body, "type": "inline", "addressed": False, "posted_comment_id": None}
state = {
    "pr": {"number": 77, "title": view["title"], "branch": "feat/shopping-export", "owner": "acme-kitchen",
           "repo": "pantry-api", "url": view["url"]},
    "commit_sha": sha, "review_run": 1, "previous_run_finding_count": None,
    "lenses": ["correctness-reviewer", "maintainability-reviewer"],
    "reviewed_at": "2026-10-30T08:00:00Z", "human_comments_fetched_at": "2026-10-30T08:05:00Z",
    "posted": False, "posted_at": None,
    "reviewers": [
        {"agent": "correctness-reviewer", "focus": "logic errors, edge cases", "summary": "2 findings", "issues": [
            issue("corr-0", "blocking", "src/export.js",
                  "CSV cells are not escaped",
                  "**[blocking] CSV cells are not escaped**\n\nAn ingredient named `salt, kosher` or `7\" tin` splits into extra columns or breaks the row. Wrap every cell in double quotes and double any quote inside it."),
            issue("corr-1", "issue", "src/parse.js",
                  "parseQuantity drops the fraction",
                  "**[issue] parseQuantity drops the fraction**\n\n`parseInt(\"1.5\", 10)` returns 1, so 1.5 cups of milk exports as 1. Parse it as a decimal number."),
        ]},
        {"agent": "maintainability-reviewer", "focus": "naming, structure", "summary": "1 nit", "issues": [
            issue("maint-0", "nit", "src/export.js", "Rename toCsv",
                  "**[nit] Rename toCsv**\n\n`buildCsv` would match the other builders in the service."),
        ]},
    ],
}
json.dump(state, open(f".aw-state/{slug}/reviews/77.json", "w"), indent=2)
P
