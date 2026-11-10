#!/usr/bin/env bash
# Tests for scripts/sindri-prove.sh and scripts/lib/with-tty.sh. Run: bash scripts/tests/sindri-prove.test.sh
# A stub SINDRI prints its arguments, whether stdin and stdout are terminals, and the variables the prover sets or
# unsets; a stub curl on PATH records its arguments and answers with a fixed body and status. Nothing here starts a
# real sindri, reads the owner's home or opens a socket.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
PROVE="$ROOT/scripts/sindri-prove.sh"
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
  case "$2" in *"$3"*) echo "not ok - $1"; echo "  unexpected: $3"; echo "  in:         $2"; fail=1 ;; *) echo "ok - $1" ;; esac
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
T="$WORK/tmp"         # the TMPDIR the prover sees
STATE="$WORK/state"   # the AW_STATE_DIR the prover sees (outside T)
mkdir -p "$T" "$STATE" "$WORK/elsewhere" "$WORK/bin"

STUB="$WORK/bin/sindri-stub"
cat > "$STUB" <<'EOF'
#!/usr/bin/env bash
echo "args=$*"
if [ -t 0 ] && [ -t 1 ]; then echo "tty=yes"; else echo "tty=no"; fi
for v in CLAUDECODE CLAUDE_CODE_ENTRYPOINT CLAUDE_CODE_EXECPATH CLAUDE_CODE_SESSION_ID CLAUDE_PID AW_SINDRI_CHILD CODEX_SANDBOX CURSOR_AGENT AW_SINDRI_PROVER AW_SINDRI_PORT; do
  echo "$v=${!v-<unset>}"
done
if [ "${STUB_READ:-0}" = 1 ]; then IFS= read -r line; echo "stdin=[$line]"; fi
if [ "${STUB_CR:-0}" = 1 ]; then printf '\033[?1h\033=carriage\r\n\033[?1l\033>'; fi
exit "${STUB_EXIT:-0}"
EOF
chmod +x "$STUB"

cat > "$WORK/bin/curl" <<'EOF'
#!/usr/bin/env bash
: > "$CURL_LOG"
out="" write=""
while [ $# -gt 0 ]; do
  printf '%s\n' "$1" >> "$CURL_LOG"
  case "$1" in
    -o) printf '%s\n' "$2" >> "$CURL_LOG"; out="$2"; shift ;;
    -w) printf '%s\n' "$2" >> "$CURL_LOG"; write="$2"; shift ;;
  esac
  shift
done
[ -n "$out" ] && printf '{"ok":true}' > "$out"
[ -n "$write" ] && printf '%s' "${STUB_STATUS:-200}"
[ -z "$out" ] && printf 'event: service.heartbeat\nid: 2\ndata: {}\n\n'
exit "${STUB_CURL_RC:-0}"
EOF
chmod +x "$WORK/bin/curl"
export CURL_LOG="$WORK/curl.log"

# prove HOME_VALUE args… — runs the prover with the test's TMPDIR, state root and stub; "-" leaves AW_SINDRI_HOME unset.
prove() {
  local home="$1"; shift
  if [ "$home" = "-" ]; then
    env -u AW_SINDRI_HOME TMPDIR="$T" AW_STATE_DIR="$STATE" SINDRI="$STUB" PATH="$WORK/bin:$PATH" bash "$PROVE" "$@"
  else
    env TMPDIR="$T" AW_STATE_DIR="$STATE" SINDRI="$STUB" AW_SINDRI_HOME="$home" PATH="$WORK/bin:$PATH" bash "$PROVE" "$@"
  fi
}
guard_msg="sindri-prove: AW_SINDRI_HOME must be under \$TMPDIR or a proof folder"

# --- refuses_an_unset_home ---
out="$(prove - status 2>"$WORK/err" </dev/null)"; rc=$?
check "refuses_an_unset_home: exit 2" "$rc" "2"
contains "refuses_an_unset_home: guard message" "$(cat "$WORK/err")" "$guard_msg"
check "refuses_an_unset_home: SINDRI not run" "$out" ""

