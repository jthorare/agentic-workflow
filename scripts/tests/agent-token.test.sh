#!/usr/bin/env bash
# Tests for scripts/agent-token.sh. Run: bash scripts/tests/agent-token.test.sh
# Uses a fixture RSA key generated here and a fake GitHub endpoint
# (fixtures/agent-token/fake-github.py) — never the real App key or API.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
SCRIPT="$ROOT/scripts/agent-token.sh"
FAKE="$DIR/fixtures/agent-token/fake-github.py"
for c in python3 openssl jq curl xxd; do
  command -v "$c" >/dev/null || { echo "not ok - $c is required by this test"; exit 1; }
done
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
SERVER_PID=""
stop_server() {
  if [ -n "$SERVER_PID" ]; then kill "$SERVER_PID" 2>/dev/null || true; wait "$SERVER_PID" 2>/dev/null || true; fi
  SERVER_PID=""
}
trap 'stop_server; rm -rf "$WORK"' EXIT

openssl genrsa -out "$WORK/key.pem" 2048 2>/dev/null
openssl rsa -in "$WORK/key.pem" -pubout -out "$WORK/pub.pem" 2>/dev/null
KEY_LINE="$(sed -n 2p "$WORK/key.pem")"   # a line of key material to grep for
CANNED="ghs_fixtureToken0123456789abcdefABCDEF"
LEAK="ghs_leakyBodyToken9876543210zyxwvuZYXW"

# run_tty <outfile> <cmd...>: run cmd with stdout+stderr on a fresh pty (stdin
# /dev/null), copy what it printed to <outfile>, return its exit code. Unlike
# script(1), it waits for the child however stdin behaves.
run_tty() {
  python3 - "$@" <<'PY'
import os, subprocess, sys
out, cmd = sys.argv[1], sys.argv[2:]
m, s = os.openpty()
p = subprocess.Popen(cmd, stdin=subprocess.DEVNULL, stdout=s, stderr=s)
os.close(s)
buf = b""
while True:
    try:
        chunk = os.read(m, 4096)
    except OSError:
        break
    if not chunk:
        break
    buf += chunk
open(out, "wb").write(buf)
sys.exit(p.wait())
PY
}

start_server() { # $1 = mode (ok|err)
  stop_server
  SRV="$WORK/srv-$1-$RANDOM"; mkdir -p "$SRV"
  python3 "$FAKE" "$SRV" "$WORK/pub.pem" "$1" 2>"$SRV/stderr" &
  SERVER_PID=$!
  # Up to 30 s: a cold CI runner's first python3 start can take several seconds.
  for _ in $(seq 1 300); do
    [ -s "$SRV/port" ] && break
    kill -0 "$SERVER_PID" 2>/dev/null || break
    sleep 0.1
  done
  if ! [ -s "$SRV/port" ] || ! kill -0 "$SERVER_PID" 2>/dev/null; then
    echo "not ok - fake server ($1) did not start"
    sed 's/^/# server stderr: /' "$SRV/stderr" 2>/dev/null
    exit 1
  fi
  export AGENT_APP_API_URL="http://127.0.0.1:$(cat "$SRV/port")"
}

export AGENT_APP_KEY_FILE="$WORK/key.pem"
export TMPDIR="$WORK/tmp"; mkdir -p "$TMPDIR"

# --- happy path: token on stdout only, JWT valid ---
start_server ok
rc=0; out="$(bash "$SCRIPT" 2>"$WORK/err")" || rc=$?
check "exits 0" "$rc" "0"
check "stdout is exactly the token" "$out" "$CANNED"
check "stderr is empty without --verbose" "$(cat "$WORK/err")" ""
req="$(head -1 "$SRV/requests.jsonl")"
check "POSTs the default installation" "$(jq -r .path <<<"$req")" "/app/installations/169843852/access_tokens"
check "asks for the v3 JSON media type" "$(jq -r .accept <<<"$req")" "application/vnd.github+json"
check "JWT header is RS256" "$(jq -c .header <<<"$req")" '{"alg":"RS256","typ":"JWT"}'
check "JWT iss is the default App id" "$(jq -r .claims.iss <<<"$req")" "5259848"
check "JWT lifetime is 600s (iat-60 .. exp+540)" "$(jq '.claims.exp - .claims.iat' <<<"$req")" "600"
now="$(date +%s)"; iat="$(jq .claims.iat <<<"$req")"
check "JWT iat is ~60s in the past" "$(( now - iat >= 55 && now - iat <= 75 ))" "1"
check "JWT signature verifies with the public key" "$(jq -r .verify <<<"$req")" "Verified OK"
check "no request body without --repo" "$(jq -r .body <<<"$req")" ""
check "Authorization header never in any process argv" "$(jq -r .argv_has_bearer <<<"$req")" "false"
check "JWT never in any process argv" "$(jq -r .argv_has_jwt <<<"$req")" "false"
check "argv snapshot is non-empty (the argv checks are real)" "$(jq -r .argv_seen <<<"$req")" "true"
check "private temp dir removed after the run" "$(find "$TMPDIR" -mindepth 1 | wc -l | tr -d ' ')" "0"

