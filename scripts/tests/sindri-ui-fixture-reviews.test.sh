#!/usr/bin/env bash
# Tests for `scripts/sindri-ui-fixture.sh reviews up|down`: the live profile gets review.login and workflow.forge, aw-forge-fake is called to build
# the PR's world inside the home (never the real $AW_STATE_DIR), the PR is stored as a Context record with the number the forge gave, serve is
# restarted on the forge state, a second run replaces rather than adds, and down undoes it.
# Run: bash scripts/tests/sindri-ui-fixture-reviews.test.sh. No dotnet and no real sindri or forge: stubs record their arguments.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
contains() { # $1 name, $2 haystack, $3 needle
  case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in:      $2"; fail=1 ;; esac
}
lacks() { # $1 name, $2 haystack, $3 needle
  case "$2" in *"$3"*) echo "not ok - $1"; echo "  found: $3"; echo "  in:    $2"; fail=1 ;; *) echo "ok - $1" ;; esac
}

WORK="$(mktemp -d)"
trap 'rm -r "$WORK"' EXIT
export TMPDIR="$WORK/tmp" AW_STATE_DIR="$WORK/state"
mkdir -p "$TMPDIR" "$AW_STATE_DIR/cache/bin" "$WORK/bin"

command -v jq >/dev/null || { echo "not ok - jq is needed"; exit 1; }
command -v python3 >/dev/null || command -v sqlite3 >/dev/null || { echo "not ok - sqlite3 or python3 is needed"; exit 1; }
sqlq() { # $1 db, $2 sql: statements in order, rows printed '|'-joined
  if command -v sqlite3 >/dev/null 2>&1; then sqlite3 "$1" "$2"; else
    python3 -I -c '
import sqlite3, sys
con = sqlite3.connect(sys.argv[1], isolation_level=None)
for row in con.execute(sys.argv[2]):
    print("|".join("" if v is None else str(v) for v in row))' "$1" "$2"
  fi
}
migrate() { # $1 db, $2 migration file
  if command -v sqlite3 >/dev/null 2>&1; then sqlite3 "$1" < "$2"; else
    python3 -I -c '
import sqlite3, sys
con = sqlite3.connect(sys.argv[1], isolation_level=None)
con.executescript(open(sys.argv[2]).read())' "$1" "$2"
  fi
}

# A stub SINDRI: records its arguments and the AW_STATE_DIR it ran with, and answers the profile calls the fixture makes.
STUB="$WORK/bin/sindri-stub"
cat > "$STUB" <<'EOF'
#!/usr/bin/env bash
echo "$* [state=$AW_STATE_DIR]" >> "$STUB_LOG"
case "$*" in
  "profile validate --json")
    ok=false; [ "$(cat "$STUB_LOG.approved" 2>/dev/null)" = "$(cksum < "$AW_SINDRI_HOME/profile/profile.yaml")" ] && ok=true
    echo "{\"valid\":true,\"hash\":\"abcdef0123456789\",\"approved\":$ok}" ;;
  "profile approve abcdef0123456789")
    IFS= read -r line; echo "typed=$line" >> "$STUB_LOG"
    cksum < "$AW_SINDRI_HOME/profile/profile.yaml" > "$STUB_LOG.approved" ;;
esac
exit 0
EOF
chmod +x "$STUB"
export STUB_LOG="$WORK/calls"
: > "$STUB_LOG"

# A stub aw-forge-fake (AW_FORGE_FAKE): records the arguments with the environment the fixture gives it, hands out pull number 7 and keeps the files seed.
FORGE="$WORK/bin/forge-stub"
cat > "$FORGE" <<'EOF'
#!/usr/bin/env bash
echo "$* [state=$AW_STATE_DIR adapter=$AW_FORGE_ADAPTER hosts=$AW_FORGE_HOSTS login=$AW_FORGE_LOGIN]" >> "$FORGE_LOG"
case "$1 $2" in
  "open --repo") echo '{"number":7,"link":"https://github.com/acme/api/pull/7"}' ;;
  "seed files") while [ $# -gt 0 ]; do [ "$1" != --from ] || cp "$2" "$FORGE_LOG.files"; shift; done; echo '{"ok":true}' ;;
  *) echo '{"ok":true}' ;;
