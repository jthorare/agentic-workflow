#!/usr/bin/env bash
# Tests for scripts/slack-fixture.sh (messages spec §8.1, M1a). Run: bash scripts/tests/slack-fixture.test.sh
# No dotnet build: AW_SLACK_FAKE points at a stub host (python3 binds 127.0.0.1 on a free port and prints the host's "listening" line),
# a stub dotnet fails the run if it is called, and stub keychain tools (security, secret-tool) first on PATH record any call.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
FIXTURE="$ROOT/scripts/slack-fixture.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
contains() { case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in:      $2"; fail=1 ;; esac; }
lacks() { case "$2" in *"$3"*) echo "not ok - $1"; echo "  found: $3"; fail=1 ;; *) echo "ok - $1" ;; esac; }

command -v python3 >/dev/null 2>&1 || { echo "not ok - python3 is needed (the stub host)"; exit 1; }

WORK="$(mktemp -d)"
H="$WORK/tmp/slack-fixture"
S="$H/sindri"
cleanup() { SLACK_FIXTURE_HOME="$H" bash "$FIXTURE" down >/dev/null 2>&1; rm -r "$WORK"; }
trap cleanup EXIT
export TMPDIR="$WORK/tmp" AW_STATE_DIR="$WORK/state" AW_HEAVY="$WORK/no-heavy"
mkdir -p "$TMPDIR" "$AW_STATE_DIR" "$WORK/bin"
export SLACK_FIXTURE_HOME="$H"
unset AW_SINDRI_HOME AW_SLACK_BASE_URL

# --- stubs: the keychain tools and dotnet record any call; the host binds loopback for real ---
for tool in security secret-tool dotnet; do
  printf '#!/usr/bin/env bash\necho "%s $*" >>"%s/calls.log"\nexit 1\n' "$tool" "$WORK" >"$WORK/bin/$tool"
  chmod +x "$WORK/bin/$tool"
done
cat >"$WORK/bin/fake-slack-host" <<EOF
#!/usr/bin/env bash
echo "\$*" >>"$WORK/host-args.log"
port=0; [ "\${1:-}" = --port ] && port="\$2"
exec python3 -I -c 'import socket, sys, time
s = socket.socket(); s.bind(("127.0.0.1", int(sys.argv[1]))); s.listen()
print("listening http://127.0.0.1:%d/" % s.getsockname()[1], flush=True)
time.sleep(600)' "\$port"
EOF
chmod +x "$WORK/bin/fake-slack-host"
export PATH="$WORK/bin:$PATH"
export AW_SLACK_FAKE="$WORK/bin/fake-slack-host"
TOKEN_PREFIX="xo""xp-fixture-"

