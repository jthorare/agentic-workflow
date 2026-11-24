#!/usr/bin/env bash
# Tests for scripts/replay/run-hooks.sh and scripts/replay/fake-judge.sh (#4257 P1, docs/hooks/replay.md "Replay as
# data"): the seeded block-destructive cases pass on all three providers, a payload is data and never runs, a canary
# call fails its case, the hook child gets no keys and a dead judge URL, the fake judge plays recorded verdicts and
# --strict fails on a missing one, reset and corrupt-state events, the world builder, the fake clock, malformed stdin,
# pendingHook and unsupported rows, and the usage refusals.
# Run: bash scripts/tests/replay-hooks.test.sh
set -uo pipefail
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUNNER="$REPO/scripts/replay/run-hooks.sh"
SEEDS="$REPO/tests/fixtures/replay"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export AW_STATE_DIR="$WORK/caller-state"
mkdir -p "$AW_STATE_DIR"

# run <args...>: the runner; sets OUT (stdout), ERR (stderr) and RC.
run() {
  "$BASH" "$RUNNER" "$@" > "$WORK/out" 2> "$WORK/err"; RC=$?
  OUT="$(cat "$WORK/out")"; ERR="$(cat "$WORK/err")"
}
# line <case id> <event n> <jq filter>: one field of one results line in $WORK/results.jsonl.
line() { jq -r --arg f "$1" --argjson e "$2" "select(.fixture == \$f and .event == \$e) | $3" "$WORK/results.jsonl" 2>/dev/null; }
# fx <id> <json>: writes a test fixture at $WORK/fx/<id>.json; the json gets the shared header fields.
fx() {
  local id="$1" p="${1%%/*}" rest="${1#*/}"
  local scenario="${rest%%/*}"
  mkdir -p "$WORK/fx/$(dirname "$id")"
  jq -n --arg id "$id" --arg p "$p" --arg s "$scenario" --argjson body "$2" '
    {schema: "aw-replay-fixture/1", id: $id, provider: $p, scenario: $s, verified: "unverified",
     source: {hostVersion: "test", capturedAt: null, sanitizedBy: null, reviewedBy: "test", reviewedAt: null},
     policy: "test-policy/1"} + $body' > "$WORK/fx/$id.json"
}

# Test hooks: small canonical scripts the fixtures below name through --hooks-dir.
HOOKS="$WORK/hooks"
mkdir -p "$HOOKS"
# Calls gh: the canary must fire.
printf '#!/usr/bin/env bash\ncat >/dev/null\ngh api user >/dev/null 2>&1\nexit 0\n' > "$HOOKS/calls-gh.sh"
# Pushes with git: the git canary fires on push.
printf '#!/usr/bin/env bash\ncat >/dev/null\ngit push origin main >/dev/null 2>&1\nexit 0\n' > "$HOOKS/git-push.sh"
# Reads git without the network: allowed, no canary.
printf '#!/usr/bin/env bash\ncat >/dev/null\ngit --version >/dev/null 2>&1\nexit 0\n' > "$HOOKS/git-version.sh"
# Writes the env it sees to the root, so the test can read it with --keep.
printf '#!/usr/bin/env bash\ncat >/dev/null\nenv > ./env.txt\nexit 0\n' > "$HOOKS/dump-env.sh"
# Asks the judge; denies on a recorded "deny", allows on anything else (the judge failing open).
cat > "$HOOKS/asks-judge.sh" << 'EOF'
#!/usr/bin/env bash
in="$(cat)"
out="$(printf '%s' "$in" | judge ask-check 2>/dev/null)"; rc=$?
if [ "$rc" -eq 0 ] && [ "$(printf '%s' "$out" | jq -r '.decision // empty' 2>/dev/null)" = "deny" ]; then
  echo "BLOCKED: the judge said deny" >&2; exit 2
fi
exit 0
EOF
# Records the session id it saw (Claude and Codex session_id, Cursor conversation_id after the adapter maps it).
printf '#!/usr/bin/env bash\njq -r .session_id >> ./sessions.txt\nexit 0\n' > "$HOOKS/session.sh"
# Writes a state file on its first call; on later calls records whether that file is still JSON.
cat > "$HOOKS/state.sh" << 'EOF'
#!/usr/bin/env bash
cat >/dev/null
f="$AW_STATE_DIR/test-hook/state.json"
if [ -f "$f" ]; then
  if jq -e . "$f" >/dev/null 2>&1; then echo json >> ./state.txt; else echo corrupt >> ./state.txt; fi