# --- refuses_the_default_home ---
out="$(prove "$STATE/sindri-app" status 2>"$WORK/err" </dev/null)"; rc=$?
check "refuses_the_default_home: exit 2 under AW_STATE_DIR" "$rc" "2"
contains "refuses_the_default_home: names the home" "$(cat "$WORK/err")" "(got $STATE/sindri-app)"
check "refuses_the_default_home: SINDRI not run" "$out" ""
mkdir -p "$WORK/h"
out="$(env -u AW_STATE_DIR HOME="$WORK/h" TMPDIR="$T" SINDRI="$STUB" AW_SINDRI_HOME="$WORK/h/.agentic-workflow/sindri-app" \
  bash "$PROVE" status 2>/dev/null </dev/null)"; rc=$?
check "refuses_the_default_home: exit 2 under ~/.agentic-workflow" "$rc" "2"

# --- refuses_a_home_outside_tmp ---
prove "$WORK/elsewhere/home" status >/dev/null 2>&1 </dev/null; rc=$?
check "refuses_a_home_outside_tmp: a plain path" "$rc" "2"
prove "$T" status >/dev/null 2>&1 </dev/null; rc=$?
check "refuses_a_home_outside_tmp: TMPDIR itself" "$rc" "2"
prove "$T/../elsewhere/home" status >/dev/null 2>&1 </dev/null; rc=$?
check "refuses_a_home_outside_tmp: a .. path out of TMPDIR" "$rc" "2"
ln -s "$WORK/elsewhere" "$T/link-out"
prove "$T/link-out/home" status >/dev/null 2>&1 </dev/null; rc=$?
check "refuses_a_home_outside_tmp: a link out of TMPDIR" "$rc" "2"
prove "relative/home" status >/dev/null 2>&1 </dev/null; rc=$?
check "refuses_a_home_outside_tmp: a relative path outside TMPDIR" "$rc" "2"

# --- accepts_a_temp_home ---
out="$(prove "$T/run1/home" status --json 2>&1 </dev/null)"; rc=$?
check "accepts_a_temp_home: exit 0 (home not created yet)" "$rc" "0"
contains "accepts_a_temp_home: SINDRI ran" "$out" "args=status --json"
ln -s "$T" "$WORK/tmp-link"
out="$(env TMPDIR="$WORK/tmp-link" AW_STATE_DIR="$STATE" SINDRI="$STUB" AW_SINDRI_HOME="$T/run2/home" bash "$PROVE" status 2>&1 </dev/null)"; rc=$?
check "accepts_a_temp_home: TMPDIR reached through a link" "$rc" "0"

# --- accepts_a_proof_folder ---
out="$(prove "$STATE/jthorare-agentic-workflow/proof/sindri-api-p1/home" status 2>&1 </dev/null)"; rc=$?
check "accepts_a_proof_folder: exit 0" "$rc" "0"
contains "accepts_a_proof_folder: SINDRI ran" "$out" "args=status"
prove "$STATE/jthorare-agentic-workflow/proof" status >/dev/null 2>&1 </dev/null; rc=$?
check "accepts_a_proof_folder: not the proof folder itself" "$rc" "2"
prove "$STATE/jthorare-agentic-workflow/reviews/home" status >/dev/null 2>&1 </dev/null; rc=$?
check "accepts_a_proof_folder: not another state folder" "$rc" "2"
prove "$STATE/proof/home" status >/dev/null 2>&1 </dev/null; rc=$?
check "accepts_a_proof_folder: not a proof folder without a repo slug" "$rc" "2"

# --- marks_only_a_temp_home_as_scratch ---
[ -f "$T/run1/home/.prover-scratch" ] && marked=yes || marked=no
check "marks_only_a_temp_home_as_scratch: a temp home carries the marker" "$marked" "yes"
[ -e "$STATE/jthorare-agentic-workflow/proof/sindri-api-p1/home/.prover-scratch" ] && marked=yes || marked=no
check "marks_only_a_temp_home_as_scratch: a proof folder is not marked" "$marked" "no"
[ -e "$STATE/sindri-app/.prover-scratch" ] && marked=yes || marked=no
check "marks_only_a_temp_home_as_scratch: the refused default home is not touched" "$marked" "no"