# --- flags: --app-id, --installation, --repo, --verbose ---
rc=0; out="$(bash "$SCRIPT" --app-id 42 --installation 7 --repo agentic-workflow --verbose 2>"$WORK/err")" || rc=$?
req="$(tail -1 "$SRV/requests.jsonl")"
check "--verbose: exits 0" "$rc" "0"
check "--verbose: stdout is still only the token" "$out" "$CANNED"
check "--verbose: expiry on stderr" "$(cat "$WORK/err")" "agent-token: expires_at 2099-01-01T00:00:00Z"
check "--installation picks the path" "$(jq -r .path <<<"$req")" "/app/installations/7/access_tokens"
check "--app-id sets iss" "$(jq -r .claims.iss <<<"$req")" "42"
check "--repo narrows the token" "$(jq -c '.body|fromjson' <<<"$req")" '{"repositories":["agentic-workflow"]}'

# --- env overrides for ids ---
rc=0; AGENT_APP_ID=99 AGENT_APP_INSTALLATION_ID=11 bash "$SCRIPT" >/dev/null 2>"$WORK/err" || rc=$?
check "env ids: exits 0" "$rc" "0"
req="$(tail -1 "$SRV/requests.jsonl")"
check "AGENT_APP_INSTALLATION_ID env picks the path" "$(jq -r .path <<<"$req")" "/app/installations/11/access_tokens"
check "AGENT_APP_ID env sets iss" "$(jq -r .claims.iss <<<"$req")" "99"

# --- refuses a terminal on stdout ---
rc=0; run_tty "$WORK/tty.out" bash "$SCRIPT" || rc=$?
check "TTY stdout: exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
check "TTY stdout: says why" "$(grep -c 'refusing to print a token to a terminal' "$WORK/tty.out")" "1"
check "TTY stdout: token never reaches the terminal" "$(grep -c "$CANNED" "$WORK/tty.out" || true)" "0"
n_before="$(wc -l <"$SRV/requests.jsonl" | tr -d ' ')"
check "TTY stdout: refused before calling the API" "$n_before" "3"
run_tty "$WORK/tty2.out" bash "$SCRIPT" --force-tty || true
check "--force-tty prints to the terminal" "$(grep -c "$CANNED" "$WORK/tty2.out")" "1"

# --- API error: status + message only, body redacted ---
start_server err
rc=0; out="$(bash "$SCRIPT" 2>"$WORK/err")" || rc=$?
check "API 401: exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
check "API 401: stdout empty" "$out" ""
check "API 401: stderr names status and message" "$(cat "$WORK/err")" "agent-token: GitHub returned HTTP 401: Bad credentials"
check "API 401: token-shaped body value not echoed" "$(grep -c "$LEAK" "$WORK/err" || true)" "0"

# --- missing key: clear error, no output, no API call ---
start_server ok
rc=0; out="$(AGENT_APP_KEY_FILE="$WORK/nope.pem" bash "$SCRIPT" 2>"$WORK/err")" || rc=$?
check "missing key: exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
check "missing key: stdout empty" "$out" ""
check "missing key: says which file" "$(grep -c "key file not readable: $WORK/nope.pem" "$WORK/err")" "1"
check "missing key: API not called" "$([ -e "$SRV/requests.jsonl" ] && echo called || echo none)" "none"

# --- not a key: openssl failure surfaces without key bytes ---
echo "not a key" >"$WORK/bad.pem"
rc=0; out="$(AGENT_APP_KEY_FILE="$WORK/bad.pem" bash "$SCRIPT" 2>"$WORK/err")" || rc=$?
check "bad key: exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
check "bad key: stdout empty" "$out" ""
check "bad key: says signing failed" "$(grep -c 'could not sign the JWT' "$WORK/err")" "1"

