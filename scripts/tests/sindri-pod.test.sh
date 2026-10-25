#!/usr/bin/env bash
# Tests for scripts/sindri-pod.sh connect with a fake `kubectl` and a fake `sindri-ui` on a temp AW_STATE_DIR: the dry run
# prints the plan and writes nothing; connect writes the mirror home (control.json with the forwarded loopback URL, the pod's
# pid and epoch, auth/human.token; files 0600, dirs 0700), forwards the pod's own port and starts the app on it; a new
# epoch or token in the pod rewrites the home; a port-forward that drops is retried; refusals (no kubectl, no sindri-ui, no
# pod, a pod that is not Ready, a local port that is taken, no rights; --check reports each and exits 0 or 2); the mirror
# home lives in $TMPDIR and goes on exit (a TERM too), stale ones are removed; the retry loop prints its attempts and is
# bounded; profile edit (a fake EDITOR): unchanged stops, a valid edit is validated, put back and approved in that order,
# an invalid one is never approved and its copy kept, a refused approval's exit code propagates; --onyx forwards the pod's Onyx port and runs `sindri onyx attach` in the pod with -it; a failed attach is the exit code.
# Run: bash scripts/tests/sindri-pod.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(cd "$(mktemp -d)" && pwd -P)"
descendants() {   # <pid>: every process below it, by ps parent links (no pattern matching)
  ps -eo pid=,ppid= | awk -v root="$1" '{ kid[$2] = kid[$2] " " $1 } END {
    n = split(root, todo, " "); while (n > 0) { p = todo[n--]; m = split(kid[p], k, " ")
      for (i = 1; i <= m; i++) { print k[i]; todo[++n] = k[i] } } }'
}
cleanup() {
  local p
  for p in "$WORK"/runs/*.pid; do [ -f "$p" ] && kill "$(cat "$p")" 2>/dev/null; done
  # whatever this run still has below it, found by parent links; nothing else is touched
  for p in $(descendants "$$"); do kill "$p" 2>/dev/null; done
  rm -rf "$WORK"
}
trap cleanup EXIT
unset AW_SINDRI_HOME AW_SINDRI_UI
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state" AW_SINDRI_POD_POLL=0.2 AW_SINDRI_POD_RETRIES=5 AW_SINDRI_POD_RETRY_WAIT=0.3
export AW_SINDRI_POD_ONYX_PORT=43000 TMPDIR="$WORK/tmp"
mkdir -p "$HOME" "$WORK/bin" "$WORK/pod" "$WORK/runs" "$TMPDIR"
export CALLS="$WORK/calls" POD="$WORK/pod" STOP="$WORK/ui-stop"
: > "$CALLS"

# The pod: what `kubectl exec` reads (the pod's control.json and human token).
pod_state() {   # pid epoch token port
  printf '{"url":"http://127.0.0.1:%s","pid":%s,"epoch":%s}\n' "$4" "$1" "$2" > "$POD/control.json"
  printf '%s\n' "$3" > "$POD/token"
}
pod_state 41 7 tok-one 47480
echo http://127.0.0.1:43000 > "$POD/onyx-url"

cat > "$WORK/bin/kubectl" <<'EOS'
#!/usr/bin/env bash
echo "kubectl $*" >> "$CALLS"
case " $* " in
  *" auth can-i "*) r="${!#}"; if [ -e "$POD/deny-${r//\//_}" ]; then echo no; else echo yes; fi; exit 0 ;;
  *" get pod "*) [ -e "$POD/no-pod" ] && exit 1
    case "$*" in
      *'@.name!='*) cat "$POD/others" 2>/dev/null; exit 0 ;;
      *'"sindri")].state}'*) cat "$POD/state" 2>/dev/null; exit 0 ;;
      *) cat "$POD/sindri-ready" 2>/dev/null || echo true; exit 0 ;;
    esac ;;
  *" config current-context "*) echo kind-test; exit 0 ;;
  *" config view "*) echo sindri; exit 0 ;;
  *" port-forward "*)
    if [ -e "$POD/pf-fail-once" ] && [[ " $* " == *" pod/"* ]]; then rm -f "$POD/pf-fail-once"; echo "pf failed" >&2; exit 1; fi
    exec sleep 300 ;;
  *" exec "*" -- test -f "*) [ -e "$POD/profile.yaml" ]; exit $? ;;
  *"sindri profile init"*) printf 'schemaVersion: 1\nuser: default\n' > "$POD/profile.yaml"; exit 0 ;;
  *'AW_SINDRI_ONYX_URL:-'*) cat "$POD/onyx-url" 2>/dev/null; exit 0 ;;
  *" exec "*" -- cat "*) cat "$POD/profile.yaml"; exit 0 ;;
  *" rm -f "*) echo "removed" >> "$POD/rm-calls"; exit 0 ;;
  *"sindri doctor"*|*"sindri repo add"*) exit "$(cat "$POD/wrap-rc" 2>/dev/null || echo 0)" ;;
  *" exec -i "*) cat > "$POD/uploaded"; exit 0 ;;
  *"sindri profile validate"*) cp "$POD/uploaded" "$POD/validated"; rc="$(cat "$POD/validate-rc" 2>/dev/null || echo 1)"
    [ "$rc" -ge 2 ] && echo "SND-PROFILE-001 bad key" >&2; exit "$rc" ;;
  *" mv "*) h="$(sha256sum "$POD/profile.yaml" | cut -d' ' -f1)"; [ "$h" = "${!#}" ] || exit 4
    cp "$POD/uploaded" "$POD/profile.yaml"; exit 0 ;;
  *'echo "${AW_SINDRI_PROFILE'*) echo /data/profile/profile.yaml; exit 0 ;;
  *"profile approve"*) exit "$(cat "$POD/approve-rc" 2>/dev/null || echo 0)" ;;
  *" exec -it "*) [ -e "$POD/attach-fail" ] && exit "$(cat "$POD/attach-fail")"; exit 0 ;;
  *" exec "*) [ -e "$POD/down" ] && exit 1
    cat "$POD/control.json"; echo "@@aw-token@@"; cat "$POD/token"; exit 0 ;;
esac
exit 0
EOS
cat > "$WORK/bin/sindri-ui" <<'EOS'
#!/usr/bin/env bash
echo "ui home=$AW_SINDRI_HOME" >> "$CALLS"
echo "${AW_SINDRI_APP_NO_MCP:-unset}" > "$POD/ui.nomcp"
echo $$ > "$POD/ui.pid"
end=$((SECONDS + 120))   # bounded: the test's temp dir going away ends it too
while [ ! -e "$STOP" ] && [ -d "$POD" ] && [ "$SECONDS" -lt "$end" ]; do sleep 0.1; done
EOS
chmod +x "$WORK/bin/kubectl" "$WORK/bin/sindri-ui"
export AW_SINDRI_UI="$WORK/bin/sindri-ui"
mirror_dir() { ls -d "$TMPDIR"/aw-sindri-pod.* 2>/dev/null | head -1; }
MIRROR=""

mode() { stat -c %a "$1" 2>/dev/null || stat -f %Lp "$1"; }
waitfor() {   # <seconds> <command...>: poll until it succeeds
  local n=$(( $1 * 10 )); shift
  while [ "$n" -gt 0 ]; do "$@" 2>/dev/null && return 0; sleep 0.1; n=$((n - 1)); done
  return 1
}
count() { grep -c -- "$1" "$CALLS" || true; }
has_epoch() { [ "$(jq -r .epoch "$(mirror_dir)/control.json")" = "$1" ]; }
has_ui() { grep -q '^ui home=' "$CALLS" && [ -n "$(mirror_dir)" ]; }
has_line() { grep -q -- "$1" "$CALLS"; }
reset() { : > "$CALLS"; rm -rf "$AW_STATE_DIR/sindri-pod" "$TMPDIR"/aw-sindri-pod.* "$POD"/deny-* "$POD/no-pod" "$POD/sindri-ready" "$POD/others"; }
run_connect() {   # <tag> args...: connect in the background with the fakes on the PATH; stdin is one Enter once $WORK/enter exists
  local tag="$1"; shift
  rm -f "$STOP" "$WORK/enter" "$WORK/runs/$tag.rc"
  ( PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" connect "$@" < <(end=$((SECONDS + 60)); while [ ! -e "$WORK/enter" ] && [ -d "$WORK" ] && [ "$SECONDS" -lt "$end" ]; do sleep 0.1; done; echo) \
      > "$WORK/runs/$tag.out" 2> "$WORK/runs/$tag.err"; echo $? > "$WORK/runs/$tag.rc" ) &
  echo $! > "$WORK/runs/$tag.pid"
}
finish() {   # <tag>: close the fake app, wait for the script to end, echo its exit code
  touch "$STOP"
  waitfor 10 test -s "$WORK/runs/$1.rc" || { echo hung; return; }
  cat "$WORK/runs/$1.rc"
}
said() { cat "$WORK/runs/$1.out" "$WORK/runs/$1.err" | grep -c -- "$2" || true; }

# --- the dry run: prints the plan, calls nothing, writes nothing
OUT="$(PATH="/usr/bin:/bin" bash "$DIR/sindri-pod.sh" connect --dry-run --context c1 --namespace n1 --onyx 2>&1)"; RC=$?
check "dry run: exit 0, nothing written, no kubectl needed" "$RC:$([ -e "$AW_STATE_DIR/sindri-pod" ] && echo wrote || echo clean)" "0:clean"
check "dry run: names the exec, the forward, the app and Onyx's attach" \
  "$(printf '%s' "$OUT" | grep -c -e 'exec sindri-0 -c sindri' -e 'port-forward pod/sindri-0' -e 'sindri-ui' -e 'onyx attach')" "6"
OUT="$(env -u AW_SINDRI_POD_RETRIES -u AW_SINDRI_POD_RETRY_WAIT PATH="/usr/bin:/bin" bash "$DIR/sindri-pod.sh" connect --dry-run 2>&1)"
check "dry run: the default is 30 attempts 10 s apart (5 minutes)" "$(printf '%s' "$OUT" | grep -c '30 attempts 10s apart')" "1"

# --- connect: mirror home, forward, app
run_connect a
waitfor 10 has_ui; check "connect: the app starts" "$?" "0"
MIRROR="$(mirror_dir)"
check "connect: control.json holds the forwarded loopback URL, the pod's pid and epoch" \
  "$(jq -c . "$MIRROR/control.json")" '{"url":"http://127.0.0.1:47480","pid":41,"epoch":7}'
check "connect: the app runs with AW_SINDRI_APP_NO_MCP=1 (no MCP registration in ~/.claude.json)" "$(cat "$POD/ui.nomcp")" "1"
check "connect: the token file holds the pod's token" "$(cat "$MIRROR/auth/human.token")" "tok-one"
check "connect: files are 0600, directories 0700" \
  "$(mode "$MIRROR/control.json"):$(mode "$MIRROR/auth/human.token"):$(mode "$MIRROR"):$(mode "$MIRROR/auth")" "600:600:700:700"
check "connect: the app runs on the mirror home" "$(grep '^ui home=' "$CALLS" | head -1)" "ui home=$MIRROR"
check "connect: forwards the pod's own port on the chosen context and namespace" \
  "$(count 'kubectl --context kind-test --namespace sindri port-forward pod/sindri-0 47480:47480')" "1"

# --- the pod restarts: new pid, epoch and token
pod_state 52 8 tok-two 47480
waitfor 10 has_epoch 8; check "restart: the mirror home is rewritten with the new epoch" "$?" "0"
check "restart: pid and token follow" "$(jq -r .pid "$MIRROR/control.json"):$(cat "$MIRROR/auth/human.token")" "52:tok-two"
check "restart: files stay 0600" "$(mode "$MIRROR/control.json"):$(mode "$MIRROR/auth/human.token")" "600:600"

# --- the pod's port changes: the forward is restarted on the new port
pod_state 52 9 tok-two 47481
waitfor 10 has_epoch 9; check "new port: the home is rewritten" "$?" "0"
check "new port: the forward is restarted onto the pod's new port, the local one stays" \
  "$(count 'port-forward pod/sindri-0 47480:47481'):$(jq -r .url "$MIRROR/control.json")" "1:http://127.0.0.1:47480"

# --- pod unreachable for a while: the script retries and recovers
touch "$POD/down"; sleep 0.6; rm -f "$POD/down"; pod_state 53 10 tok-two 47481
waitfor 10 has_epoch 10; check "pod down then up: recovered without exiting" "$?" "0"
check "exit: closing the app ends the script cleanly" "$(finish a)" "0"
check "exit: the script said the app was closed" "$(said a 'the app was closed')" "1"
check "exit: the app process is gone" "$(kill -0 "$(cat "$POD/ui.pid")" 2>/dev/null && echo alive || echo gone)" "gone"
check "connect: prints the connected line" "$(said a 'connected: sindri-0 at http://127.0.0.1:47480; Ctrl-C to stop')" "1"
check "pod down then up: the attempts were announced and the reconnect said" \
  "$([ "$(said a 'pod restarting, reconnecting (attempt 1 of 5)')" -ge 1 ] && [ "$(said a 'reconnected to sindri-0')" = 1 ] && echo yes)" "yes"
check "exit: the mirror home is removed" "$(mirror_dir)" ""

# --- a port-forward that fails once is retried
reset; pod_state 41 7 tok-one 47480; touch "$POD/pf-fail-once"
run_connect b
waitfor 10 has_ui; check "forward retry: the app still starts" "$?" "0"
check "forward retry: port-forward ran twice" "$(count 'port-forward pod/sindri-0')" "2"
check "forward retry: exit 0" "$(finish b)" "0"

# --- --port picks the local port, --pod the pod
reset
run_connect c --port 18480 --pod sindri-9
waitfor 10 has_ui
check "--port: the forward maps the chosen local port to the pod's, for the chosen pod" "$(count 'port-forward pod/sindri-9 18480:47480')" "1"
check "--port: control.json carries the local port" "$(jq -r .url "$(mirror_dir)/control.json")" "http://127.0.0.1:18480"
finish c >/dev/null

# --- --onyx: forward the pod's Onyx port, run onyx attach in the pod
reset
run_connect d --onyx
waitfor 10 has_line 'exec -it'; waitfor 10 has_ui
check "--onyx: attach runs in the sindri container with -it, with the forwarded address" \
  "$(count 'exec -it sindri-0 -c sindri -- env AW_SINDRI_ONYX_FORWARD_URL=http://127.0.0.1:43000 sindri onyx attach$')" "1"
check "--onyx: onyx up is never run" "$(count 'onyx up')" "0"
SEQ="$(awk '
  /exec sindri-0 -c sindri -- sh/ && !a {print "read"; a=1} /port-forward pod\/sindri-0 47480/ && !b {print "forward"; b=1}
  /exec -it/ {print "attach"}' "$CALLS" | paste -sd, -)"
check "--onyx: pod read, owner forward, then attach" "$SEQ" "read,forward,attach"
waitfor 10 has_line 'port-forward pod/sindri-0 43000:43000'; check "--onyx: the Onyx port is forwarded from the pod" "$?" "0"
check "--onyx: no Service forward" "$(count 'port-forward svc/')" "0"
check "--onyx: the forwarded address is printed" "$(said d "Onyx's page: http://127.0.0.1:43000")" "1"
check "--onyx: the app is still connected after attach" "$(jq -r .epoch "$(mirror_dir)/control.json")" "7"
check "--onyx: success ends with exit 0" "$(finish d)" "0"

# --- --onyx: attach fails; its exit code propagates with a clear text, the app stays up meanwhile
reset; echo 3 > "$POD/attach-fail"
run_connect e --onyx
waitfor 10 has_ui; waitfor 10 has_line 'exec -it'
sleep 0.5
check "--onyx failure: the app was still started" "$(count '^ui home=')" "1"
check "--onyx failure: prints attach exited 3 and says the app stays connected" "$(said e 'attach exited 3; the app stays connected')" "1"
check "--onyx failure: the script's own exit stays 0 (3 is only for gave up reconnecting)" "$(finish e)" "0"
rm -f "$POD/attach-fail"

# --- --onyx: Ctrl-C in attach (130) stops attach only
reset; echo 130 > "$POD/attach-fail"
run_connect f --onyx
waitfor 10 has_ui; sleep 0.5
check "--onyx Ctrl-C: attach stopped, app stays" "$(said f 'the app stays connected')" "1"
check "--onyx Ctrl-C: exit 0" "$(finish f)" "0"
rm -f "$POD/attach-fail"

# --- refusals: one line each, exit 2
conn() { PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" connect "$@" 2>&1; }
lines() { printf '%s\n' "$1" | grep -c .; }
reset
OUT="$(PATH="/usr/bin:/bin" bash "$DIR/sindri-pod.sh" connect 2>&1)"; RC=$?
check "no kubectl: exit 2 with one line" "$RC:$(lines "$OUT"):$(printf '%s' "$OUT" | grep -c 'kubectl is not on the PATH')" "2:1:1"
OUT="$(AW_SINDRI_UI="$WORK/nope" conn)"; RC=$?
check "no sindri-ui: exit 2, one line naming install-cs.sh, nothing forwarded" \
  "$RC:$(lines "$OUT"):$(printf '%s' "$OUT" | grep -c 'install-cs.sh'):$(count port-forward)" "2:1:1:0"
touch "$POD/deny-pods_exec"
OUT="$(conn)"; RC=$?
check "no exec right: exit 2, one line naming the verb" "$RC:$(lines "$OUT"):$(printf '%s' "$OUT" | grep -c "create pods/exec")" "2:1:1"
rm -f "$POD/deny-pods_exec"; touch "$POD/deny-pods_portforward"
OUT="$(conn)"; RC=$?
check "no port-forward right: exit 2 naming pods/portforward" "$RC:$(printf '%s' "$OUT" | grep -c "create pods/portforward")" "2:1"
rm -f "$POD/deny-pods_portforward"
echo false > "$POD/sindri-ready"; echo "onyx-api=false " > "$POD/others"; echo '{"waiting":{"reason":"ContainerCreating"}}' > "$POD/state"
OUT="$(AW_SINDRI_POD_RETRIES=2 conn)"; RC=$?
check "sindri container not ready: waits, prints its state each time, then exit 2 naming the state and the logs command" \
  "$RC:$(printf '%s' "$OUT" | grep -c 'waiting for the sindri container of pod sindri-0: waiting: ContainerCreating (attempt 2 of 2)'):$(printf '%s' "$OUT" | grep -c 'is not running (state: waiting: ContainerCreating); see: kubectl logs sindri-0 -c sindri (other containers, for information: onyx-api=false)')" "2:1:1"
echo '{"running":{"startedAt":"x"}}' > "$POD/state"
OUT="$(AW_SINDRI_POD_RETRIES=1 conn)"
check "sindri container running but not ready: the state says so" "$(printf '%s' "$OUT" | grep -c 'state: running, not ready')" "1"
echo '{"terminated":{"reason":"Error"}}' > "$POD/state"
OUT="$(AW_SINDRI_POD_RETRIES=1 conn)"
check "sindri container terminated: the state names the reason" "$(printf '%s' "$OUT" | grep -c 'state: terminated: Error')" "1"
rm -f "$POD/sindri-ready" "$POD/state"
reset; echo "onyx-api=false " > "$POD/others"
run_connect h
waitfor 10 has_ui
check "pod not Ready but the sindri container is: connects, Onyx not answering is a note" \
  "$(said h 'Onyx: not answering, see sindri doctor'):$(count '^ui home=')" "1:1"
finish h >/dev/null
reset
rm -f "$POD/others"
touch "$POD/no-pod"
OUT="$(AW_SINDRI_POD_RETRIES=1 conn)"; RC=$?
check "pod missing: exit 2, state not found" "$RC:$(printf '%s' "$OUT" | grep -c 'state: not found')" "2:1"
rm -f "$POD/no-pod"
if command -v python3 >/dev/null 2>&1; then
  python3 -I -c 'import socket,sys,time
s=socket.socket(); s.bind(("127.0.0.1",48999)); s.listen(1); open(sys.argv[1],"w").write("up"); time.sleep(120)' "$WORK/listening" &
  echo $! > "$WORK/runs/listener.pid"
  waitfor 10 test -e "$WORK/listening"
  OUT="$(conn --port 48999)"; RC=$?
  check "local port taken: exit 2, one line naming the port and --port" \
    "$RC:$(lines "$OUT"):$(printf '%s' "$OUT" | grep -c 'local port 48999 is taken; pick another with --port'):$(count port-forward)" "2:1:1:0"
  kill "$(cat "$WORK/runs/listener.pid")" 2>/dev/null
  python3 -I -c 'import socket,sys,time
s=socket.socket(); s.bind(("127.0.0.1",48998)); s.listen(1); open(sys.argv[1],"w").write("up"); time.sleep(120)' "$WORK/listening2" &
  echo $! > "$WORK/runs/listener2.pid"
  waitfor 10 test -e "$WORK/listening2"
  reset
  OUT="$(AW_SINDRI_POD_ONYX_PORT=48998 conn --onyx)"; RC=$?
  check "Onyx port taken: exit 2, one line, attach never runs" \
    "$RC:$(printf '%s' "$OUT" | grep -c 'local port 48998 is in use: stop what uses it, then run this again'):$(count 'exec -it'):$(count '^ui home=')" "2:1:0:0"
  kill "$(cat "$WORK/runs/listener2.pid")" 2>/dev/null
fi

# --- the default Onyx port is 8081
OUT="$(env -u AW_SINDRI_POD_ONYX_PORT PATH="/usr/bin:/bin" bash "$DIR/sindri-pod.sh" connect --dry-run --onyx --adopt 2>&1)"
check "--onyx dry run: the Onyx port comes from the pod, --adopt is passed on" \
  "$(printf '%s' "$OUT" | grep -c -e 'AW_SINDRI_ONYX_URL' -e 'pod/sindri-0 <onyx port>:<onyx port>' -e 'onyx attach --adopt')" "3"

# --- --check: every prerequisite, exit 0 or 2, nothing started
reset
OUT="$(conn --check)"; RC=$?
check "--check: all hold, exit 0, an ok line for each" "$RC:$(printf '%s' "$OUT" | grep -c '^sindri-pod: ok:')" "0:7"
check "--check: forwards nothing, starts nothing, writes no mirror home" \
  "$(count port-forward):$(count '^ui home='):$(mirror_dir)" "0:0:"
touch "$POD/deny-pods_exec"; echo false > "$POD/sindri-ready"
OUT="$(AW_SINDRI_UI="$WORK/nope" conn --check)"; RC=$?
check "--check: reports every failure, not just the first, exit 2" \
  "$RC:$(printf '%s' "$OUT" | grep -c -e 'install-cs.sh' -e 'create pods/exec' -e 'sindri container of pod sindri-0 is not running')" "2:3"
rm -f "$POD/deny-pods_exec" "$POD/sindri-ready"
reset
OUT="$(conn --check --onyx)"; RC=$?
check "--check --onyx: also checks the Onyx port, runs no attach" "$RC:$(printf '%s' "$OUT" | grep -c 'Onyx) is free'):$(count 'exec -it')" "0:1:0"

# --- cleanup: TERM (what Ctrl-C does) removes the mirror home and the port-forward
reset
run_connect f
waitfor 10 has_ui; M="$(mirror_dir)"
check "cleanup: the mirror home is 0700 and holds the owner pid" "$(mode "$M"):$(test -s "$M/owner.pid" && echo pid)" "700:pid"
kill -TERM "$(cat "$M/owner.pid")"
waitfor 10 test -s "$WORK/runs/f.rc"; check "cleanup: TERM ends the script with exit 0" "$(cat "$WORK/runs/f.rc")" "0"
check "cleanup: TERM stops the app the script opened" "$(kill -0 "$(cat "$POD/ui.pid")" 2>/dev/null && echo alive || echo gone)" "gone"
check "cleanup: the mirror home is gone" "$(mirror_dir)" ""
touch "$STOP"

# --- stale mirror homes of dead runs are removed on the next run; live and unmarked ones are left
reset
mkdir -p "$TMPDIR/aw-sindri-pod.stale" "$TMPDIR/aw-sindri-pod.live" "$TMPDIR/aw-sindri-pod.plain"
echo 999999 > "$TMPDIR/aw-sindri-pod.stale/owner.pid"; echo $$ > "$TMPDIR/aw-sindri-pod.live/owner.pid"
run_connect g
waitfor 10 has_ui
check "stale: a dead run's home is removed" "$([ -d "$TMPDIR/aw-sindri-pod.stale" ] && echo kept || echo removed)" "removed"
check "stale: a live run's and an unmarked directory are left" \
  "$([ -d "$TMPDIR/aw-sindri-pod.live" ] && [ -d "$TMPDIR/aw-sindri-pod.plain" ] && echo kept)" "kept"
finish g >/dev/null

# --- the retry loop is bounded: the pod never answers
reset; touch "$POD/down"
OUT="$(AW_SINDRI_POD_RETRIES=2 conn)"; RC=$?
check "pod never reachable: exit 3 after the attempts" "$RC:$(printf '%s' "$OUT" | grep -c "can't reach sindri-0")" "3:1"
check "pod never reachable: attempts 1 and 2 announced, no third" \
  "$(printf '%s' "$OUT" | grep -c -e 'reconnecting (attempt 1 of 2)' -e 'reconnecting (attempt 2 of 2)'):$(printf '%s' "$OUT" | grep -c 'attempt 3')" "2:0"
check "pod never reachable: the mirror home is removed too" "$(mirror_dir)" ""
rm -f "$POD/down"
conn --bogus >/dev/null; check "unknown argument: exit 2" "$?" "2"
bash "$DIR/sindri-pod.sh" >/dev/null 2>&1; check "no verb: exit 2" "$?" "2"

# --- profile edit: copy out, edit, validate, put back, approve
cat > "$WORK/bin/ed" <<'EOS'
#!/usr/bin/env bash
echo "editor $1" >> "$CALLS"
[ -e "$POD/ed-change" ] && ! grep -q "^index.graph.enabled: true" "$1" && echo "index.graph.enabled: true" >> "$1"
exit 0
EOS
chmod +x "$WORK/bin/ed"
printf 'schemaVersion: 1\nuser: me\n' > "$POD/profile.yaml"
pedit() { printf '%s' "${PEDIT_IN:-}" | env EDITOR="$WORK/bin/ed" PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" profile edit "$@" 2>&1; }
pedit_reset() { reset; rm -f "$POD/ed-change" "$POD/validate-rc" "$POD/approve-rc" "$POD/uploaded" "$POD/validated"
  printf 'schemaVersion: 1\nuser: me\n' > "$POD/profile.yaml"; }
seq_of() { awk '/sindri profile validate/ {print "validate"} / mv / {print "move"} /profile approve/ {print "approve"}' "$CALLS" | paste -sd, -; }

pedit_reset
OUT="$(pedit)"; RC=$?
check "profile edit unchanged: exit 0, says so, no validate or approve" \
  "$RC:$(printf '%s' "$OUT" | grep -c 'profile unchanged; nothing to validate or approve'):$(seq_of)" "0:1:"
check "profile edit: the profile is read with exec cat at the pod's profile path" "$(count 'exec sindri-0 -c sindri -- cat /data/profile/profile.yaml')" "1"

pedit_reset; touch "$POD/ed-change"
OUT="$(pedit)"; RC=$?
check "profile edit changed and valid: validate, then put back, then approve" "$RC:$(seq_of)" "0:validate,move,approve"
check "profile edit: approve runs with -it in the sindri container" "$(count 'exec -it sindri-0 -c sindri -- sindri profile approve')" "1"
check "profile edit: the pod's profile is the edited one" "$(tail -1 "$POD/profile.yaml")" "index.graph.enabled: true"
check "profile edit: validate ran on the temp copy, not the live path" \
  "$(count 'AW_SINDRI_PROFILE="$1" sindri profile validate _ /data/profile/profile.yaml.edit-')" "1"
check "profile edit: the final line says what changed and what is next" \
  "$(printf '%s' "$OUT" | tail -1 | grep -c 'profile changed (1 lines differ) and approved in sindri-0; next:')" "1"
check "profile edit: the local copy is removed on success" "$(ls "$AW_STATE_DIR"/sindri-pod/*/profile-edit 2>/dev/null | head -1)" ""