H="$T/home"
mkdir -p "$H/auth"

# --- verb_mode_keeps_the_environment ---
out="$(CLAUDECODE=1 CODEX_SANDBOX=seatbelt prove "$H" stop --json 2>&1 </dev/null)"; rc=$?
check "verb_mode_keeps_the_environment: exit 0" "$rc" "0"
contains "verb_mode_keeps_the_environment: arguments passed" "$out" "args=stop --json"
contains "verb_mode_keeps_the_environment: CLAUDECODE kept" "$out" "CLAUDECODE=1"
contains "verb_mode_keeps_the_environment: CODEX_SANDBOX kept" "$out" "CODEX_SANDBOX=seatbelt"
contains "verb_mode_keeps_the_environment: no prover marker" "$out" "AW_SINDRI_PROVER=<unset>"
contains "verb_mode_keeps_the_environment: no terminal" "$out" "tty=no"
STUB_EXIT=2 prove "$H" stop >/dev/null 2>&1 </dev/null; rc=$?
check "verb_mode_keeps_the_environment: SINDRI's exit code" "$rc" "2"

# --- human_mode_unsets_agent_variables_sets_the_marker_and_has_a_tty ---
out="$(CLAUDECODE=1 CLAUDE_CODE_ENTRYPOINT=cli CLAUDE_CODE_EXECPATH=x CLAUDE_CODE_SESSION_ID=s CLAUDE_PID=1 AW_SINDRI_CHILD=1 CODEX_SANDBOX=seatbelt CURSOR_AGENT=1 prove "$H" human status 2>&1 </dev/null)"; rc=$?
check "human_mode_unsets_agent_variables_sets_the_marker_and_has_a_tty: exit 0" "$rc" "0"
contains "human_mode_unsets_agent_variables_sets_the_marker_and_has_a_tty: arguments" "$out" "args=status"
for v in CLAUDECODE CLAUDE_CODE_ENTRYPOINT CLAUDE_CODE_EXECPATH CLAUDE_CODE_SESSION_ID CLAUDE_PID AW_SINDRI_CHILD CODEX_SANDBOX CURSOR_AGENT; do
  contains "human_mode_unsets_agent_variables_sets_the_marker_and_has_a_tty: $v unset" "$out" "$v=<unset>"
done
contains "human_mode_unsets_agent_variables_sets_the_marker_and_has_a_tty: marker" "$out" "AW_SINDRI_PROVER=1"
contains "human_mode_unsets_agent_variables_sets_the_marker_and_has_a_tty: terminal" "$out" "tty=yes"
out="$(printf 'abc123\n' | STUB_READ=1 prove "$H" human profile approve deadbeef 2>&1)"
contains "human_mode_unsets_agent_variables_sets_the_marker_and_has_a_tty: stdin passed through" "$out" "stdin=[abc123]"
lacks "human_mode_unsets_agent_variables_sets_the_marker_and_has_a_tty: typed input not echoed" \
  "$(printf '%s\n' "$out" | grep -v '^stdin=')" "abc123"
STUB_EXIT=3 prove "$H" human stop >/dev/null 2>&1 </dev/null; rc=$?
check "human_mode_unsets_agent_variables_sets_the_marker_and_has_a_tty: SINDRI's exit code" "$rc" "3"

# --- human_mode_strips_cr ---
out="$(STUB_CR=1 prove "$H" human status 2>&1 </dev/null)"
contains "human_mode_strips_cr: output arrives" "$out" "carriage"
check "human_mode_strips_cr: no CR in the output" "$(printf '%s' "$out" | tr -cd '\r' | wc -c | tr -d ' ')" "0"
check "human_mode_strips_cr: no terminal control sequences in the output" "$(printf '%s' "$out" | tr -cd '\033' | wc -c | tr -d ' ')" "0"
check "human_mode_strips_cr: the line itself is kept" "$(printf '%s\n' "$out" | tail -1)" "carriage"