else
  mkdir -p "${f%/*}"; echo '{"n":1}' > "$f"; echo made >> ./state.txt
fi
exit 0
EOF
# Records what the world holds: an rg on PATH, a declared file, the gh auth file's mtime, the clock.
cat > "$HOOKS/world.sh" << 'EOF'
#!/usr/bin/env bash
cat >/dev/null
{
  printf 'rg=%s ' "$(command -v rg >/dev/null 2>&1 && echo yes || echo no)"
  printf 'file=%s ' "$([ -f ./src/a.cs ] && echo yes || echo no)"
  printf 'auth=%s ' "$(stat -c %Y "$HOME/.config/gh/hosts.yml" 2>/dev/null || stat -f %m "$HOME/.config/gh/hosts.yml" 2>/dev/null || echo none)"
  printf 'now=%s\n' "$(date +%s)"
} >> ./world.txt
exit 0
EOF
# Advises: exit 0 with text on stderr.
printf '#!/usr/bin/env bash\ncat >/dev/null\necho "consider a backoff" >&2\nexit 0\n' > "$HOOKS/advises.sh"
chmod +x "$HOOKS"/*.sh
# The repo's block-destructive hook, so test fixtures can name it beside the test hooks.
cp "$REPO/config/hooks/block-destructive.sh" "$HOOKS/"
mkdir -p "$WORK/lib" && cp "$REPO/config/lib/failopen.sh" "$WORK/lib/"

check "the runner exists" "$([ -f "$RUNNER" ] && echo y)" "y"
check "the fake judge exists" "$([ -f "$REPO/scripts/replay/fake-judge.sh" ] && echo y)" "y"

# --- The seeded cases: block-destructive on claude, codex and cursor. ---
run "$SEEDS" --out "$WORK/results.jsonl" --strict
check "seeds: exit 0" "$RC" "0"
check "seeds: 9 result lines" "$(wc -l < "$WORK/results.jsonl" 2>/dev/null | tr -d ' ')" "9"
check "seeds: every line passes" "$(jq -r .status "$WORK/results.jsonl" 2>/dev/null | sort -u | tr '\n' ' ')" "pass "
for p in claude codex cursor; do
  check "seeds: $p rm -rf is deny" "$(line "$p/block-destructive/deny-rm-rf" 1 .decision)" "deny"
  check "seeds: $p ls is allow" "$(line "$p/block-destructive/allow-ls" 1 .decision)" "allow"
  check "seeds: $p not-JSON stdin is abstain" "$(line "$p/block-destructive/malformed-not-json" 1 .decision)" "abstain"
  check "seeds: $p not-JSON logs a fail-open line" "$(line "$p/block-destructive/malformed-not-json" 1 .failopen)" "true"
done
check "seeds: a results line has the contract fields" \
  "$(line claude/block-destructive/deny-rm-rf 1 '[has("fixture","provider","scenario","verified","event","hook","decision","reasonCode","exit","hookMs","judgeCalls","judgeSource","failopen","expected","status","reason")] | all')" "true"
check "seeds: the deny line carries the hook's exit 2" "$(line claude/block-destructive/deny-rm-rf 1 .exit)" "2"
check "seeds: a summary on stderr" "$(printf '%s' "$ERR" | grep -c '^replay: 9 cases, 9 events, 9 pass, 0 fail')" "1"
check "seeds: nothing written to the caller's state dir" "$(find "$AW_STATE_DIR" -mindepth 1 | wc -l | tr -d ' ')" "0"

# --- A payload is data: a command that would write a file and reach the network does neither. ---
fx claude/pwned/touch-and-curl '{"events":[{"n":1,"hook":"PreToolUse","script":"block-destructive.sh",
  "payload":{"session_id":"s1","hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{"command":"touch __ROOT__/PWNED; curl http://example.invalid"}}}],
  "expect":[{"event":1,"decision":"allow"}]}'
run "$WORK/fx/claude/pwned" --out "$WORK/results.jsonl" --hooks-dir "$HOOKS" --keep "$WORK/keep-pwned"
check "pwned: exit 0" "$RC" "0"
check "pwned: the case passes" "$(line claude/pwned/touch-and-curl 1 .status)" "pass"
check "pwned: --keep kept the case dir" "$(ls "$WORK/keep-pwned" 2>/dev/null | wc -l | tr -d ' ')" "1"
check "pwned: no PWNED file" "$(find "$WORK/keep-pwned" -name PWNED | wc -l | tr -d ' ')" "0"
check "pwned: no CANARY file" "$(find "$WORK/keep-pwned" -name CANARY | wc -l | tr -d ' ')" "0"

# --- Canaries: a hook that reaches gh, or pushes with git, fails its case. Plain git reads pass. ---
fx claude/canary/gh '{"events":[{"n":1,"hook":"PreToolUse","script":"calls-gh.sh","payload":{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"ls"}}}],
  "expect":[{"event":1,"decision":"allow"}]}'
fx claude/canary/git-push '{"events":[{"n":1,"hook":"PreToolUse","script":"git-push.sh","payload":{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"ls"}}}],
  "expect":[{"event":1,"decision":"allow"}]}'
fx claude/canary/git-read '{"events":[{"n":1,"hook":"PreToolUse","script":"git-version.sh","payload":{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"ls"}}}],
  "expect":[{"event":1,"decision":"allow"}]}'
run "$WORK/fx/claude/canary" --out "$WORK/results.jsonl" --hooks-dir "$HOOKS"
check "canary: exit 1" "$RC" "1"
check "canary: the gh case fails" "$(line claude/canary/gh 1 .status)" "fail"
check "canary: the reason names gh" "$(line claude/canary/gh 1 '.reason | test("canary: gh")')" "true"
check "canary: the git push case fails" "$(line claude/canary/git-push 1 .status)" "fail"
check "canary: the reason names git push" "$(line claude/canary/git-push 1 '.reason | test("canary: git push")')" "true"
check "canary: a git read passes" "$(line claude/canary/git-read 1 .status)" "pass"

# --- No network and no keys: the hook child's env. ---
fx claude/env/dump '{"events":[{"n":1,"hook":"PreToolUse","script":"dump-env.sh","payload":{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"ls"}}}],
  "expect":[{"event":1,"decision":"allow"}]}'
ANTHROPIC_API_KEY=sk-test OPENAI_API_KEY=sk-test GH_TOKEN=ghp-test run "$WORK/fx/claude/env" --out "$WORK/results.jsonl" --hooks-dir "$HOOKS" --keep "$WORK/keep-env"
ENVF="$(find "$WORK/keep-env" -name env.txt | head -1)"
check "env: exit 0" "$RC" "0"
check "env: the hook wrote its env" "$([ -s "$ENVF" ] && echo y)" "y"
check "env: no ANTHROPIC_API_KEY" "$(grep -c '^ANTHROPIC_API_KEY=' "$ENVF" 2>/dev/null)" "0"
check "env: no OPENAI_API_KEY" "$(grep -c '^OPENAI_API_KEY=' "$ENVF" 2>/dev/null)" "0"
check "env: no GH_TOKEN" "$(grep -c '^GH_TOKEN=' "$ENVF" 2>/dev/null)" "0"
check "env: AW_REPLAY=1" "$(grep -c '^AW_REPLAY=1$' "$ENVF" 2>/dev/null)" "1"
check "env: the Jev URL is a dead loopback port" "$(grep -c '^AW_JUDGE_JEV_URL=http://127.0.0.1:9$' "$ENVF" 2>/dev/null)" "1"
check "env: the proxies point at the dead port" "$(grep -c '^https_proxy=http://127.0.0.1:9$' "$ENVF" 2>/dev/null)" "1"
check "env: HOME is the case's own" "$(grep '^HOME=' "$ENVF" 2>/dev/null | grep -c "^HOME=$WORK/keep-env/")" "1"
check "env: AW_STATE_DIR is the case's own" "$(grep '^AW_STATE_DIR=' "$ENVF" 2>/dev/null | grep -c "^AW_STATE_DIR=$WORK/keep-env/")" "1"
check "env: PATH starts with the canary shims" "$(grep '^PATH=' "$ENVF" 2>/dev/null | grep -c "^PATH=$WORK/keep-env/[^:]*/shims:")" "1"
check "env: AW_JUDGE_CS_BIN is the fake judge" "$(grep '^AW_JUDGE_CS_BIN=' "$ENVF" 2>/dev/null | grep -c 'fake-judge.sh$')" "1"