pedit_reset; touch "$POD/ed-change"; echo 2 > "$POD/validate-rc"
OUT="$(pedit)"; RC=$?
check "profile edit invalid and left as it was: exit 3, errors shown, editor reopened once, no move, no approve" \
  "$RC:$(printf '%s' "$OUT" | grep -c 'SND-PROFILE-001 bad key'):$(count '^editor '):$(seq_of)" "3:1:2:validate"
KEPT="$(ls "$AW_STATE_DIR"/sindri-pod/*/profile-edit/profile.yaml 2>/dev/null | head -1)"
check "profile edit invalid: your file is kept without the error comments, and its path printed" \
  "$([ -n "$KEPT" ] && tail -1 "$KEPT"):$(grep -c '^# sindri-pod: ' "$KEPT"):$(printf '%s' "$OUT" | grep -c -F "your file is kept at $KEPT")" "index.graph.enabled: true:0:1"
check "profile edit invalid: the pod's profile is unchanged" "$(wc -l < "$POD/profile.yaml" | tr -d ' ')" "2"
check "profile edit invalid: the temp copy in the pod is removed" "$(wc -l < "$POD/rm-calls" | tr -d ' ')" "1"

# the editor reopens with the error as comments on top; a fix then goes through
cat > "$WORK/bin/ed-fix" <<'EOS'
#!/usr/bin/env bash
echo "editor $1" >> "$CALLS"
if grep -q '^# sindri-pod: ' "$1"; then cp "$1" "$POD/ed-saw"; rm -f "$POD/validate-rc"; echo "fixed: true" >> "$1"; else echo "bad: true" >> "$1"; fi
exit 0
EOS
chmod +x "$WORK/bin/ed-fix"
pedit_reset; echo 2 > "$POD/validate-rc"; rm -f "$POD/ed-saw"
OUT="$(printf '' | EDITOR="$WORK/bin/ed-fix" PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" profile edit 2>&1)"; RC=$?
check "profile edit invalid then fixed: the editor saw the error, the fix is validated, put back and approved" \
  "$RC:$(grep -c '^# sindri-pod: SND-PROFILE-001 bad key' "$POD/ed-saw"):$(seq_of)" "0:1:validate,validate,move,approve"