# --- port_is_zero ---
out="$(AW_SINDRI_PORT=7410 prove "$H" status 2>&1 </dev/null)"  # test-ports: ok: a value for a stub sindri, nothing binds it
contains "port_is_zero: verb mode" "$out" "AW_SINDRI_PORT=0"
out="$(AW_SINDRI_PORT=7410 prove "$H" human status 2>&1 </dev/null)"  # test-ports: ok: a value for a stub sindri, nothing binds it
contains "port_is_zero: human mode" "$out" "AW_SINDRI_PORT=0"

# --- curl_mode_reads_url_and_token_and_adds_headers ---
printf '{"url":"http://127.0.0.1:5555/","pid":4242,"epoch":3}' > "$H/control.json"
printf 'tok-abc\n' > "$H/auth/human.token"
out="$(prove "$H" url 2>/dev/null)"
check "curl_mode_reads_url_and_token_and_adds_headers: url mode prints the base URL" "$out" "http://127.0.0.1:5555"
out="$(prove "$H" curl POST /sindri/profile/approve '{"hash":"h","confirmed":true}' 2>"$WORK/err")"; rc=$?
log="$(cat "$CURL_LOG")"
check "curl_mode_reads_url_and_token_and_adds_headers: body on stdout" "$out" '{"ok":true}'
contains "curl_mode_reads_url_and_token_and_adds_headers: status on stderr" "$(cat "$WORK/err")" "HTTP 200"
check "curl_mode_reads_url_and_token_and_adds_headers: exit 0 on 2xx" "$rc" "0"
contains "curl_mode_reads_url_and_token_and_adds_headers: URL" "$log" "http://127.0.0.1:5555/sindri/profile/approve"
contains "curl_mode_reads_url_and_token_and_adds_headers: method" "$log" $'-X\nPOST'
for h in 'X-Sindri-Caller: human' 'X-Sindri-Terminal: 1' 'X-Sindri-Client: prover' 'X-Sindri-Token: tok-abc' \
  'Content-Type: application/json'; do
  contains "curl_mode_reads_url_and_token_and_adds_headers: $h" "$log" $'-H\n'"$h"
done
contains "curl_mode_reads_url_and_token_and_adds_headers: JSON body sent" "$log" '{"hash":"h","confirmed":true}'
out="$(STUB_STATUS=403 prove "$H" curl GET /sindri/status 2>"$WORK/err")"; rc=$?
contains "curl_mode_reads_url_and_token_and_adds_headers: a refusal's status" "$(cat "$WORK/err")" "HTTP 403"
check "curl_mode_reads_url_and_token_and_adds_headers: exit 1 on a non-2xx status" "$rc" "1"
lacks "curl_mode_reads_url_and_token_and_adds_headers: no Content-Type without a body" "$(cat "$CURL_LOG")" "Content-Type"
rm -f "$H/control.json"
prove "$H" curl GET /sindri/status >/dev/null 2>"$WORK/err"; rc=$?
check "curl_mode_reads_url_and_token_and_adds_headers: exit 2 when not running" "$rc" "2"
contains "curl_mode_reads_url_and_token_and_adds_headers: says why" "$(cat "$WORK/err")" "control.json"
printf '{"url":"http://127.0.0.1:5555/","pid":4242,"epoch":3}' > "$H/control.json"

