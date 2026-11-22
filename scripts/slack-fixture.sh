#!/usr/bin/env bash
# scripts/slack-fixture.sh — the Messages page's fake Slack (messages spec §8.1, phase M1a). It starts the fake Slack host
# (tests/Aw.Connectors.Testing.Server, the same FakeSlackServer the tests use, with SlackWorkspaceSeed's made-up workspace) on
# loopback, and makes a guarded temp Sindri home that reads it: the marker file, a fixture token in the file secret store, and the
# slack:messages connection row. No keychain tool is ever called, so the owner's real slack:messages token is never read, written
# or deleted.
#
#   slack-fixture.sh up [--port N] [--rate-limit <method>=<times>[:<seconds>]]... [--gap <conversation>=<count>]... [--storm]
#                                  build the host into <home>/bin when a source is newer, start it on 127.0.0.1 (port N, default a
#                                  free one; --rate-limit queues that many 429s for one method at start, Retry-After 30 s unless given;
#                                  --gap adds that many older messages to a seeded conversation; --storm is
#                                  --rate-limit conversations.history=20 --gap C0001GEN=12, the spec's "a gap and a 429 storm"), write <home>/sindri/.slack-fixture-home ("slack-fixture v1", then the port), the
#                                  fixture token as an owner-only file under <home>/sindri/secrets, and the connection row when
#                                  <home>/sindri/ledger.db has its connection table. Prints the answer key: url, port, the Sindri
#                                  home, the env to start Sindri with, and the seeded ids. A repeat up stops the old host first.
#   slack-fixture.sh seed          write the connection row again (run it after the control plane made ledger.db)
#   slack-fixture.sh status        the url of the host that is up (exit 1 when none)
#   slack-fixture.sh down          stop the host (its recorded pid only) and remove the marker, the token file and the connection
#                                  row; other entries of the home stay
#
# The home is SLACK_FIXTURE_HOME (default ${TMPDIR:-/tmp}/aw-slack-fixture-<uid>). The guard runs first for every verb: its real path
# must be strictly inside realpath(${TMPDIR:-/tmp}) or inside a proof folder, <AW_STATE_DIR or ~/.agentic-workflow>/<repo slug>/proof/…,
# and contain no '..'. Anything else exits 2, so the fixture never writes into the owner's real Sindri home.
# Environment: AW_SLACK_FAKE (the host's command line, split on spaces; default: a dotnet build of tests/Aw.Connectors.Testing.Server
# into <home>/bin, through the heavy wrapper (AW_HEAVY, default ~/.agentic-workflow/bin-r1b/heavy) when it exists, run as
# dotnet <home>/bin/Aw.Connectors.Testing.Server.dll). Needs sqlite3 or python3 for the connection row, and dotnet without AW_SLACK_FAKE.
# The token is built at run time from parts and starts with the fixture prefix the M1d client requires (xo + xp-fixture-).
# Exit: 0 done; 1 status found nothing up; 2 usage, the guard or a failed step.
# Tests: bash scripts/tests/slack-fixture.test.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MARKER=".slack-fixture-home"
MARKER_HEADER="slack-fixture v1"
CONNECTION="slack:messages"
SECRET_NAME="sindri-connection-slack.messages"
WORKSPACE="T0000FIX"
ME="U0000ME"
CREATED_BY="slack-fixture"

die() { echo "slack-fixture: $*" >&2; exit 2; }
usage() { awk 'NR >= 2 { if (/^#/) print; else exit }' "$0" | sed 's/^# \{0,1\}//'; }
need() { command -v "$1" >/dev/null 2>&1 || die "$1 is needed"; }

# --- the guard (the same rule as forge-fixture.sh) ---------------------------------------------------------------------------
real() { (cd "$1" 2>/dev/null && pwd -P); }
guard() {
  local tmp_root="${TMPDIR:-/tmp}"; tmp_root="${tmp_root%/}"
  HOME_DIR="${SLACK_FIXTURE_HOME:-${tmp_root}/aw-slack-fixture-$(id -u)}"
  local msg="SLACK_FIXTURE_HOME must be under \$TMPDIR or a proof folder, with no '..' (got ${HOME_DIR})"
  case "$HOME_DIR" in /*) ;; *) die "$msg" ;; esac
  case "$HOME_DIR/" in */../* | */./* | *//*) die "$msg" ;; esac
  local ancestor="$HOME_DIR" rest="" temp state resolved below slug
  while [ ! -d "$ancestor" ]; do rest="/$(basename "$ancestor")$rest"; ancestor="$(dirname "$ancestor")"; done
  resolved="$(real "$ancestor")$rest"
  temp="$(real "${TMPDIR:-/tmp}")" || die "$msg"
  SINDRI_HOME="$HOME_DIR/sindri"
  case "$resolved/" in
    "$temp"/?*) return 0 ;;
  esac
  state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
  if [ -d "$state" ]; then
    state="$(real "$state")"
    case "$resolved/" in
      "$state"/?*/proof/?*) below="${resolved#"$state"/}"; slug="${below%%/*}"; [ -n "$slug" ] && return 0 ;;
    esac
  fi
  die "$msg"
}