# --- The fake judge: a recorded verdict plays back; a missing one is unavailable, and fails under --strict. ---
PAYLOAD='{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"ls"}}'
fx claude/judge/recorded "$(jq -n --argjson p "$PAYLOAD" '{events:[{n:1,hook:"PreToolUse",script:"asks-judge.sh",payload:$p}],
  judge:{verdicts:"verdicts/judge.jsonl"}, expect:[{event:1,decision:"deny",textContains:"the judge said deny"}]}')"
KEY="$( { printf 'ask-check\n'; printf '%s' "$PAYLOAD" | jq -jc .; } | { sha256sum 2>/dev/null || shasum -a 256; } | cut -d' ' -f1)"
mkdir -p "$WORK/fx/claude/verdicts"
{
  echo '{"header":{"question":"ask-check","model":"test","promptSha256":"0","judgeVersion":"test","recordedAt":"2026-11-24","source":"fresh"}}'
  jq -nc --arg k "$KEY" '{key:$k,itemId:"1",label:null,status:"decided",decision:"deny",confidence:0.9,reasonCode:"test",latencyMs:5,stdout:"{\"decision\":\"deny\"}",exit:0}'
} > "$WORK/fx/claude/verdicts/judge.jsonl"
fx claude/judge-missing/none "$(jq -n --argjson p "$PAYLOAD" '{events:[{n:1,hook:"PreToolUse",script:"asks-judge.sh",payload:$p}],
  expect:[{event:1,decision:"allow"}]}')"
