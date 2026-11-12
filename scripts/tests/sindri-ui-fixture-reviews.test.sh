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
    echo "{\"valid\":true,\"hash\":\"abcdef0123456789\"}" ;;
esac
exit 0
EOF
chmod +x "$STUB"
export STUB_LOG="$WORK/calls"
: > "$STUB_LOG"

# A stub aw-forge-fake (AW_FORGE_FAKE): records the arguments with the environment the fixture gives it, hands out pull numbers 7, 8, ... (one more per
# open in the log) and keeps the first files seed.
FORGE="$WORK/bin/forge-stub"
cat > "$FORGE" <<'EOF'
#!/usr/bin/env bash
echo "$* [state=$AW_STATE_DIR adapter=$AW_FORGE_ADAPTER hosts=$AW_FORGE_HOSTS login=$AW_FORGE_LOGIN]" >> "$FORGE_LOG"
case "$1 $2" in
  "open --repo") n=$((6 + $(grep -c '^open ' "$FORGE_LOG"))); echo "{\"number\":$n,\"link\":\"https://github.com/acme/api/pull/$n\"}" ;;
  "seed files") while [ $# -gt 0 ]; do [ "$1" != --from ] || [ -e "$FORGE_LOG.files" ] || cp "$2" "$FORGE_LOG.files"; shift; done; echo '{"ok":true}' ;;
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
# #2619: GET /sindri/reviews answers "Connect GitHub" (and no rows) while there is no GitHub connection and no profile token (ReviewConnectRules.For).
# This stub suite has no serve to ask, so it checks the input the rule reads: context.sources.github.token is a non-empty pointer. The pointer names an
# env var nothing sets and no tick runs in the fixture, so the github cursor's last_error stays NULL (no AW-CONNECTORS-006/-007 sign-in code).
contains "the profile's github source has a token pointer, so Reviews reads the seeded PR instead of Connect GitHub" "$profile" 'github: { repos: [acme/api, acme/web], token: "env:FIXTURE_GITHUB_TOKEN" }'
check "the github cursor row has no error code (no sign-in code turns the connect panel back on)" "$(sqlq "$PJDB" "SELECT COALESCE(last_error, 'none') FROM source_cursor WHERE source = 'github' AND repo = 'records'")" "none"
lacks "no profile approve is run (edits apply on save)" "$(cat "$STUB_LOG")" "profile approve"

STATE="$PJ/ui-fixture-forge/state"
calls="$(cat "$FORGE_LOG")"
contains "open names the repo on github.com, the branch and the head sha" "$calls" "open --repo github.com/acme/api --head feat/retry-budget --base main --head-sha 1f2e3d4c5b6a79880011223344556677889900aa"
contains "seed files uses the number the forge gave" "$calls" "seed files https://github.com/acme/api/pull/7 --from"
contains "seed checks names the failing run" "$calls" "seed checks https://github.com/acme/api/pull/7 --state failing --failing lint"
contains "seed thread is on the changed file" "$calls" "seed thread https://github.com/acme/api/pull/7 --author reviewer-kai"
contains "every forge call uses the world inside the home" "$calls" "[state=$STATE adapter=file hosts=github.com login=fx-viewer]"
lacks "no forge call touches the real state dir" "$calls" "[state=$AW_STATE_DIR "
check "the forge world is inside the guarded home" "$(case "$STATE/" in ("$PJ"/*) echo inside ;; (*) echo outside ;; esac)" "inside"
check "the real state's cache/bin is linked into the forge state (serve finds Temporal there)" "$(readlink "$STATE/cache/bin")" "$AW_STATE_DIR/cache/bin"
check "the seeded file is src/Sync.cs with 3 added lines" "$(jq -r '.[0].file | "\(.path) \(.status) +\(.additions) -\(.deletions)"' "$FORGE_LOG.files")" "src/Sync.cs Modified +3 -0"
check "line 42 of the seeded text is an added line (on the new side)" "$(jq -r '.[0].text' "$FORGE_LOG.files" | sed -n 42p)" "    if (delay is null) return Retry.Stop;"
check "the base text lacks the added lines" "$(jq -r '.[0].baseText' "$FORGE_LOG.files" | grep -c 'budget.Next')" "0"

check "the viewer's PR and 3 by other authors, each with the forge's number" "$(sqlq "$PJDB" "SELECT key || ' ' || url || ' ' || author || ' ' || state || ' ' || audience_key FROM source_record WHERE source = 'github' AND kind = 'pr' ORDER BY key")" \
  "github:pr/acme/api/10 https://github.com/acme/api/pull/10 lee-park open linear:SPL
github:pr/acme/api/7 https://github.com/acme/api/pull/7 fx-viewer open linear:SPL
github:pr/acme/api/8 https://github.com/acme/api/pull/8 dana-okafor open linear:SPL
github:pr/acme/api/9 https://github.com/acme/api/pull/9 kai-reviewer open linear:SPL"
check "the other authors' PRs request the viewer's review" "$(sqlq "$PJDB" "SELECT COUNT(*) FROM source_record WHERE key IN ('github:pr/acme/api/8', 'github:pr/acme/api/9', 'github:pr/acme/api/10') AND pull_facts LIKE '%\"requestedReviewers\":[\"fx-viewer\"]%'")" "3"
contains "another author's PR is opened as that author" "$calls" "open --repo github.com/acme/api --head feat/rate-limit-headers --base main --head-sha 2a3b4c5d6e7f8091a2b3c4d5e6f708192a3b4c5d --title Add rate limit headers to v3 list endpoints --body A fixture PR by dana-okafor. [state=$STATE adapter=file hosts=github.com login=dana-okafor]"
contains "lee-park's PR has its test check failing" "$calls" "seed checks https://github.com/acme/api/pull/10 --state failing --failing test"
check "every other author's PR gets a files seed" "$(ls "$PJ/ui-fixture-forge"/files-*.json | wc -l | tr -d ' ')" "3"
check "the record carries its pull facts (head sha)" "$(sqlq "$PJDB" "SELECT pull_facts FROM source_record WHERE key = 'github:pr/acme/api/7'")" \
  '{"requestedReviewers":["reviewer-kai"],"headSha":"1f2e3d4c5b6a79880011223344556677889900aa","reviews":[]}'
# why: source_record.repo is a profile repo name (ContextRepoName) or NULL; the slug 'acme/api' made every read of the row throw (SND-CLI-003 on the page)
check "the record has no profile repo (repo is NULL, as GitHub sync stores it)" "$(sqlq "$PJDB" "SELECT COUNT(*) FROM source_record WHERE key = 'github:pr/acme/api/7' AND repo IS NULL")" "1"
check "a github cursor row exists" "$(sqlq "$PJDB" "SELECT phase FROM source_cursor WHERE source = 'github' AND repo = 'records'")" "incremental"
check "the store epoch moved" "$(sqlq "$PJDB" "SELECT record_stamp FROM store_epoch WHERE id = 1")" "2"

stub_calls="$(cat "$STUB_LOG")"
contains "serve was restarted" "$stub_calls" "--headless"
contains "serve was stopped first" "$stub_calls" "stop [state="

# Idempotent: a repeat rebuilds the world and replaces the record.
rows="$(sqlq "$PJDB" "SELECT COUNT(*) FROM source_record")"
: > "$FORGE_LOG"
rev up >/dev/null 2>&1; check "a second reviews up exits 0" "$?" "0"
check "a second reviews up adds no record" "$(sqlq "$PJDB" "SELECT COUNT(*) FROM source_record")" "$rows"
check "a second reviews up opens the 4 PRs again (a new world)" "$(grep -c '^open ' "$FORGE_LOG")" "4"
check "a second reviews up leaves one review: and one forge: line" "$(grep -c '^review:' "$PJ/profile/profile.yaml") $(grep -c 'forge:' "$PJ/profile/profile.yaml")" "1 1"
check "a second reviews up lists each repo once" "$(grep -c 'acme/api' "$PJ/profile/profile.yaml")" "1"

# A later restart by another verb keeps the forge state while the folder exists.
: > "$STUB_LOG"
AW_SINDRI_HOME="$PJ" SINDRI="$STUB" bash -c 'eval "$(sed -n "/^sessions_export()/,/^}/p;/^reviews_state()/p;/^reviews_dir()/p;/^sessions_dir()/p;/^agent_dir()/p;/^sandbox_dir()/p;/^REVIEWS_DIRNAME=/p;/^SESSIONS_DIRNAME=/p;/^PROJECTS_VIEWER=/p" "$1")"; sessions_export; echo "$AW_STATE_DIR $AW_FORGE_LOGIN"' _ "$FIXTURE" > "$WORK/export" 2>/dev/null
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
lacks "down removes the github token pointer up added" "$profile" "FIXTURE_GITHUB_TOKEN"
contains "down leaves the github repos" "$profile" "repos: [acme/api, acme/web]"
lacks "down removes review.login" "$profile" "review:"
lacks "down removes workflow.forge and the empty workflow:" "$profile" "workflow:"
check "down leaves the real state's cache/bin alone" "$([ -d "$AW_STATE_DIR/cache/bin" ] && echo present || echo gone)" "present"
contains "down restarts serve on the real state dir" "$(cat "$STUB_LOG")" "--headless [state=$AW_STATE_DIR]"
check "down removes the github cursor row up added" "$(sqlq "$PJDB" "SELECT COUNT(*) FROM source_cursor WHERE source = 'github' AND repo = 'records'")" "0"
check "down removes its marker" "$([ -e "$PJ/ui-fixture-reviews-added" ] && echo present || echo gone)" "gone"

# A review: block that was there before up stays after down (down removes only what up added).
printf 'schemaVersion: 1\nuser: fixture\nservices:\n  heartbeat: { every: 5s }\nreview:\n  login: fx-viewer\n' > "$PJ/profile/profile.yaml"
rev up >/dev/null 2>&1; check "up with a review: block already there exits 0" "$?" "0"
rev down >/dev/null 2>&1; check "down after that exits 0" "$?" "0"
contains "down keeps the review: block up did not add" "$(cat "$PJ/profile/profile.yaml")" "login: fx-viewer"
lacks "down still removes the forge up added" "$(cat "$PJ/profile/profile.yaml")" "forge:"

# After another verb's forge.test-only forge is in the profile, up can't reach the github.com PR: it refuses and says why.
printf 'schemaVersion: 1\nuser: fixture\nservices:\n  heartbeat: { every: 5s }\nworkflow:\n  forge: { adapter: file, hosts: [forge.test] }\n' > "$PJ/profile/profile.yaml"
: > "$FORGE_LOG"
rev up >/dev/null 2>"$WORK/err-forge"; check "up with a forge.test-only forge exits 2" "$?" "2"
contains "the refusal says the forge is without github.com" "$(cat "$WORK/err-forge")" "without github.com"
contains "the refusal names the forge line" "$(cat "$WORK/err-forge")" "hosts: [forge.test]"
check "the refusal opens no forge PR" "$(grep -c '^open ' "$FORGE_LOG")" "0"
contains "the refusal leaves the forge.test forge in place" "$(cat "$PJ/profile/profile.yaml")" "hosts: [forge.test]"

if [ "$fail" -eq 0 ]; then echo "all checks passed"; else echo "some checks FAILED"; fi
exit "$fail"