# --- unknown flag ---
rc=0; bash "$SCRIPT" --bogus >/dev/null 2>"$WORK/err" || rc=$?
check "unknown flag: exit 2" "$rc" "2"
check "unknown flag: usage on stderr" "$(grep -c '^usage: agent-token.sh' "$WORK/err")" "1"

# --- keychain path, with a stub `security` on PATH (never the real keychain) ---
start_server ok
mkdir -p "$WORK/kc-hex" "$WORK/kc-fail"
cat >"$WORK/kc-hex/security" <<STUB
#!/bin/sh
echo "\$*" >"$WORK/kc-args"
xxd -p "$WORK/key.pem" | tr -d '\n'; echo
STUB
printf '#!/bin/sh\necho "security: item not found" >&2\nexit 44\n' >"$WORK/kc-fail/security"
chmod +x "$WORK/kc-hex/security" "$WORK/kc-fail/security"
rc=0; out="$(env -u AGENT_APP_KEY_FILE AGENT_APP_KEYCHAIN_SERVICE=other-item AGENT_APP_KEYCHAIN_ACCOUNT=other \
  PATH="$WORK/kc-hex:$PATH" bash "$SCRIPT" 2>"$WORK/err")" || rc=$?
req="$(tail -1 "$SRV/requests.jsonl")"
check "keychain hex: exits 0" "$rc" "0"
check "keychain hex: stdout is the token" "$out" "$CANNED"
check "keychain hex: decoded key signs a valid JWT" "$(jq -r .verify <<<"$req")" "Verified OK"
check "keychain: reads only the App's item (env cannot redirect it)" "$(cat "$WORK/kc-args")" \
  "find-generic-password -s agentic-workflow-agent-app -a private-key -w"
check "keychain hex: temp dir removed" "$(find "$TMPDIR" -mindepth 1 | wc -l | tr -d ' ')" "0"
n="$(wc -l <"$SRV/requests.jsonl" | tr -d ' ')"
rc=0; out="$(env -u AGENT_APP_KEY_FILE PATH="$WORK/kc-fail:$PATH" bash "$SCRIPT" 2>"$WORK/err")" || rc=$?
check "keychain failure: exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
check "keychain failure: stdout empty" "$out" ""
check "keychain failure: says so" "$(grep -c 'could not read the App key from the keychain' "$WORK/err")" "1"
check "keychain failure: API not called" "$(wc -l <"$SRV/requests.jsonl" | tr -d ' ')" "$n"

# --- API URL: only GitHub over https, or loopback http for tests ---
for url in "http://api.github.com" "https://evil.example" "http://10.0.0.1:8080" \
  "https://api.github.com.evil.example" "http://127.0.0.1:1\"x" \
  $'http://127.0.0.1:1"\ntrace-ascii = "'"$WORK"'/trace'; do
  rc=0; out="$(AGENT_APP_API_URL="$url" bash "$SCRIPT" 2>"$WORK/err")" || rc=$?
  check "API URL rejected ($(printf '%q' "$url")): exit 2" "$rc" "2"
  check "API URL rejected: says why" "$(grep -c 'AGENT_APP_API_URL must be https://api.github.com' "$WORK/err")" "1"
done
check "API URL rejected: no request reached the fake" "$(wc -l <"$SRV/requests.jsonl" | tr -d ' ')" "$n"
check "curl config injection wrote no trace file" "$([ -e "$WORK/trace" ] && echo written || echo none)" "none"
rc=0; out="$(AGENT_APP_API_URL="http://localhost:$(cat "$SRV/port")" bash "$SCRIPT" 2>/dev/null)" || rc=$?
check "API URL: http://localhost:<port> accepted" "$out" "$CANNED"

# --- --repo names are validated before they reach the request ---
rc=0; bash "$SCRIPT" --repo 'a"b' >/dev/null 2>"$WORK/err" || rc=$?
check "bad --repo name: exit 2" "$rc" "2"
check "bad --repo name: says why" "$(grep -c 'invalid repository name' "$WORK/err")" "1"

# --- the user's ~/.curlrc is ignored (curl -q) ---
mkdir -p "$WORK/home"
printf 'trace-ascii = "%s/rc-trace"\n' "$WORK" >"$WORK/home/.curlrc"
rc=0; out="$(HOME="$WORK/home" bash "$SCRIPT" 2>/dev/null)" || rc=$?
check "~/.curlrc ignored: token still minted" "$out" "$CANNED"
check "~/.curlrc ignored: no trace written" "$([ -e "$WORK/rc-trace" ] && echo written || echo none)" "none"

