#!/usr/bin/env bash
# Tests for scripts/ci/test-ports.sh (#2597): a *.test.sh that listens on, or assigns, a literal non-zero port is rejected
# with file:line; port 0, run-time ports, client probes, URLs, comments and `# test-ports: ok: <reason>` waivers pass.
set -u
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHECK="$DIR/scripts/ci/test-ports.sh"
fail=0
check() { if [ "$2" == "$3" ]; then echo "ok - $1"; else echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1; fi; }
W="$(mktemp -d)"; trap 'rm -rf "$W"' EXIT
mkdir -p "$W/scripts/tests"

# scan <body>: a one-file fixture tree; prints "<exit>:<number of violation lines>".
scan() {
  rm -f "$W"/scripts/tests/*.sh
  printf '%s\n' "$1" > "$W/scripts/tests/a.test.sh"
  local out rc
  out="$(bash "$CHECK" --root "$W" 2> /dev/null)"; rc=$?
  echo "$rc:$(grep -c "^[^:]*:[0-9]*: " <<< "$out")"
}

check "the real tree is clean" "$(bash "$CHECK" > /dev/null 2>&1; echo $?)" "0"

# violation 1: listeners on a literal port
check "nc -l PORT is a violation" "$(scan 'nc -l 39917')" "1:1"
check "nc -l -p PORT is a violation" "$(scan 'nc -l -p 39917 > out &')" "1:1"
check "nc -lk PORT is a violation" "$(scan 'nc -lk 8080')" "1:1"
check "ncat -l PORT is a violation" "$(scan 'ncat -l 9000')" "1:1"
check "socat TCP-LISTEN is a violation" "$(scan 'socat TCP-LISTEN:7000,fork -')" "1:1"
check "socat TCP4-LISTEN is a violation" "$(scan 'socat TCP4-LISTEN:7000 -')" "1:1"
check "python http.server with a port is a violation" "$(scan 'python3 -m http.server 8000')" "1:1"
check "python bind with a double-quoted host is a violation" "$(scan 's.bind(("127.0.0.1",48999))')" "1:1"
check "python bind with single quotes and a space is a violation" "$(scan "s.bind(('127.0.0.1', 48999))")" "1:1"

# violation 2: literal port assignments
check "PORT=digits is a violation" "$(scan 'PORT=39917')" "1:1"
check "AW_PORT=digits is a violation" "$(scan 'AW_PORT=3100')" "1:1"
check "an indented export WEB_PORT=digits is a violation" "$(scan '  export WEB_PORT=4000')" "1:1"
check "an env prefix FOO_PORT=digits cmd is a violation" "$(scan 'FOO_PORT=7410 cmd')" "1:1"
check "local PORT=digits is a violation" "$(scan 'local PORT=5000')" "1:1"

# violation 3: the less direct forms review found passing (#2635)
check "python HTTPServer((host, N), H) is a violation" "$(scan 'srv = HTTPServer(("127.0.0.1", 8080), H)')" "1:1"
check "python TCPServer((host, N), H) is a violation" "$(scan "srv = socketserver.TCPServer(('', 8086), H)")" "1:1"
check "http.server --bind host N is a violation" "$(scan 'python3 -m http.server --bind 127.0.0.1 8081 &')" "1:1"
check "node .listen(N) is a violation" "$(scan 'node -e "require(\"http\").createServer(h).listen(8082)" &')" "1:1"
check "a lowercase port=digits is a violation" "$(scan 'port=39917; nc -l "$port" &')" "1:1"
check "local port=digits is a violation" "$(scan '  local srv_port=39919')" "1:1"
check "a literal default in \${PORT:-N} is a violation" "$(scan 'PORT="${PORT:-39918}"')" "1:1"
check "a literal default in \${X_PORT-N} is a violation" "$(scan 'echo "${AW_PORT-3100}"')" "1:1"
check "ncat --listen N is a violation" "$(scan 'ncat --listen 8083 &')" "1:1"
check "nc -lpN (no space) is a violation" "$(scan 'nc -lp8084 &')" "1:1"
check "a lowercase socat tcp-listen:N is a violation" "$(scan 'socat tcp-listen:8085,reuseaddr -')" "1:1"

# allowed
check "PORT=0 is allowed" "$(scan 'PORT=0')" "0:0"
check "python bind to port 0 is allowed" "$(scan 's.bind(("127.0.0.1",0))')" "0:0"
check "http.server 0 is allowed" "$(scan 'python3 -m http.server 0')" "0:0"
check "a port from a command substitution is allowed" "$(scan 'PORT="$(free_port)"')" "0:0"
check "a port from arithmetic is allowed" "$(scan 'PORT=$((BASE + 1))')" "0:0"
check "nc -l with a variable is allowed" "$(scan 'nc -l "$PORT"')" "0:0"
check "nc -l with arithmetic is allowed" "$(scan 'nc -l "$((PORT + 6))"')" "0:0"
check "python bind with a run-time port is allowed" "$(scan 's.bind(("127.0.0.1", int(sys.argv[1])))')" "0:0"
check "nc -z is a client probe, allowed" "$(scan 'nc -z localhost 4100')" "0:0"
check "a port only inside a URL is allowed" "$(scan 'curl http://127.0.0.1:7480/x')" "0:0"
check "a comment line is skipped" "$(scan '# PORT=39917 was pinned before')" "0:0"
check "python HTTPServer on port 0 is allowed" "$(scan 'srv = HTTPServer(("127.0.0.1", 0), H)')" "0:0"
check "node .listen(0) is allowed" "$(scan 'node -e "s.listen(0, () => console.log(s.address().port))"')" "0:0"
check "node .listen with a run-time port is allowed" "$(scan 'node -e "s.listen(Number(process.argv[1]))" "$PORT"')" "0:0"
check "a lowercase port from a command substitution is allowed" "$(scan 'port="$(free_port)"')" "0:0"
check "\${PORT:-0} is allowed" "$(scan 'PORT="${PORT:-0}"')" "0:0"
check "a run-time default in \${PORT:-\$x} is allowed" "$(scan 'PORT="${PORT:-$(free_port)}"')" "0:0"
check "a word ending in port that is not a port (report=1) is allowed" "$(scan 'report=1; support=2; passport=3')" "0:0"
check "an array index or a count is not a port" "$(scan 'n_ports=${#ports[@]}')" "0:0"
check "ncat --listen with a variable is allowed" "$(scan 'ncat --listen "$PORT" &')" "0:0"
check "a lowercase socat tcp-listen on a variable is allowed" "$(scan 'socat "tcp-listen:$PORT" -')" "0:0"
check "an indented listener with a trailing comment is a violation" "$(scan '  nc -l 39917 &  # start a listener')" "1:1"
check "an indented PORT=digits with a trailing comment is a violation" "$(scan '  PORT=4100 run x  # pass it on')" "1:1"
check "a listener after a \${#x} on an indented line is a violation" "$(scan '  echo "${#a[@]}"; nc -l 39918 &')" "1:1"
check "an indented code line with a waiver does not waive the line below" "$(scan '  echo hi  # test-ports: ok: not a comment line
  PORT=4100')" "1:1"

# waiver
check "a same-line waiver with a reason passes" "$(scan 'UI_EVIDENCE_API_PORT=4100   # test-ports: ok: an env value, nc is a stub')" "0:0"
check "a waiver comment on the line above passes" "$(scan '# test-ports: ok: only an env value for a stub
UI_EVIDENCE_API_PORT=4100')" "0:0"
check "a waiver with no reason does not waive (same line)" "$(scan 'PORT=4100  # test-ports: ok')" "1:1"
check "a waiver with no reason does not waive (line above)" "$(scan '# test-ports: ok
PORT=4100')" "1:1"
check "a waiver covers only the line below it" "$(scan '# test-ports: ok: first only
PORT=4100
PORT=4200')" "1:1"

# output format
printf '%s\n' 'echo one' 'echo two' 'PORT=39917' > "$W/scripts/tests/a.test.sh"
out="$(bash "$CHECK" --root "$W" 2> /dev/null)"
check "a violation line starts with path:line:" "${out%%: *}" "scripts/tests/a.test.sh:3"
check "stdout holds only violation lines" "$(grep -vc '^[^:]*:[0-9]*: ' <<< "$out")" "0"

# usage and self-skip
check "--root without a value is a usage error" "$(bash "$CHECK" --root > /dev/null 2>&1; echo $?)" "2"
check "an unknown flag is a usage error" "$(bash "$CHECK" --nope > /dev/null 2>&1; echo $?)" "2"
rm -f "$W"/scripts/tests/*.sh; mkdir -p "$W/scripts/ci"
printf 'PORT=39917\n' > "$W/scripts/ci/test-ports.sh"; printf 'PORT=39917\n' > "$W/scripts/tests/test-ports.test.sh"
check "the check and its own suite are skipped" "$(bash "$CHECK" --root "$W" > /dev/null 2>&1; echo $?)" "0"
exit "$fail"