sql() { python3 -I -c 'import sqlite3, sys
con = sqlite3.connect(sys.argv[1], isolation_level=None)
if sys.argv[2].lstrip().upper().startswith("SELECT"):
    for row in con.execute(sys.argv[2]):
        print("|".join("" if c is None else str(c) for c in row))
else:
    con.executescript(sys.argv[2])' "$1" "$2"; }
pid_alive() { kill -0 "$1" 2>/dev/null; }

# --- usage and the guard (nothing starts) ---
out="$(bash "$FIXTURE" 2>&1)"; check "no verb exits 2" "$?" "2"
contains "no verb prints usage" "$out" "slack-fixture.sh up"
bash "$FIXTURE" --help >/dev/null 2>&1; check "--help exits 0" "$?" "0"
out="$(bash "$FIXTURE" sideways 2>&1)"; check "an unknown verb exits 2" "$?" "2"
contains "an unknown verb is named" "$out" "unknown verb: sideways"
out="$(bash "$FIXTURE" up --port nope 2>&1)"; check "a bad --port exits 2" "$?" "2"
out="$(SLACK_FIXTURE_HOME=/etc/slack-fixture bash "$FIXTURE" up 2>&1)"; check "a home outside a temp folder is refused" "$?" "2"
contains "the guard names the rule" "$out" "must be under"
out="$(SLACK_FIXTURE_HOME="$TMPDIR/../elsewhere" bash "$FIXTURE" up 2>&1)"; check "a dotdot home is refused" "$?" "2"
out="$(SLACK_FIXTURE_HOME=relative/home bash "$FIXTURE" down 2>&1)"; check "a relative home is refused" "$?" "2"
ln -s "$WORK/outside" "$TMPDIR/link-out"; mkdir -p "$WORK/outside"
out="$(SLACK_FIXTURE_HOME="$TMPDIR/link-out/home" bash "$FIXTURE" up 2>&1)"; check "a home reached through a link out of the temp folder is refused" "$?" "2"
check "a refused home is never made" "$([ -e "$WORK/outside/home" ] && echo made || echo absent)" "absent"
check "no host started for a refused home" "$([ -f "$WORK/host-args.log" ] && echo started || echo none)" "none"
bash "$FIXTURE" status >/dev/null 2>&1; check "status exits 1 when nothing is up" "$?" "1"

# --- up before any ledger: the host, the marker and the token file ---
out="$(bash "$FIXTURE" up 2>&1)"; check "up exits 0" "$?" "0"
url="$(sed -n 's/^url: //p' <<<"$out")"
port="$(sed -n 's/^port: //p' <<<"$out")"
contains "the url is loopback" "$url" "http://127.0.0.1:"
check "the url names the port" "$url" "http://127.0.0.1:$port/"
contains "up says the row waits for the ledger" "$out" "row: no ledger yet"
contains "up prints the Sindri home" "$out" "sindri-home: $S"
contains "up prints the base url env" "$out" "AW_SLACK_BASE_URL=${url}api/"
lacks "up never prints the token" "$out" "$TOKEN_PREFIX"
check "the host was asked for a free port" "$(head -1 "$WORK/host-args.log")" "--port 0"
pid1="$(cat "$H/fake.pid")"
check "the host is running" "$(pid_alive "$pid1" && echo alive)" "alive"
check "the marker's first line is the header" "$(sed -n 1p "$S/.slack-fixture-home")" "slack-fixture v1"
check "the marker's second line is the port" "$(sed -n 2p "$S/.slack-fixture-home")" "$port"
secret="$S/secrets/sindri-connection-slack.messages"
check "the token file is owner-only" "$(stat -c %a "$secret" 2>/dev/null || stat -f %Lp "$secret")" "600"
decoded="$(python3 -I -c 'import base64, sys; t = open(sys.argv[1]).read().strip(); print(base64.urlsafe_b64decode(t + "=" * (-len(t) % 4)).decode())' "$secret")"
contains "the token file holds a fixture token set" "$decoded" "{\"access\":\"$TOKEN_PREFIX"
out="$(bash "$FIXTURE" status 2>&1)"; check "status exits 0 when up" "$?" "0"
check "status prints the url" "$out" "up: $url"
out="$(bash "$FIXTURE" seed 2>&1)"; check "seed before the ledger exists exits 2" "$?" "2"

# --- the connection row, once the control plane made the table (here: the migration's own statement) ---
sql "$S/ledger.db" "$(cat "$ROOT/src/Sindri/Aw.Sindri.Infrastructure/Ledger/Migrations/0010_connections.sql")
INSERT INTO connection (name, kind, preset, config_json, secret_pointer, status, status_reason, created_by, created_at, updated_at, last_refreshed_at)
VALUES ('github:other', 'oauth', 'github', '{}', 'keychain:sindri-connection/github.other', 'connected', NULL, 'owner', 'x', 'x', NULL);"
out="$(bash "$FIXTURE" seed 2>&1)"; check "seed exits 0" "$?" "0"
row="$(sql "$S/ledger.db" "SELECT kind, preset, secret_pointer, status, created_by FROM connection WHERE name = 'slack:messages';")"
check "the row is a hand-made rest connection on the token file" "$row" "rest||file:$secret|connected|slack-fixture"
config="$(sql "$S/ledger.db" "SELECT config_json FROM connection WHERE name = 'slack:messages';")"
check "the row's config names the seeded workspace and me" "$(python3 -I -c 'import json, sys; c = json.loads(sys.argv[1]); print(c["workspace"], c["user"], c["probeUrl"])' "$config")" "T0000FIX U0000ME https://slack.com/api/auth.test"

# --- a repeat up replaces the host and keeps one row ---
out="$(bash "$FIXTURE" up 2>&1)"; check "a repeat up exits 0" "$?" "0"
contains "a repeat up writes the row" "$out" "row: slack:messages written"
pid2="$(cat "$H/fake.pid")"
check "the old host is gone" "$(pid_alive "$pid1" && echo alive || echo gone)" "gone"
check "the new host is running" "$(pid_alive "$pid2" && echo alive)" "alive"
check "one slack:messages row" "$(sql "$S/ledger.db" "SELECT COUNT(*) FROM connection WHERE name = 'slack:messages';")" "1"
check "the marker follows the new port" "$(sed -n 2p "$S/.slack-fixture-home")" "$(sed -n 's/^port: //p' <<<"$out")"

# --- down removes the host, the marker, the token file and the row, and nothing else ---
echo keep >"$H/notes.txt"
out="$(bash "$FIXTURE" down 2>&1)"; check "down exits 0" "$?" "0"
check "down stops the host" "$(pid_alive "$pid2" && echo alive || echo gone)" "gone"
check "down removes the marker" "$([ -e "$S/.slack-fixture-home" ] && echo there || echo gone)" "gone"
check "down removes the token file" "$([ -e "$secret" ] && echo there || echo gone)" "gone"
check "down removes the row" "$(sql "$S/ledger.db" "SELECT COUNT(*) FROM connection WHERE name = 'slack:messages';")" "0"
check "down keeps another connection" "$(sql "$S/ledger.db" "SELECT COUNT(*) FROM connection WHERE name = 'github:other';")" "1"
check "down keeps other files of the home" "$(cat "$H/notes.txt")" "keep"
bash "$FIXTURE" status >/dev/null 2>&1; check "status exits 1 after down" "$?" "1"
bash "$FIXTURE" down >/dev/null 2>&1; check "a second down exits 0" "$?" "0"

# --- Fixture_seed_never_touches_the_keychain (and never builds) ---
calls="$(cat "$WORK/calls.log" 2>/dev/null)"
check "Fixture_seed_never_touches_the_keychain" "$(grep -cE '^(security|secret-tool) ' <<<"$calls")" "0"
check "no dotnet call with AW_SLACK_FAKE set" "$(grep -c '^dotnet ' <<<"$calls")" "0"

exit "$fail"
