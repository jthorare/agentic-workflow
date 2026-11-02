#!/usr/bin/env bash
# Workspace for the weeklyRetro cases: a small recipe app with a week of history. Offline, no remote.
# Dates are relative to now (GIT_AUTHOR_DATE / GIT_COMMITTER_DATE), so the window always holds the same 10 commits.
# Planted: Ana Ruiz 6 and Ben Okoye 4 commits in the last 7 days; recipes/ is the busiest area (6 of 10 commits);
# Ben's "add shopping list export" is reverted; one older commit (20 days ago) falls outside the window.
# Writes only under $PWD.
set -eu
export AW_GIT_MODE=full # the setup commits; the agent runs after this process, still readonly (as aw_fixture_repo)
mkdir -p .aw-eval
# weeklyRetro reads no PR or issue state; an empty list makes any gh call fail visibly.
printf '[]\n' > .aw-eval/gh.json

git init -q -b main

# stamp <days-ago> [HH:MM]: ISO timestamp in UTC, GNU date first, BSD date second.
stamp() {
  local d="$1" t="${2:-12:00}"
  if [ "$d" = 0 ]; then date -u +"%Y-%m-%dT%H:%M:%S+0000"; return; fi
  date -u -d "$d days ago $t" +"%Y-%m-%dT%H:%M:%S+0000" 2>/dev/null \
    || date -u -v-"${d}"d +"%Y-%m-%dT${t}:00+0000"
}

# commit <name> <days-ago> <message>: commits whatever is staged as that person on that day.
commit() {
  local name="$1" days="$2" msg="$3" email ts
  email="$(printf '%s' "$name" | tr 'A-Z ' 'a-z.')@example.com"
  ts="$(stamp "$days")"
  GIT_AUTHOR_NAME="$name" GIT_AUTHOR_EMAIL="$email" GIT_AUTHOR_DATE="$ts" \
  GIT_COMMITTER_NAME="$name" GIT_COMMITTER_EMAIL="$email" GIT_COMMITTER_DATE="$ts" \
    git commit -q -m "$msg"
}

mkdir -p recipes pantry
printf '# recipe-box\nSave recipes and track what is in your pantry.\n' > README.md
printf 'def all_recipes():\n    return []\n' > recipes/store.py
printf 'STOCK = {}\n' > pantry/stock.py
git add -A
commit "Ana Ruiz" 20 "feat: initial recipe store and pantry"

printf 'def search(q):\n    return [r for r in all_recipes() if q in r]\n' > recipes/search.py
git add -A; commit "Ana Ruiz" 6 "feat: add recipe search"

printf 'STOCK = {}\n\ndef add(item, qty):\n    STOCK[item] = STOCK.get(item, 0) + qty\n' > pantry/stock.py
git add -A; commit "Ana Ruiz" 6 "feat: pantry stock add"

printf 'def search(q):\n    if not q:\n        return []\n    return [r for r in all_recipes() if q in r]\n' > recipes/search.py
git add -A; commit "Ben Okoye" 5 "fix: search handles an empty query"

printf 'def export(items):\n    return "\\n".join(items)\n' > recipes/shopping_export.py
git add -A; commit "Ben Okoye" 4 "feat: add shopping list export"
EXPORT_SHA="$(git rev-parse HEAD)"

printf 'def search(q):\n    if not q:\n        return []\n    hits = [r for r in all_recipes() if q in r]\n    return sorted(hits, key=lambda r: r.find(q))\n' > recipes/search.py
git add -A; commit "Ana Ruiz" 4 "refactor: rank search results by title match"

# The revert, authored by Ben three days ago.
ts="$(stamp 3)"
GIT_AUTHOR_NAME="Ben Okoye" GIT_AUTHOR_EMAIL="ben.okoye@example.com" GIT_AUTHOR_DATE="$ts" \
GIT_COMMITTER_NAME="Ben Okoye" GIT_COMMITTER_EMAIL="ben.okoye@example.com" GIT_COMMITTER_DATE="$ts" \
  git revert --no-edit "$EXPORT_SHA" >/dev/null

printf 'def search(q):\n    if not q:\n        return []\n    words = q.lower().split()\n    hits = [r for r in all_recipes() if all(w in r for w in words)]\n    return sorted(hits, key=lambda r: r.find(words[0]))\n' > recipes/search.py
git add -A; commit "Ana Ruiz" 2 "fix: search splits the query into words"

printf '# recipe-box\nSave recipes and track what is in your pantry.\n\nSearch takes several words.\n' > README.md
git add -A; commit "Ben Okoye" 2 "docs: mention multi-word search"

printf 'STOCK = {}\n\ndef add(item, qty):\n    STOCK[item] = STOCK.get(item, 0) + qty\n\ndef remove(item, qty):\n    STOCK[item] = max(0, STOCK.get(item, 0) - qty)\n' > pantry/stock.py
git add -A; commit "Ana Ruiz" 1 "feat: pantry stock remove"

printf 'STOCK = {}\n\ndef add(item, qty):\n    STOCK[item] = STOCK.get(item, 0) + qty\n\ndef remove(item, qty):\n    STOCK[item] = max(0, STOCK.get(item, 0) - qty)\n\ndef low(limit=1):\n    return [i for i, q in STOCK.items() if q < limit]\n' > pantry/stock.py
git add -A; commit "Ana Ruiz" 0 "feat: pantry low-stock list"