check "profile edit: the error comments are not uploaded" "$(grep -c '^# sindri-pod' "$POD/profile.yaml")" "0"

pedit_reset; touch "$POD/ed-change"; echo 5 > "$POD/approve-rc"
OUT="$(pedit)"; RC=$?
check "profile edit approve refused: exit 1, order held" "$RC:$(seq_of)" "1:validate,move,approve"
KEPT="$(ls "$AW_STATE_DIR"/sindri-pod/*/profile-edit/profile.yaml 2>/dev/null | head -1)"
check "profile edit approve refused: the copy is kept and the next step named" \
  "$([ -n "$KEPT" ] && echo kept):$(printf '%s' "$OUT" | grep -c 'in place in sindri-0 but not approved')" "kept:1"
pedit_reset; touch "$POD/ed-change"
OUT="$(pedit --no-approve)"; RC=$?
check "profile edit --no-approve: validate and move, no approve, exit 0, says not approved" \
  "$RC:$(seq_of):$(printf '%s' "$OUT" | tail -1 | grep -c 'profile changed, not yet approved (1 lines differ, valid, in place in sindri-0); next: kubectl exec -it sindri-0 -c sindri -- sindri profile approve')" "0:validate,move:1"
check "profile edit --no-approve: the pod's profile is the edited one" "$(tail -1 "$POD/profile.yaml")" "index.graph.enabled: true"
OUT="$(PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" connect --no-approve 2>&1)"; check "--no-approve is for profile edit only: connect refuses it" "$?" "2"
pedit_reset
OUT="$(pedit --port 1)"; RC=$?; check "profile edit: connect-only flags are a usage error" "$RC" "2"