esac
EOF
chmod +x "$FORGE"
export FORGE_LOG="$WORK/forge-calls"
: > "$FORGE_LOG"

PJ="$TMPDIR/proj"
mkdir -p "$PJ/profile" "$PJ/context"
printf 'schemaVersion: 1\nuser: fixture\nservices:\n  heartbeat: { every: 5s }\n' > "$PJ/profile/profile.yaml"
PJDB="$PJ/context/context.db"
for m in 0001 0018 0019 0020 0022 0023 0029 0030; do migrate "$PJDB" "$(ls "$ROOT"/src/Context/Aw.Context.Infrastructure/Store/Migrations/${m}_*.sql)"; done
rev() { AW_SINDRI_HOME="$PJ" SINDRI="$STUB" AW_FORGE_FAKE="$FORGE" bash "$FIXTURE" reviews "$@"; }

out="$(rev up 2>"$WORK/err")"; code=$?
check "reviews up exits 0" "$code" "0"
[ "$code" -eq 0 ] || cat "$WORK/err"
check "it prints the page address, the head sha and the file:line" "$out" "/sindri/reviews/acme/api/7
1f2e3d4c5b6a79880011223344556677889900aa
src/Sync.cs:42"

profile="$(cat "$PJ/profile/profile.yaml")"
contains "the profile has review.login" "$profile" "review:
  login: fx-viewer"
contains "the profile has the file forge on github.com" "$profile" "forge: { adapter: file, hosts: [github.com] }"
contains "the profile lists acme/api under github repos" "$profile" "acme/api"
contains "the profile was approved" "$(cat "$STUB_LOG")" "typed=approve profile abcdef"

STATE="$PJ/ui-fixture-forge/state"
calls="$(cat "$FORGE_LOG")"
contains "open names the repo on github.com, the branch and the head sha" "$calls" "open --repo github.com/acme/api --head feat/retry-budget --base main --head-sha 1f2e3d4c5b6a79880011223344556677889900aa"
contains "seed files uses the number the forge gave" "$calls" "seed files https://github.com/acme/api/pull/7 --from"
contains "seed checks names the failing run" "$calls" "seed checks https://github.com/acme/api/pull/7 --state failing --failing lint"
contains "seed thread is on the changed file" "$calls" "seed thread https://github.com/acme/api/pull/7 --author reviewer-kai"
contains "every forge call uses the world inside the home" "$calls" "[state=$STATE adapter=file hosts=github.com login=fx-viewer]"
lacks "no forge call touches the real state dir" "$calls" "[state=$AW_STATE_DIR "
check "the forge world is inside the guarded home" "$(case "$STATE/" in "$PJ"/*) echo inside ;; *) echo outside ;; esac)" "inside"
check "the real state's cache/bin is linked into the forge state (serve finds Temporal there)" "$(readlink "$STATE/cache/bin")" "$AW_STATE_DIR/cache/bin"
check "the seeded file is src/Sync.cs with 3 added lines" "$(jq -r '.[0].file | "\(.path) \(.status) +\(.additions) -\(.deletions)"' "$FORGE_LOG.files")" "src/Sync.cs Modified +3 -0"
check "line 42 of the seeded text is an added line (on the new side)" "$(jq -r '.[0].text' "$FORGE_LOG.files" | sed -n 42p)" "    if (delay is null) return Retry.Stop;"
check "the base text lacks the added lines" "$(jq -r '.[0].baseText' "$FORGE_LOG.files" | grep -c 'budget.Next')" "0"

check "one open GitHub PR record with the forge's number" "$(sqlq "$PJDB" "SELECT key || ' ' || url || ' ' || author || ' ' || state || ' ' || audience_key FROM source_record WHERE source = 'github' AND kind = 'pr'")" \
  "github:pr/acme/api/7 https://github.com/acme/api/pull/7 fx-viewer open linear:SPL"