# --- the host command -----------------------------------------------------------------------------------------------------
HOST_CMD=()
ensure_bin() {
  if [ -n "${AW_SLACK_FAKE:-}" ]; then
    # shellcheck disable=SC2206
    HOST_CMD=($AW_SLACK_FAKE)
    return
  fi
  need dotnet
  local dll="$HOME_DIR/bin/Aw.Connectors.Testing.Server.dll" heavy="${AW_HEAVY:-$HOME/.agentic-workflow/bin-r1b/heavy}" runner=(dotnet)
  [ -f "$heavy" ] && runner=("$heavy" --light dotnet)
  if [ ! -f "$dll" ] || [ -n "$(find "$ROOT/tests/Aw.Connectors.Testing" "$ROOT/tests/Aw.Connectors.Testing.Server" "$ROOT/src/Connectors" -name '*.cs' -newer "$dll" -print -quit 2>/dev/null)" ]; then
    echo "slack-fixture: building the fake Slack host into $HOME_DIR/bin" >&2
    mkdir -p "$HOME_DIR/bin"
    "${runner[@]}" build "$ROOT/tests/Aw.Connectors.Testing.Server/Aw.Connectors.Testing.Server.csproj" -c Release -o "$HOME_DIR/bin" -v q --nologo >&2 \
      || die "dotnet build of the fake Slack host failed"
  fi
  HOST_CMD=(dotnet "$dll")
}