# --- a weird TMPDIR (quote, newline) cannot inject curl directives ---
odd="$WORK/odd\"dir"$'\n'"x"; mkdir -p "$odd"
rc=0; out="$(TMPDIR="$odd" bash "$SCRIPT" 2>"$WORK/err")" || rc=$?
check "odd TMPDIR: token minted" "$out" "$CANNED"
check "odd TMPDIR: temp dir removed" "$(find "$odd" -mindepth 1 | wc -l | tr -d ' ')" "0"

# --- inherited xtrace never traces the JWT or the token ---
rc=0; out="$(bash -x "$SCRIPT" 2>"$WORK/xtrace")" || rc=$?
check "bash -x: token still on stdout" "$out" "$CANNED"
check "bash -x: token not traced" "$(grep -c "$CANNED" "$WORK/xtrace" || true)" "0"
check "bash -x: JWT not traced" "$(grep -c -e 'eyJ' -e 'Bearer' "$WORK/xtrace" || true)" "0"
rc=0; out="$(env SHELLOPTS=xtrace bash "$SCRIPT" 2>"$WORK/xtrace2")" || rc=$?
check "SHELLOPTS=xtrace: token not traced" "$(grep -c -e "$CANNED" -e 'eyJ' "$WORK/xtrace2" || true)" "0"

# --- missing jq fails up front, before any token is minted ---
mkdir -p "$WORK/nojq"
for c in mktemp cat grep openssl tr date curl rm sed xxd; do ln -sf "$(command -v "$c")" "$WORK/nojq/$c"; done
n="$(wc -l <"$SRV/requests.jsonl" | tr -d ' ')"
rc=0; out="$(PATH="$WORK/nojq" "$(command -v bash)" "$SCRIPT" 2>"$WORK/err")" || rc=$?
check "no jq: exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
check "no jq: says jq is required" "$(grep -c 'jq is required' "$WORK/err")" "1"
check "no jq: API not called" "$(wc -l <"$SRV/requests.jsonl" | tr -d ' ')" "$n"

# --- API error message: token shapes redacted, control characters stripped ---
start_server errmsg
rc=0; out="$(bash "$SCRIPT" 2>"$WORK/err")" || rc=$?
check "errmsg: exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
check "errmsg: no token shape survives" "$(grep -c -e 'ghs_abc' -e 'def.ghi' -e 'github_pat_' -e 'bb-cc' "$WORK/err" || true)" "0"
check "errmsg: no control characters" "$(LC_ALL=C tr -d '\n' <"$WORK/err" | LC_ALL=C grep -c '[[:cntrl:]]' || true)" "0"
check "errmsg: still names status and message" "$(grep -c 'GitHub returned HTTP 401: Bad credentials' "$WORK/err")" "1"

# --- the token never lands on disk: a jq wrapper scans TMPDIR while the script runs ---
start_server ok
mkdir -p "$WORK/jqscan"
REAL_JQ="$(command -v jq)"
cat >"$WORK/jqscan/jq" <<STUB
#!/bin/sh
grep -rl "$CANNED" "\$TMPDIR" >>"$WORK/disk-scan" 2>/dev/null
echo scanned >>"$WORK/disk-scan-ran"
exec "$REAL_JQ" "\$@"
STUB
chmod +x "$WORK/jqscan/jq"
rc=0; out="$(PATH="$WORK/jqscan:$PATH" bash "$SCRIPT" --verbose 2>/dev/null)" || rc=$?
check "token off disk: token minted" "$out" "$CANNED"
check "token off disk: scan ran while the script was alive" "$([ -s "$WORK/disk-scan-ran" ] && echo yes || echo no)" "yes"
check "token off disk: no file under TMPDIR ever held the token" "$(cat "$WORK/disk-scan" 2>/dev/null | wc -l | tr -d ' ')" "0"

# --- signals mid-request: prompt exit, the run's own curl dies, and no token
#     ever reaches stdout, checked after the fake has answered ---
mkdir -p "$WORK/curlpid"
REAL_CURL="$(command -v curl)"
cat >"$WORK/curlpid/curl" <<STUB
#!/bin/sh
echo \$\$ >"$WORK/curl.pid"
exec "$REAL_CURL" "\$@"
STUB
chmod +x "$WORK/curlpid/curl"
ms() { python3 -c 'import time; print(int(time.time() * 1000))'; }
# `python3 -c "$DFL" CMD...` execs CMD with SIGINT and SIGQUIT back at their
# default disposition. A plain background `&` in this non-interactive shell
# would start the script with both ignored, so INT and QUIT could not be tested
# that way. It's a direct command, not a function, so `$!` is the script's pid.
DFL='import os, signal, sys
for s in (signal.SIGINT, signal.SIGQUIT):
    signal.signal(s, signal.SIG_DFL)
