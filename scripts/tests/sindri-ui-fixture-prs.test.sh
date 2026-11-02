#!/usr/bin/env bash
# Tests for the GitHub side of `scripts/sindri-ui-fixture.sh projects` (#1908): the live profile lists acme/api and acme/web under
# context.sources.github.repos, the context store holds three open PRs each linked from a Checkout rewrite issue, and a second run adds no row.
# Run: bash scripts/tests/sindri-ui-fixture-prs.test.sh. No dotnet and no real sindri: a stub SINDRI records its arguments.
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

WORK="$(mktemp -d)"
trap 'rm -r "$WORK"' EXIT
export TMPDIR="$WORK/tmp" AW_STATE_DIR="$WORK/state"
mkdir -p "$TMPDIR" "$AW_STATE_DIR" "$WORK/bin"

# sqlite3 CLI when there is one, else python3's module (the fixture has the same fallback).
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

# A stub SINDRI: records its arguments and answers the profile calls the fixture makes.
STUB="$WORK/bin/sindri-stub"
cat > "$STUB" <<'EOF'
#!/usr/bin/env bash
echo "$*" >> "$STUB_LOG"
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

PJ="$TMPDIR/proj"
mkdir -p "$PJ/profile" "$PJ/context"
printf 'schemaVersion: 1\nuser: fixture\nservices:\n  heartbeat: { every: 5s }\n' > "$PJ/profile/profile.yaml"
PJDB="$PJ/context/context.db"
proj() { AW_SINDRI_HOME="$PJ" SINDRI="$STUB" bash "$FIXTURE" projects "$@"; }
for m in 0001 0018 0019 0020 0022 0023 0029; do migrate "$PJDB" "$(ls "$ROOT"/src/Context/Aw.Context.Infrastructure/Store/Migrations/${m}_*.sql)"; done
out="$(proj 2>&1)"; check "projects exits 0 (the setup works; the checks below are about the PRs)" "$?" "0"

profile="$(cat "$PJ/profile/profile.yaml")"
contains "the live profile has a github source" "$profile" "github:"
contains "the live profile lists acme/api under context.sources.github.repos" "$profile" "acme/api"
contains "the live profile lists acme/web under context.sources.github.repos" "$profile" "acme/web"
check "three open GitHub PRs are stored" "$(sqlq "$PJDB" "SELECT GROUP_CONCAT(key) FROM (SELECT key FROM source_record WHERE source = 'github' AND kind = 'pr' AND state = 'open' ORDER BY key)")" \
  "github:pr/acme/api/41,github:pr/acme/api/42,github:pr/acme/web/17"
check "each PR has a relates link from a Checkout rewrite issue" "$(sqlq "$PJDB" "SELECT GROUP_CONCAT(to_key) FROM (SELECT to_key FROM record_link WHERE kind = 'relates' AND from_key IN ('linear:issue/fx-checkout-1', 'linear:issue/fx-checkout-2', 'linear:issue/fx-checkout-3') AND to_key LIKE 'github:pr/%' ORDER BY to_key)")" \
  "github:pr/acme/api/41,github:pr/acme/api/42,github:pr/acme/web/17"
check "those links start at three distinct Checkout rewrite issues" "$(sqlq "$PJDB" "SELECT COUNT(DISTINCT from_key) FROM record_link WHERE kind = 'relates' AND to_key LIKE 'github:pr/%' AND from_key LIKE 'linear:issue/fx-checkout-%'")" "3"

before="$(sqlq "$PJDB" "SELECT COUNT(*) FROM source_record") $(sqlq "$PJDB" "SELECT COUNT(*) FROM record_link")"
proj >/dev/null 2>&1; check "a second projects exits 0" "$?" "0"
check "a second projects adds no row" "$(sqlq "$PJDB" "SELECT COUNT(*) FROM source_record") $(sqlq "$PJDB" "SELECT COUNT(*) FROM record_link")" "$before"
check "a second projects lists each repo once" "$(grep -c 'acme/api' "$PJ/profile/profile.yaml")" "1"

if [ "$fail" -eq 0 ]; then echo "all checks passed"; else echo "some checks FAILED"; fi
exit "$fail"