# --- profile edit: a conflict (the pod's copy changed meanwhile) replaces nothing, exit 4
cat > "$WORK/bin/ed-race" <<'EOS'
#!/usr/bin/env bash
echo "editor $1" >> "$CALLS"
echo "index.graph.enabled: true" >> "$1"
printf 'schemaVersion: 1\nuser: me\nrepos: [a]\n' > "$POD/profile.yaml"   # a repo add lands while the editor is open
exit 0
EOS
chmod +x "$WORK/bin/ed-race"
pedit_reset
OUT="$(printf '' | EDITOR="$WORK/bin/ed-race" PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" profile edit 2>&1)"; RC=$?
KEPT="$(ls "$AW_STATE_DIR"/sindri-pod/*/profile-edit/profile.yaml 2>/dev/null | head -1)"
check "profile edit conflict: exit 4, nothing replaced, no approve" "$RC:$(grep -c 'repos: \[a\]' "$POD/profile.yaml"):$(seq_of)" "4:1:validate,move"
check "profile edit conflict: the text names the differing top-level keys and where the edit is kept" \
  "$(printf '%s' "$OUT" | grep -c -F "The profile on the pod changed while you were editing (repos). Nothing was replaced; your edit is kept at $KEPT. Run profile edit again and make the change on top of the new copy.")" "1"