run "$WORK/fx/claude/judge" --out "$WORK/results.jsonl" --hooks-dir "$HOOKS" --strict
check "judge: a recorded verdict -> exit 0" "$RC" "0"
check "judge: the recorded deny reached the hook" "$(line claude/judge/recorded 1 .decision)" "deny"
check "judge: judgeSource recorded" "$(line claude/judge/recorded 1 .judgeSource)" "recorded"
check "judge: one judge call" "$(line claude/judge/recorded 1 .judgeCalls)" "1"
run "$WORK/fx/claude/judge-missing" --out "$WORK/results.jsonl" --hooks-dir "$HOOKS"
check "judge: missing verdict, not strict -> exit 0" "$RC" "0"
check "judge: missing verdict -> judgeSource missing" "$(line claude/judge-missing/none 1 .judgeSource)" "missing"
check "judge: missing verdict -> the hook failed open" "$(line claude/judge-missing/none 1 .decision)" "allow"
run "$WORK/fx/claude/judge-missing" --out "$WORK/results.jsonl" --hooks-dir "$HOOKS" --strict
check "judge: missing verdict, strict -> exit 1" "$RC" "1"
check "judge: strict -> the line fails" "$(line claude/judge-missing/none 1 .status)" "fail"
check "judge: strict -> the reason says verdict missing" "$(line claude/judge-missing/none 1 '.reason | test("verdict missing")')" "true"
check "judge: the fake judge refuses outside replay" \
  "$(echo '{}' | env -u AW_REPLAY "$BASH" "$REPO/scripts/replay/fake-judge.sh" ask-check >/dev/null 2>&1; echo $?)" "2"

# --- reset: session gives later payloads a new session id; corrupt-state breaks the state file. ---
for p in claude codex cursor; do
  case $p in
    cursor) P1='{"conversation_id":"c1","generation_id":"g1","hook_event_name":"beforeShellExecution","command":"ls","cwd":"/tmp"}'; H=beforeShellExecution ;;
    *) P1='{"session_id":"c1","turn_id":"t1","hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{"command":"ls"}}'; H=PreToolUse ;;
  esac
  fx "$p/reset/session" "$(jq -n --argjson p "$P1" --arg h "$H" '{events:[{n:1,hook:$h,script:"session.sh",payload:$p},{n:2,reset:"session"},
    {n:3,hook:$h,script:"session.sh",payload:$p}], expect:[{event:1,decision:"allow"},{event:3,decision:"allow"}]}')"
  run "$WORK/fx/$p/reset" --out "$WORK/results.jsonl" --hooks-dir "$HOOKS" --keep "$WORK/keep-reset-$p"
  check "reset $p: exit 0" "$RC" "0"
  check "reset $p: the second call saw a new session id" \
    "$(cat "$(find "$WORK/keep-reset-$p" -name sessions.txt | head -1)" 2>/dev/null | tr '\n' ' ')" "c1 c1-r1 "
