#!/usr/bin/env bash
# Tests for scripts/test-tmux.sh, the helper every tmux-using test sources. Every tmux here runs through the helper (ttmux or its
# PATH shim) in a private socket; a fake tmux stands in for the "resolves to the default socket" case.
set -u
unset TMUX
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
HELPER="$DIR/scripts/test-tmux.sh"
fail=0
check() { if [ "$2" == "$3" ]; then echo "ok - $1"; else echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1; fi; }
command -v tmux > /dev/null || { echo "ok - skipped: tmux not installed"; exit 0; }
W="$(mktemp -d)"; trap 'rm -rf "$W"' EXIT

# 1. Sourced from inside a tmux client (TMUX set), the helper unsets it and works in its own temp dir.
OUT="$( TMUX=/tmp/tmux-1000/default,1,0 bash -c '
  . "'"$HELPER"'" || exit 2
  echo "tmux_unset=${TMUX:-unset}"
  echo "tmpdir_ok=$([[ "$TMUX_TMPDIR" == "$AW_TEST_TMUX_DIR"/* && "$AW_TEST_TMUX_DIR" == */tmp.* ]] && echo yes || echo no)"
  ttmux new-session -d -s one "sleep 30"
  echo "sessions=$(ttmux list-sessions -F "#S")"
  echo "socket_ok=$([[ "$(ttmux display-message -p -t one "#{socket_path}")" == "$AW_TEST_TMUX_DIR"/* ]] && echo yes || echo no)"
  echo "plain_sees=$(tmux list-sessions -F "#S")"
  echo "plain_socket_same=$([ "$(tmux display-message -p -t one "#{socket_path}")" = "$(ttmux display-message -p -t one "#{socket_path}")" ] && echo yes || echo no)"
  echo "name=$AW_TEST_TMUX"
  echo "$AW_TEST_TMUX_DIR" > "'"$W"'/dir"
' 2>&1 )"
check "TMUX is unset after sourcing" "$(grep -c '^tmux_unset=unset$' <<< "$OUT")" "1"
check "TMUX_TMPDIR is under a mktemp dir of the helper" "$(grep -c '^tmpdir_ok=yes$' <<< "$OUT")" "1"
check "ttmux makes sessions on its own socket" "$(grep -c '^sessions=one$' <<< "$OUT")" "1"
check "the socket lives inside the temp dir, not /tmp/tmux-<uid>/default" "$(grep -c '^socket_ok=yes$' <<< "$OUT")" "1"
check "plain tmux (the shim) reaches the same private server" "$(grep -c '^plain_sees=one$' <<< "$OUT"):$(grep -c '^plain_socket_same=yes$' <<< "$OUT")" "1:1"
check "the socket name is unique per run (t1-<pid>-<random>)" "$(grep -cE '^name=t1-[0-9]+-[0-9]+$' <<< "$OUT")" "1"
check "the EXIT trap removed the temp dir" "$(test -e "$(cat "$W/dir")" && echo present || echo gone)" "gone"