check "the record carries its pull facts (head sha)" "$(sqlq "$PJDB" "SELECT pull_facts FROM source_record WHERE key = 'github:pr/acme/api/7'")" \
  '{"requestedReviewers":["reviewer-kai"],"headSha":"1f2e3d4c5b6a79880011223344556677889900aa","reviews":[]}'
check "a github cursor row exists" "$(sqlq "$PJDB" "SELECT phase FROM source_cursor WHERE source = 'github' AND repo = 'records'")" "incremental"
check "the store epoch moved" "$(sqlq "$PJDB" "SELECT record_stamp FROM store_epoch WHERE id = 1")" "1"

stub_calls="$(cat "$STUB_LOG")"
contains "serve was restarted" "$stub_calls" "--headless"
contains "serve was stopped first" "$stub_calls" "stop [state="

# Idempotent: a repeat rebuilds the world and replaces the record.
rows="$(sqlq "$PJDB" "SELECT COUNT(*) FROM source_record")"
: > "$FORGE_LOG"
rev up >/dev/null 2>&1; check "a second reviews up exits 0" "$?" "0"
check "a second reviews up adds no record" "$(sqlq "$PJDB" "SELECT COUNT(*) FROM source_record")" "$rows"
check "a second reviews up opens the PR again (a new world)" "$(grep -c '^open ' "$FORGE_LOG")" "1"
check "a second reviews up leaves one review: and one forge: line" "$(grep -c '^review:' "$PJ/profile/profile.yaml") $(grep -c 'forge:' "$PJ/profile/profile.yaml")" "1 1"
check "a second reviews up lists each repo once" "$(grep -c 'acme/api' "$PJ/profile/profile.yaml")" "1"

# A later restart by another verb keeps the forge state while the folder exists.
: > "$STUB_LOG"
AW_SINDRI_HOME="$PJ" SINDRI="$STUB" bash -c 'source <(sed -n "/^sessions_export()/,/^}/p;/^reviews_state()/p;/^reviews_dir()/p;/^sessions_dir()/p;/^agent_dir()/p;/^sandbox_dir()/p;/^REVIEWS_DIRNAME=/p;/^SESSIONS_DIRNAME=/p;/^PROJECTS_VIEWER=/p" "$1"); sessions_export; echo "$AW_STATE_DIR $AW_FORGE_LOGIN"' _ "$FIXTURE" > "$WORK/export" 2>/dev/null
check "sessions_export keeps AW_STATE_DIR and AW_FORGE_LOGIN on the forge state" "$(cat "$WORK/export")" "$STATE fx-viewer"

# Guards.
AW_SINDRI_HOME="$PJ" SINDRI="$STUB" bash "$FIXTURE" reviews >/dev/null 2>&1; check "reviews with no argument exits 2" "$?" "2"
AW_SINDRI_HOME="$PJ" SINDRI="$STUB" bash "$FIXTURE" reviews sideways >/dev/null 2>&1; check "reviews with an unknown argument exits 2" "$?" "2"

# down.
: > "$STUB_LOG"
: > "$PJ/control.json"
rev down >/dev/null 2>&1; check "reviews down exits 0" "$?" "0"
check "down removes the PR record" "$(sqlq "$PJDB" "SELECT COUNT(*) FROM source_record WHERE source = 'github' AND kind = 'pr'")" "0"
check "down removes the forge world" "$([ -e "$PJ/ui-fixture-forge" ] && echo present || echo gone)" "gone"
profile="$(cat "$PJ/profile/profile.yaml")"
lacks "down removes review.login" "$profile" "review:"
lacks "down removes workflow.forge and the empty workflow:" "$profile" "workflow:"
check "down leaves the real state's cache/bin alone" "$([ -d "$AW_STATE_DIR/cache/bin" ] && echo present || echo gone)" "present"
contains "down restarts serve on the real state dir" "$(cat "$STUB_LOG")" "--headless [state=$AW_STATE_DIR]"

if [ "$fail" -eq 0 ]; then echo "all checks passed"; else echo "some checks FAILED"; fi
exit "$fail"