check "profile edit conflict: your edit is kept" "$(tail -1 "$KEPT")" "index.graph.enabled: true"

# --- profile edit: no profile in the pod yet: sindri profile init runs first, then it is opened
pedit_reset; rm -f "$POD/profile.yaml"; touch "$POD/ed-change"
OUT="$(pedit --no-approve)"; RC=$?
check "profile edit with no profile: init runs in the pod with -it, before the read and the editor" \
  "$RC:$(awk '/sindri profile init/ {print "init"} /exec sindri-0 -c sindri -- cat/ {print "read"} /^editor/ {print "edit"}' "$CALLS" | paste -sd, -)" "0:init,read,edit"
check "profile edit with no profile: init is exec -it" "$(count 'exec -it sindri-0 -c sindri -- sindri profile init')" "1"
check "profile edit with a profile: init never runs" \
  "$(pedit_reset; pedit >/dev/null; count 'profile init')" "0"

# --- profile edit: usage and unreachable pod are exit 2
pedit_reset; touch "$POD/no-pod"
pedit >/dev/null; check "profile edit: the pod unreachable is exit 2" "$?" "2"
rm -f "$POD/no-pod"
pedit --onyx >/dev/null; check "profile edit: --onyx is a usage error" "$?" "2"
pedit_reset; OUT="$(pedit --pod p-1 --namespace ns1 --context c9)"
check "profile edit takes --pod, --namespace and --context" "$(count 'kubectl --context c9 --namespace ns1 exec p-1 -c sindri -- cat')" "1"
pedit_reset; pedit --release rel >/dev/null
check "profile edit --release rel is pod rel-0" "$(count 'exec rel-0 -c sindri -- cat')" "1"
pedit --pod a --release b >/dev/null; check "--pod with --release is a usage error" "$?" "2"
check "profile edit: with no EDITOR it falls back to vi (VISUAL is not read)" \
  "$(grep -c 'VISUAL' "$DIR/sindri-pod.sh" | head -1):$(grep -c 'editor="${EDITOR:-vi}"' "$DIR/sindri-pod.sh")" "0:1"