# 2. The shim refuses when TMUX is set again, or TMUX_TMPDIR leaves the mktemp dir.
OUT="$(bash -c '. "'"$HELPER"'" || exit 2; TMUX=x tmux list-sessions; echo "rc=$?"' 2>&1)"
check "the shim refuses to run tmux with TMUX set" "$(grep -c 'TMUX is set: refusing' <<< "$OUT"):$(grep -c '^rc=2$' <<< "$OUT")" "1:1"
OUT="$(bash -c '. "'"$HELPER"'" || exit 2; TMUX_TMPDIR=/tmp tmux list-sessions; echo "rc=$?"' 2>&1)"
check "the shim refuses a TMUX_TMPDIR outside a mktemp dir" "$(grep -c 'not this run' <<< "$OUT"):$(grep -c '^rc=2$' <<< "$OUT")" "1:1"
OUT="$(bash -c '. "'"$HELPER"'" || exit 2; TMUX_TMPDIR="$(mktemp -d)" tmux list-sessions; echo "rc=$?"' 2>&1)"
check "the shim refuses another mktemp dir (only its own is accepted)" "$(grep -c 'not this run' <<< "$OUT"):$(grep -c '^rc=2$' <<< "$OUT")" "1:1"
OUT="$(bash -c '. "'"$HELPER"'" || exit 2; tmux -S "$AW_TEST_TMUX_DIR/x.sock" new-session -d -s z; echo "rc=$?"; tmux -L other -S /tmp/tmux-1000/default list-sessions; echo "rc=$?"; ls "$AW_TEST_TMUX_DIR/x.sock" 2>&1 | grep -c "No such"' 2>&1)"
check "the shim refuses -S anywhere, creating nothing" "$(grep -c 'refused: use -L' <<< "$OUT"):$(grep -c '^rc=2$' <<< "$OUT"):$(tail -1 <<< "$OUT")" "2:2:1"

# 3. A tmux whose socket resolves to the default socket is refused (exit 2), and kill-server is never sent.
mkdir -p "$W/fake"
cat > "$W/fake/tmux" <<EOF
#!/bin/sh
echo "\$@" >> "$W/fake-calls"
case "\$*" in *display-message*) echo /tmp/tmux-\$(id -u)/default ;; esac
exit 0
EOF
chmod +x "$W/fake/tmux"
OUT="$(PATH="$W/fake:$PATH" bash -c '. "'"$HELPER"'"; echo "sourced rc=$?"' 2>&1)"
check "a socket outside the temp dir: refused with rc 2" "$(grep -c 'does not resolve inside' <<< "$OUT"):$(grep -c 'sourced rc=2' <<< "$OUT")" "1:1"
check "the refusal killed only the marker session, never the server" "$(grep -c 'kill-session -t awtest-guard' "$W/fake-calls"):$(grep -c 'kill-server' "$W/fake-calls" | tr -d '\n')" "1:0"
: > "$W/fake-calls"

# 4. A preset socket name is kept; 'default' and odd names are refused.
OUT="$(AW_TEST_TMUX=t1 bash -c '. "'"$HELPER"'" || exit 2; echo "name=$AW_TEST_TMUX"; ttmux display-message -p -t awtest-guard "x" > /dev/null 2>&1; echo "path=$(ttmux new-session -d -s q \; display-message -p "#{socket_path}")"' 2>&1)"
check "a preset name (the team under test) is kept" "$(grep -c '^name=t1$' <<< "$OUT"):$(grep -cE '^path=.*/tmp\..*/tmux/tmux-[0-9]+/t1$' <<< "$OUT")" "1:1"
OUT="$(AW_TEST_TMUX=default bash -c '. "'"$HELPER"'"; echo "rc=$?"' 2>&1)"; check "the name 'default' is refused" "$(grep -c '^rc=2$' <<< "$OUT")" "1"
OUT="$(AW_TEST_TMUX='A b' bash -c '. "'"$HELPER"'"; echo "rc=$?"' 2>&1)"; check "an odd name is refused" "$(grep -c '^rc=2$' <<< "$OUT")" "1"

# 5. ttmux_cleanup stops the server it started.
OUT="$(bash -c '. "'"$HELPER"'" || exit 2; ttmux new-session -d -s keep "sleep 30"; d="$AW_TEST_TMUX_DIR"; ttmux_cleanup; echo "dir=$(test -e "$d" && echo present || echo gone)"; echo "server=$(ttmux list-sessions 2>&1 | grep -c "no server\|No such file\|error connecting")"' 2>&1)"
check "ttmux_cleanup removes the temp dir and stops the server" "$(grep -c '^dir=gone$' <<< "$OUT"):$(grep -c '^server=1$' <<< "$OUT")" "1:1"
exit "$fail"