# --- the host process: started once, its pid recorded, stopped by that pid only ---------------------------------------------------
host_pid() { [ -f "$HOME_DIR/fake.pid" ] && cat "$HOME_DIR/fake.pid"; }
host_alive() { local pid; pid="$(host_pid)" && [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; }

stop_host() {
  local pid i
  if pid="$(host_pid)" && [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
    kill "$pid" 2>/dev/null || true
    for i in $(seq 1 50); do kill -0 "$pid" 2>/dev/null || break; sleep 0.1; done
    kill -0 "$pid" 2>/dev/null && kill -9 "$pid" 2>/dev/null || true
  fi
  rm -f "$HOME_DIR/fake.pid" "$HOME_DIR/fake.url"
}

start_host() { # <port> [host arguments…]
  local port="$1" url="" i
  shift
  : >"$HOME_DIR/fake.log"
  nohup "${HOST_CMD[@]}" --port "$port" "$@" </dev/null >"$HOME_DIR/fake.log" 2>&1 &
  echo "$!" >"$HOME_DIR/fake.pid"
  for i in $(seq 1 600); do
    url="$(sed -n 's/^listening \(http:\/\/127\.0\.0\.1:[0-9][0-9]*\/\)$/\1/p' "$HOME_DIR/fake.log" | head -1)"
    [ -n "$url" ] && break
    host_alive || { cat "$HOME_DIR/fake.log" >&2; rm -f "$HOME_DIR/fake.pid"; die "the fake Slack host exited before it listened"; }
    sleep 0.1
  done
  [ -n "$url" ] || { stop_host; die "the fake Slack host did not print its address within 60 s"; }
  echo "$url" >"$HOME_DIR/fake.url"
}

# --- the Sindri home: marker, token file, connection row -----------------------------------------------------------------------
token() { printf '%s%s%s' "xo" "xp-fixture-" "local-$(id -u)-0000"; }
b64url() { base64 | tr -d '\n=' | tr '+/' '-_'; }
secret_file() { echo "$SINDRI_HOME/secrets/$SECRET_NAME"; }

write_marker() { # <port>
  mkdir -p "$SINDRI_HOME"
  printf '%s\n%s\n' "$MARKER_HEADER" "$1" >"$SINDRI_HOME/$MARKER"
}

# The file secret store's layout (KeychainSecretWriter.ToFile, KeychainConnectionSecrets): <home>/secrets/sindri-connection-<service>.<label>,
# owner-only, holding the token set {"access": …} as JSON then base64url. The row points at it with file:, so nothing reads a keychain.
write_secret() {
  local file; file="$(secret_file)"
  mkdir -p "$SINDRI_HOME/secrets"; chmod 700 "$SINDRI_HOME/secrets"
  (umask 077; printf '{"access":"%s"}' "$(token)" | b64url >"$file.tmp")
  mv "$file.tmp" "$file"; chmod 600 "$file"
}

use_cli() { command -v sqlite3 >/dev/null 2>&1; }
sql_on() { # <db> <statements>
  if use_cli; then sqlite3 -bail "$1" "$2"; return; fi
  command -v python3 >/dev/null 2>&1 || die "sqlite3 (or python3) is needed for the connection row"
  python3 -I -c 'import sqlite3, sys
con = sqlite3.connect(sys.argv[1], isolation_level=None)
if sys.argv[2].lstrip().upper().startswith("SELECT"):
    for row in con.execute(sys.argv[2]):
        print("|".join(str(c) for c in row))
else:
    con.executescript(sys.argv[2])
' "$1" "$2"
}
has_table() { [ -f "$SINDRI_HOME/ledger.db" ] && [ "$(sql_on "$SINDRI_HOME/ledger.db" "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'connection';")" = 1 ]; }

seed_row() { # -> 0 written, 1 no ledger table yet
  has_table || return 1
  local now config
  now="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  config="{\"workspace\":\"$WORKSPACE\",\"user\":\"$ME\",\"probeUrl\":\"https://slack.com/api/auth.test\"}"
  sql_on "$SINDRI_HOME/ledger.db" "INSERT OR REPLACE INTO connection (name, kind, preset, config_json, secret_pointer, status, status_reason, created_by, created_at, updated_at, last_refreshed_at) VALUES ('$CONNECTION', 'rest', NULL, '$config', 'file:$(secret_file)', 'connected', NULL, '$CREATED_BY', '$now', '$now', NULL);"
}

remove_row() {
  has_table || return 0
  sql_on "$SINDRI_HOME/ledger.db" "DELETE FROM connection WHERE name = '$CONNECTION' AND created_by = '$CREATED_BY';"
}

print_key() {
  local url; url="$(cat "$HOME_DIR/fake.url")"
  echo "url: $url"
  echo "port: $(sed -n 's/.*:\([0-9][0-9]*\)\/$/\1/p' <<<"$url")"
  echo "sindri-home: $SINDRI_HOME"
  echo "env: AW_SINDRI_HOME=$SINDRI_HOME AW_SLACK_BASE_URL=${url}api/"
  echo "connection: $CONNECTION (token file $(secret_file))"
  echo "seeded: me $ME, dm D0001PAT with U0001PAT (newest message waits on me), group dm G0001MPD, #general C0001GEN (a mention and a thread with a reply after mine), #ops-rota C0002OPS"
}

# --- verbs ---------------------------------------------------------------------------------------------------------------------------
verb="${1:-}"
case "$verb" in
  "") usage >&2; exit 2 ;;
  -h | --help) usage; exit 0 ;;
esac
shift

case "$verb" in
  up)
    port=0
    SCRIPTED=()
    while [ $# -gt 0 ]; do
      case "$1" in
        --port) [[ "${2:-}" =~ ^[0-9]+$ ]] && [ "$2" -le 65535 ] || die "--port needs a number from 0 to 65535"; port="$2"; shift 2 ;;
        --rate-limit)
          [[ "${2:-}" =~ ^[a-z]+\.[A-Za-z.]+=[1-9][0-9]*(:[1-9][0-9]*)?$ ]] || die "--rate-limit needs <method>=<times>[:<seconds>], got '${2:-}'"
          SCRIPTED+=(--rate-limit "$2"); shift 2 ;;
        --gap)
          [[ "${2:-}" =~ ^[A-Z][A-Z0-9]+=[1-9][0-9]*$ ]] || die "--gap needs <conversation>=<count>, got '${2:-}'"
          SCRIPTED+=(--gap "$2"); shift 2 ;;
        --storm) SCRIPTED+=(--rate-limit conversations.history=20 --gap C0001GEN=12); shift ;;
        *) die "up takes --port N, --rate-limit, --gap or --storm, got '$1'" ;;
      esac
    done
    guard; mkdir -p "$HOME_DIR"; ensure_bin
    stop_host
    start_host "$port" ${SCRIPTED[@]+"${SCRIPTED[@]}"}
    url="$(cat "$HOME_DIR/fake.url")"
    write_marker "$(sed -n 's/.*:\([0-9][0-9]*\)\/$/\1/p' <<<"$url")"
    write_secret
    if seed_row; then echo "row: $CONNECTION written to $SINDRI_HOME/ledger.db"; else
      echo "row: no ledger yet; start Sindri with the env below, then run: slack-fixture.sh seed"
    fi
    print_key
    [ ${#SCRIPTED[@]} -eq 0 ] || echo "scripted: ${SCRIPTED[*]}"
    ;;
  seed)
    [ $# -eq 0 ] || die "seed takes no arguments"
    guard
    [ -f "$SINDRI_HOME/$MARKER" ] || die "nothing is up in $HOME_DIR: run 'up' first"
    seed_row || die "no connection table in $SINDRI_HOME/ledger.db yet: start Sindri with AW_SINDRI_HOME=$SINDRI_HOME first"
    echo "row: $CONNECTION written to $SINDRI_HOME/ledger.db"
    ;;
  status)
    [ $# -eq 0 ] || die "status takes no arguments"
    guard
    if host_alive && [ -f "$HOME_DIR/fake.url" ]; then echo "up: $(cat "$HOME_DIR/fake.url")"; else echo "nothing is up in $HOME_DIR"; exit 1; fi
    ;;
  down)
    [ $# -eq 0 ] || die "down takes no arguments"
    guard
    if [ ! -d "$HOME_DIR" ]; then echo "nothing to remove"; exit 0; fi
    stop_host
    remove_row
    rm -f "$SINDRI_HOME/$MARKER" "$(secret_file)" "$(secret_file).tmp"
    echo "removed"
    ;;
  *) die "unknown verb: $verb" ;;
esac