# --- publish_reads_the_maps_publish_state_as_the_owner_or_as_a_session ---
out="$(prove "$H" publish 2>"$WORK/err")"; rc=$?
log="$(cat "$CURL_LOG")"
check "publish: body on stdout" "$out" '{"ok":true}'
check "publish: exit 0 on 2xx" "$rc" "0"
contains "publish: asks the publish route" "$log" "http://127.0.0.1:5555/sindri/context/map/publish"
contains "publish: as the owner it carries the human headers" "$log" "X-Sindri-Caller: human"
contains "publish: as the owner it carries the token" "$log" "X-Sindri-Token: tok-abc"
out="$(prove "$H" publish --session --repo aw-split-panes 2>"$WORK/err")"; rc=$?
log="$(cat "$CURL_LOG")"
check "publish --session: body on stdout" "$out" '{"ok":true}'
contains "publish --session: HTTP status on stderr" "$(cat "$WORK/err")" "HTTP 200"
contains "publish --session: asks the publish route" "$log" "http://127.0.0.1:5555/sindri/context/map/publish"
contains "publish --session: names its repo" "$log" "X-Sindri-Repo: aw-split-panes"
lacks "publish --session: sends no caller header (the API reads a session)" "$log" "X-Sindri-Caller"
lacks "publish --session: sends no token" "$log" "X-Sindri-Token"
prove "$H" publish --session >/dev/null 2>&1; log="$(cat "$CURL_LOG")"
lacks "publish --session: without --repo sends no repo header" "$log" "X-Sindri-Repo"
out="$(STUB_STATUS=404 prove "$H" publish --session 2>"$WORK/err")"; rc=$?
check "publish --session: exit 1 on a non-2xx status" "$rc" "1"
prove "$H" publish --repo aw-split-panes >/dev/null 2>&1; rc=$?
check "publish: --repo without --session is a usage error" "$rc" "2"
prove "$H" publish --bogus >/dev/null 2>&1; rc=$?
check "publish: an unknown flag is a usage error" "$rc" "2"
prove "$WORK/elsewhere" publish >/dev/null 2>&1; rc=$?
check "publish: refused outside a guarded home" "$rc" "2"

# --- curl_flags_drop_terminal_and_token ---
prove "$H" curl --no-terminal POST /sindri/stop >/dev/null 2>&1
log="$(cat "$CURL_LOG")"
lacks "curl_flags_drop_terminal_and_token: --no-terminal drops the terminal header" "$log" "X-Sindri-Terminal"
contains "curl_flags_drop_terminal_and_token: --no-terminal keeps the token" "$log" "X-Sindri-Token: tok-abc"
prove "$H" curl --no-token GET /sindri/status >/dev/null 2>&1
log="$(cat "$CURL_LOG")"
lacks "curl_flags_drop_terminal_and_token: --no-token drops the token header" "$log" "X-Sindri-Token"
contains "curl_flags_drop_terminal_and_token: --no-token keeps the caller" "$log" "X-Sindri-Caller: human"
prove "$H" curl --token stale-1 GET /sindri/status >/dev/null 2>&1
log="$(cat "$CURL_LOG")"
contains "curl_flags_drop_terminal_and_token: --token overrides the file" "$log" "X-Sindri-Token: stale-1"
lacks "curl_flags_drop_terminal_and_token: --token drops the file's token" "$log" "tok-abc"
prove "$H" curl -H 'Origin: http://localhost:3000' POST /sindri/stop >/dev/null 2>&1
log="$(cat "$CURL_LOG")"
contains "curl_flags_drop_terminal_and_token: -H adds a header" "$log" $'-H\nOrigin: http://localhost:3000'
prove "$H" curl --bogus GET /sindri/status >/dev/null 2>&1; rc=$?
check "curl_flags_drop_terminal_and_token: an unknown flag is a usage error" "$rc" "2"
rm -f "$H/auth/human.token"
prove "$H" curl GET /sindri/status >/dev/null 2>"$WORK/err"; rc=$?
check "curl_flags_drop_terminal_and_token: a missing token file is an error without --no-token" "$rc" "2"
prove "$H" curl --no-token GET /sindri/status >/dev/null 2>&1; rc=$?
check "curl_flags_drop_terminal_and_token: --no-token needs no token file" "$rc" "0"