done
fx claude/corrupt/state '{"events":[{"n":1,"hook":"PreToolUse","script":"state.sh","payload":{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"ls"}}},
  {"n":2,"hook":"PreToolUse","script":"state.sh","payload":{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"ls"}}},
  {"n":3,"reset":"corrupt-state"},
  {"n":4,"hook":"PreToolUse","script":"state.sh","payload":{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"ls"}}}],
  "expect":[{"event":4,"decision":"allow"}]}'
run "$WORK/fx/claude/corrupt" --out "$WORK/results.jsonl" --hooks-dir "$HOOKS" --keep "$WORK/keep-corrupt"
check "corrupt-state: made, then JSON, then corrupt" \
  "$(cat "$(find "$WORK/keep-corrupt" -name state.txt | head -1)" 2>/dev/null | tr '\n' ' ')" "made json corrupt "
check "corrupt-state: one result line (only event 4 is scored)" "$(wc -l < "$WORK/results.jsonl" | tr -d ' ')" "1"

# --- The world builder and the fake clock. ---
fx claude/world/build '{"world":{"files":["__ROOT__/src/a.cs"],"authFileMtime":{"github.com":1000}},
  "events":[{"n":1,"hook":"PreToolUse","script":"world.sh","payload":{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"rg x"}}},
  {"n":2,"world":{"commandsOnPath":["rg"],"authFileMtime":{"github.com":2000}}},
  {"n":3,"hook":"PreToolUse","script":"world.sh","advanceClock":86400,"payload":{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"rg x"}}}],
  "expect":[{"event":1,"decision":"allow"},{"event":3,"decision":"allow"}]}'
run "$WORK/fx/claude/world" --out "$WORK/results.jsonl" --hooks-dir "$HOOKS" --keep "$WORK/keep-world"
WF="$(find "$WORK/keep-world" -name world.txt | head -1)"
check "world: exit 0" "$RC" "0"
check "world: event 1 sees the file, no rg, auth 1000" "$(sed -n 1p "$WF" 2>/dev/null | cut -d' ' -f1-3)" "rg=no file=yes auth=1000"
check "world: event 3 sees rg and auth 2000" "$(sed -n 2p "$WF" 2>/dev/null | cut -d' ' -f1-3)" "rg=yes file=yes auth=2000"
t1="$(sed -n 1p "$WF" 2>/dev/null | sed 's/.*now=//')"; t3="$(sed -n 2p "$WF" 2>/dev/null | sed 's/.*now=//')"
check "world: advanceClock moved date +%s by a day" "$([ -n "$t1" ] && [ -n "$t3" ] && [ $((t3 - t1)) -ge 86400 ] && [ $((t3 - t1)) -lt 86500 ] && echo y)" "y"

# --- Malformed stdin through block-destructive on every provider: never a crash, never a deny. ---
# What each host really does, found by this harness: Codex's adapter turns a string tool_input into {command: …}, so
# it is a readable ls (allow). A 2 MB payload is a plain allow on every provider: Cursor's adapter used to fail open
# on it (the payload went to jq as one argument, past the kernel's per-argument limit); #4300 moved it to stdin.
for p in claude codex cursor; do
  case $p in cursor) H=beforeShellExecution ;; *) H=PreToolUse ;; esac
  case $p in codex) X4=allow ;; *) X4=abstain ;; esac
  fx "$p/malformed/all" "$(jq -n --arg h "$H" --arg x4 "$X4" '{events:[
    {n:1,hook:$h,script:"block-destructive.sh",payloadRaw:""},
    {n:2,hook:$h,script:"block-destructive.sh",payloadRaw:"{\"tool_name\":\"Bash\",\"tool_in"},
    {n:3,hook:$h,script:"block-destructive.sh",payloadBase64:"//79/A=="},
    {n:4,hook:$h,script:"block-destructive.sh",payload:{session_id:"s1",hook_event_name:$h,tool_name:"Bash",tool_input:"ls"}},
    {n:5,hook:$h,script:"block-destructive.sh",padBytes:2097152,payload:{session_id:"s1",conversation_id:"s1",hook_event_name:$h,tool_name:"Bash",tool_input:{command:"ls"},command:"ls"}}],
    expect:[{event:1,decision:"abstain"},{event:2,decision:"abstain"},{event:3,decision:"abstain"},{event:4,decision:$x4},{event:5,decision:"allow"}]}')"
  run "$WORK/fx/$p/malformed" --out "$WORK/results.jsonl" --hooks-dir "$HOOKS"
  check "malformed $p: exit 0" "$RC" "0"
  check "malformed $p: every line passes" "$(jq -r .status "$WORK/results.jsonl" 2>/dev/null | sort -u | tr '\n' ' ')" "pass "
  check "malformed $p: no deny" "$(jq -r .decision "$WORK/results.jsonl" 2>/dev/null | grep -c deny)" "0"