# --- the thin verbs: repo add, approve, doctor
thin_reset() { reset; rm -f "$POD/wrap-rc" "$POD/approve-rc"; }
tv() { PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" "$@" </dev/null 2>&1; }
thin_reset
tv repo add web git@example.com:a/web.git >/dev/null; RC=$?
check "repo add: exec -it of sindri repo add --url --name in the sindri container, exit 0" \
  "$RC:$(count 'exec -it sindri-0 -c sindri -- sindri repo add --url git@example.com:a/web.git --name web$')" "0:1"
tv repo add web u --credential env:TOK --release rel --namespace n1 --context c2 >/dev/null
check "repo add --credential: passed on; --release, --namespace and --context are taken" \
  "$(count 'kubectl --context c2 --namespace n1 exec -it rel-0 -c sindri -- sindri repo add --url u --name web --credential env:TOK$')" "1"
echo 7 > "$POD/wrap-rc"; tv repo add web u >/dev/null; check "repo add: the wrapped command's exit code is passed through" "$?" "7"
tv repo add web >/dev/null; check "repo add with one argument: usage, exit 2" "$?" "2"
tv repo add a b c >/dev/null; check "repo add with three arguments: usage, exit 2" "$?" "2"
tv repo add a b --no-approve >/dev/null; check "repo add: --no-approve is a usage error" "$?" "2"
tv repo list >/dev/null; check "repo with another word: usage, exit 2" "$?" "2"
thin_reset
tv approve --pod p-3 >/dev/null; RC=$?
check "approve: exec -it of sindri profile approve in the sindri container" "$RC:$(count 'exec -it p-3 -c sindri -- sindri profile approve$')" "0:1"
echo 9 > "$POD/approve-rc"; tv approve >/dev/null; check "approve: the wrapped exit code is passed through" "$?" "9"
rm -f "$POD/approve-rc"
tv doctor --release r2 >/dev/null; RC=$?
check "doctor: exec -it of sindri doctor in pod <release>-0" "$RC:$(count 'exec -it r2-0 -c sindri -- sindri doctor$')" "0:1"
echo 1 > "$POD/wrap-rc"; tv doctor >/dev/null; check "doctor: the wrapped exit code is passed through" "$?" "1"
rm -f "$POD/wrap-rc"
touch "$POD/no-pod"
for v in "approve" "doctor" "repo add a b"; do
  tv $v >/dev/null; check "$v: the pod unreachable is exit 2" "$?" "2"
done
rm -f "$POD/no-pod"
OUT="$(PATH="/usr/bin:/bin" bash "$DIR/sindri-pod.sh" doctor 2>&1)"; check "doctor: no kubectl is exit 2" "$?" "2"
tv doctor --port 1 >/dev/null; check "doctor: connect-only flags are a usage error" "$?" "2"
tv doctor extra >/dev/null; check "doctor: a stray argument is a usage error" "$?" "2"

# --- connect takes --release too
reset
run_connect r --release rel
waitfor 10 has_ui
check "connect --release rel: the forward goes to pod rel-0" "$(count 'port-forward pod/rel-0 47480:47480')" "1"
finish r >/dev/null

# --- connect --onyx: the port comes from the pod's AW_SINDRI_ONYX_URL, the override warns, 8081 is last
reset; echo http://127.0.0.1:8123 > "$POD/onyx-url"
run_connect o1 --onyx
waitfor 10 has_line 'exec -it'; sleep 0.3
check "--onyx: the pod's onyx.port is forwarded to the same local port (no override, here the env one is 43000)" \
  "$(count 'port-forward pod/sindri-0 43000:8123'):$(said o1 'warning: AW_SINDRI_POD_ONYX_PORT=43000: sign-up redirects and connector OAuth may land on port 8123, not 43000')" "1:1"
finish o1 >/dev/null
reset
( export -n AW_SINDRI_POD_ONYX_PORT; unset AW_SINDRI_POD_ONYX_PORT
  rm -f "$WORK/enter" "$STOP"
  PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" connect --onyx > "$WORK/runs/o2.out" 2> "$WORK/runs/o2.err" < /dev/null & echo $! > "$WORK/runs/o2.pid"
  waitfor 10 has_line 'exec -it'; sleep 0.3
  echo "$(count 'port-forward pod/sindri-0 8123:8123'):$(grep -c 'warning:' "$WORK/runs/o2.err" || true):$(grep -c "Onyx's page: http://127.0.0.1:8123" "$WORK/runs/o2.err")" > "$WORK/o2.res"
  touch "$STOP"; wait )
check "--onyx without the override: the pod's port on both sides, no warning, the page printed before attach" "$(cat "$WORK/o2.res")" "1:0:1"
reset; echo http://onyx.local > "$POD/onyx-url"
( unset AW_SINDRI_POD_ONYX_PORT; rm -f "$STOP"
  PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" connect --onyx --dry-run >/dev/null 2>&1
  PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" connect --onyx > "$WORK/runs/o3.out" 2> "$WORK/runs/o3.err" < /dev/null & echo $! > "$WORK/runs/o3.pid"
  waitfor 10 has_line 'exec -it'; sleep 0.3
  echo "$(count 'port-forward pod/sindri-0 8081:8081')" > "$WORK/o3.res"; touch "$STOP"; wait )
check "--onyx: a URL with no port falls back to 8081" "$(cat "$WORK/o3.res")" "1"
reset; echo http://127.0.0.1:43000 > "$POD/onyx-url"

# --- connect --onyx --adopt passes --adopt to attach
reset
run_connect ad --onyx --adopt
waitfor 10 has_line 'exec -it'; sleep 0.3
check "--onyx --adopt: attach gets --adopt" "$(count 'sindri onyx attach --adopt$')" "1"
finish ad >/dev/null
reset
OUT="$(conn --adopt)"; RC=$?; check "--adopt without --onyx: usage, exit 2" "$RC" "2"

# --- connect --onyx with Onyx off (AW_SINDRI_ONYX_URL unset in the pod): refused, exit 2, nothing started
reset; rm -f "$POD/onyx-url"
OUT="$(conn --onyx)"; RC=$?
check "--onyx with Onyx off: exit 2, the spec's text, no app, no forward, no attach" \
  "$RC:$(printf '%s' "$OUT" | grep -c 'Onyx is off in this install (onyx.enabled=false); use connect.'):$(count '^ui home='):$(count port-forward):$(count 'exec -it')" "2:1:0:0:0"
OUT="$(conn --onyx --check)"; RC=$?
check "--onyx --check with Onyx off: reports it, exit 2" "$RC:$(printf '%s' "$OUT" | grep -c 'use connect.')" "2:1"
echo http://127.0.0.1:43000 > "$POD/onyx-url"

# --- the Onyx-not-answering line, and the plain-output rule
reset; echo "onyx-api=false " > "$POD/others"
run_connect na --onyx
waitfor 10 has_line 'exec -it'; sleep 0.3
check "connect --onyx with Onyx's containers not ready: it carries on and says Onyx is not answering" \
  "$(said na 'Onyx: not answering, see sindri doctor'):$(count '^ui home=')" "1:1"
check "output is plain: no escape characters when stdout is not a terminal" \
  "$(cat "$WORK/runs/na.out" "$WORK/runs/na.err" | grep -c "$(printf '\033')" || true)" "0"
finish na >/dev/null
rm -f "$POD/others"
OUT="$(NO_COLOR=1 PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" profile edit --pod nope --bogus 2>&1)"
check "usage output is plain too" "$(printf '%s' "$OUT" | grep -c "$(printf '\033')" || true)" "0"