os.execvp(sys.argv[1], sys.argv[1:])'
# signal_mid_request "SIG [SIG2 ...]" WANT [kill]: send the signals back to back
# to the script while the fake holds the request. WANT is the exit code or
# "nonzero". With "kill" (SIGKILL: nothing can clean up) the request job
# survives the script, so only "nothing printed, no file left" is checked.
signal_mid_request() {
  local sigs="$1" want="$2" mode="${3:-}" name tpid rc elapsed alive sig t0 answered_at_exit
  name="SIG${sigs// /+SIG}"
  export FAKE_HOLD=3; start_server slow; unset FAKE_HOLD
  : >"$WORK/curl.pid"
  PATH="$WORK/curlpid:$PATH" python3 -c "$DFL" bash "$SCRIPT" >"$WORK/sig.out" 2>"$WORK/sig.err" &
  tpid=$!
  for _ in $(seq 1 100); do [ -e "$SRV/inflight" ] && break; sleep 0.1; done
  if ! [ -e "$SRV/inflight" ]; then
    echo "not ok - $name: the request never reached the fake"; kill "$tpid" 2>/dev/null; exit 1
  fi
  if ! [ -s "$WORK/curl.pid" ]; then
    echo "not ok - $name: curl pid not recorded"; kill "$tpid" 2>/dev/null; exit 1
  fi
  t0="$(ms)"
  for sig in $sigs; do kill "-$sig" "$tpid" 2>/dev/null || true; done
  rc=0; wait "$tpid" || rc=$?
  answered_at_exit=$([ -e "$SRV/replying" ] && echo yes || echo no)
  elapsed=$(( $(ms) - t0 ))
  if [ "$want" = nonzero ]; then
    check "$name: exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
  else
    check "$name: exit $want" "$rc" "$want"
  fi
  check "$name: exits before the fake answers (took ${elapsed} ms)" "$answered_at_exit" "no"
  if [ "$mode" != kill ]; then
    alive=yes
    for _ in $(seq 1 10); do kill -0 "$(cat "$WORK/curl.pid")" 2>/dev/null || { alive=no; break; }; sleep 0.1; done
    check "$name: this run's curl is gone" "$alive" "no"
  fi
  for _ in $(seq 1 60); do [ -e "$SRV/answered" ] && break; sleep 0.1; done
  check "$name: the fake answered (the late-token window was exercised)" "$([ -e "$SRV/answered" ] && echo yes || echo no)" "yes"
  sleep 0.5
  check "$name: stdout still empty after the fake answered" "$(wc -c <"$WORK/sig.out" | tr -d ' ')" "0"
  if [ "$mode" = kill ]; then
    check "$name: no file left in the temp dir" "$(find "$TMPDIR" -type f | wc -l | tr -d ' ')" "0"
    find "$TMPDIR" -mindepth 1 -delete # SIGKILL leaves the empty private dir behind
  else
    check "$name: temp dir removed" "$(find "$TMPDIR" -mindepth 1 | wc -l | tr -d ' ')" "0"
    find "$TMPDIR" -mindepth 1 -delete # a failure here must not cascade into the next case
  fi
  cat "$WORK/sig.err" >>"$WORK/term.err"
  stop_server
}
: >"$WORK/term.err"
signal_mid_request TERM 143
signal_mid_request HUP 129
signal_mid_request INT 130
signal_mid_request QUIT 131
signal_mid_request PROF 155
signal_mid_request USR1 nonzero
signal_mid_request ALRM nonzero
signal_mid_request "TERM TERM" 143
signal_mid_request "HUP HUP" 129
signal_mid_request "TERM HUP" nonzero
signal_mid_request KILL 137 kill

# --- key material never in any output across all runs ---
leaks="$(cat "$WORK"/err "$WORK"/tty.out "$WORK"/tty2.out "$WORK"/xtrace "$WORK"/xtrace2 "$WORK"/term.err | grep -c -e "$KEY_LINE" -e 'PRIVATE KEY' || true)"
check "key material never printed" "$leaks" "0"

exit "$fail"