done

# --- Expectation checks: a wrong decision, textContains, advise, pendingHook, unsupported. ---
fx claude/expect/wrong '{"events":[{"n":1,"hook":"PreToolUse","script":"block-destructive.sh","payload":{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"ls"}}}],
  "expect":[{"event":1,"decision":"deny"}]}'
fx claude/expect/text '{"events":[{"n":1,"hook":"PreToolUse","script":"block-destructive.sh","payload":{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"rm -rf build"}}}],
  "expect":[{"event":1,"decision":"deny","textContains":"no such words"}]}'
fx claude/expect/advise '{"events":[{"n":1,"hook":"PostToolUse","script":"advises.sh","payload":{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"ls"}}}],
  "expect":[{"event":1,"decision":"advise","textContains":"backoff"}]}'
fx claude/expect/pending '{"pendingHook":"#4221 duplicate-script.sh","events":[{"n":1,"hook":"PreToolUse","script":"block-destructive.sh","payload":{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"ls"}}}],
  "expect":[{"event":1,"decision":"deny"}]}'
fx claude/expect/unsupported '{"events":[{"n":1,"hook":"PostToolUseFailure","script":"advises.sh","payload":{"session_id":"s1","tool_name":"Read"}}],
  "expect":[{"event":1,"decision":"advise","unsupported":true}]}'
run "$WORK/fx/claude/expect" --out "$WORK/results.jsonl" --hooks-dir "$HOOKS"
check "expect: exit 1 (a wrong decision)" "$RC" "1"
check "expect: wrong decision fails" "$(line claude/expect/wrong 1 .status)" "fail"
check "expect: the reason names both decisions" "$(line claude/expect/wrong 1 .reason)" "expected deny, got allow"
check "expect: textContains miss fails" "$(line claude/expect/text 1 .status)" "fail"
check "expect: the reason names the missing text" "$(line claude/expect/text 1 '.reason | test("no such words")')" "true"
check "expect: stderr text on exit 0 is advise" "$(line claude/expect/advise 1 .decision)" "advise"
check "expect: the advise case passes" "$(line claude/expect/advise 1 .status)" "pass"
check "expect: pendingHook -> notCovered" "$(line claude/expect/pending 1 .status)" "notCovered"
check "expect: unsupported -> unsupported" "$(line claude/expect/unsupported 1 .status)" "unsupported"

# --- Usage and refusals. ---
run
check "usage: no fixtures -> exit 2" "$RC" "2"
run "$WORK/fx/claude/expect/wrong.json"
check "usage: no --out -> results on stdout" "$(printf '%s\n' "$OUT" | jq -r .fixture 2>/dev/null)" "claude/expect/wrong"
mkdir -p "$AW_STATE_DIR/replay/raw/claude/x"
cp "$WORK/fx/claude/expect/wrong.json" "$AW_STATE_DIR/replay/raw/claude/x/"
run "$AW_STATE_DIR/replay/raw/claude/x/wrong.json" --out "$WORK/results.jsonl"
check "refuse: a fixture under the raw dir -> exit 2" "$RC" "2"
check "refuse: the message names the raw dir" "$(printf '%s' "$ERR" | grep -c 'replay/raw')" "1"
rm -rf "$AW_STATE_DIR/replay"
mkdir -p "$WORK/fx/claude/bad"
printf 'not json' > "$WORK/fx/claude/bad/broken.json"
run "$WORK/fx/claude/bad" --out "$WORK/results.jsonl"
check "refuse: a fixture that is not JSON -> exit 2" "$RC" "2"
fx claude/badscript/missing '{"events":[{"n":1,"hook":"PreToolUse","script":"no-such-hook.sh","payload":{}}],"expect":[{"event":1,"decision":"allow"}]}'
run "$WORK/fx/claude/badscript" --out "$WORK/results.jsonl"
check "refuse: a script that is not in the hooks dir -> exit 2" "$RC" "2"

check "the runner left no case dir behind" "$(find "${TMPDIR:-/tmp}" -maxdepth 1 -name 'aw-replay.*' -newer "$WORK/fx" 2>/dev/null | wc -l | tr -d ' ')" "0"

exit "$fail"