# --- the wait for the sindri container: the first not-ready, then ready, connects
reset; echo false > "$POD/sindri-ready"; echo '{"waiting":{"reason":"ContainerCreating"}}' > "$POD/state"
run_connect w
sleep 0.8; has_ui; check "wait: the app does not start while the sindri container is not ready" "$?" "1"
rm -f "$POD/sindri-ready"
waitfor 15 has_ui; check "wait: it starts once the container is ready" "$?" "0"
check "wait: the state was printed while waiting" "$([ "$(said w 'waiting for the sindri container of pod sindri-0: waiting: ContainerCreating')" -ge 1 ] && echo yes)" "yes"
finish w >/dev/null; rm -f "$POD/state"
OUT="$(env -u AW_SINDRI_POD_RETRIES -u AW_SINDRI_POD_RETRY_WAIT PATH="/usr/bin:/bin" bash "$DIR/sindri-pod.sh" connect --dry-run 2>&1)"
check "wait: the default is 40 attempts 15 s apart (10 minutes)" "$(printf '%s' "$OUT" | grep -c 'up to 40 attempts 15s apart')" "1"

# --- no process of this run is left: each carries this run's STOP path in its environment (an orphan reparented to
# systemd keeps it); the loops are bounded, so give them a moment to end on their own, then fail on any that remain
left() {   # runs in this shell (no subshell of its own to find), results in $WORK/left
  local d p
  : > "$WORK/left"
  for d in /proc/[0-9]*; do
    p="${d#/proc/}"; [ "$p" != "$$" ] || continue
    tr '\0' '\n' < "$d/environ" 2>/dev/null | grep -qx "STOP=$STOP" && echo "$p" >> "$WORK/left"
  done
}
touch "$STOP"
for _ in $(seq 1 50); do left; [ -s "$WORK/left" ] || break; sleep 0.1; done
LEFT="$(tr '\n' ' ' < "$WORK/left")"
check "no process of this run is left running" "$LEFT" ""
for p in $LEFT; do kill "$p" 2>/dev/null; done

exit "$fail"