# --- curl_stream_prints_the_body_as_it_arrives ---
printf 'tok-abc\n' > "$H/auth/human.token"
out="$(prove "$H" curl --stream GET '/sindri/events?after=1' 2>"$WORK/err")"; rc=$?
log="$(cat "$CURL_LOG")"
check "curl_stream_prints_the_body_as_it_arrives: the body is on stdout" "$out" $'event: service.heartbeat\nid: 2\ndata: {}'
check "curl_stream_prints_the_body_as_it_arrives: no status line" "$(cat "$WORK/err")" ""
check "curl_stream_prints_the_body_as_it_arrives: exit 0 when the stream ends" "$rc" "0"
contains "curl_stream_prints_the_body_as_it_arrives: -N" "$log" $'-N'
lacks "curl_stream_prints_the_body_as_it_arrives: no buffer file" "$log" $'-o\n'
lacks "curl_stream_prints_the_body_as_it_arrives: no status write-out" "$log" $'-w\n'
contains "curl_stream_prints_the_body_as_it_arrives: URL" "$log" "http://127.0.0.1:5555/sindri/events?after=1"
contains "curl_stream_prints_the_body_as_it_arrives: human headers" "$log" $'-H\nX-Sindri-Caller: human'
contains "curl_stream_prints_the_body_as_it_arrives: terminal header" "$log" $'-H\nX-Sindri-Terminal: 1'
contains "curl_stream_prints_the_body_as_it_arrives: the token" "$log" "X-Sindri-Token: tok-abc"
STUB_CURL_RC=22 prove "$H" curl --stream GET /sindri/events >/dev/null 2>&1; rc=$?
check "curl_stream_prints_the_body_as_it_arrives: an HTTP error status is exit 1" "$rc" "1"
STUB_CURL_RC=7 prove "$H" curl --stream GET /sindri/events >/dev/null 2>&1; rc=$?
check "curl_stream_prints_the_body_as_it_arrives: no response is exit 3" "$rc" "3"
prove "$WORK/elsewhere/home" curl --stream GET /sindri/events >/dev/null 2>&1; rc=$?
check "curl_stream_prints_the_body_as_it_arrives: still refuses a non-temp home" "$rc" "2"

# --- with_tty_under_set_e_outlives_its_feeder ---
# A caller with errexit on, and a command that runs past the feeder's exit: with_tty returns the command's status.
out="$(bash -c 'set -euo pipefail; source "$1"; with_tty sh -c "sleep 0.6; echo hi; exit 0" </dev/null; echo "after"' _ "$ROOT/scripts/lib/with-tty.sh" 2>&1)"; rc=$?
check "with_tty_under_set_e_outlives_its_feeder: the caller carries on" "$rc" "0"
contains "with_tty_under_set_e_outlives_its_feeder: the caller reaches its next line" "$out" "after"

# --- with_tty_under_set_e_stops_its_feeder_when_the_command_fails ---
# A caller with errexit and pipefail on, and a command that fails: with_tty still cleans up (its temp folder, its
# feeder) before it returns the command's status, instead of the failed pipeline ending it halfway.
FT="$WORK/feeder-tmp"; mkdir -p "$FT"
out="$(TMPDIR="$FT" bash -c 'set -euo pipefail; source "$1"; with_tty sh -c "echo hi; exit 3" < <(sleep 8 >/dev/null 2>&1); echo "after"' _ "$ROOT/scripts/lib/with-tty.sh" 2>&1)"; rc=$?
check "with_tty_under_set_e_stops_its_feeder_when_the_command_fails: the command's status ends the caller" "$rc" "3"
contains "with_tty_under_set_e_stops_its_feeder_when_the_command_fails: the output arrives" "$out" "hi"
check "with_tty_under_set_e_stops_its_feeder_when_the_command_fails: no temp folder is left" "$(ls "$FT" | wc -l | tr -d ' ')" "0"
check "with_tty_under_set_e_stops_its_feeder_when_the_command_fails: no feeder is left running" "$(pgrep -f "$FT/with-tty" | wc -l | tr -d ' ')" "0"

if [ "$fail" -ne 0 ]; then echo "FAIL: sindri-prove tests"; exit 1; fi
echo "PASS: sindri-prove tests"
