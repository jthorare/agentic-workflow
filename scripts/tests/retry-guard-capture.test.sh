#!/usr/bin/env bash
# scripts/retry-guard-capture.sh (issue 4382): the adapters' capture tap (AW_HOOK_CAPTURE_DIR) and the PASS/FAIL check of
# a real pre + failure pair per provider. Fake codex and cursor-agent on PATH stand in for the hosts: each pipes the
# guard's own fixtures through the real adapter the way the host runs its hooks, so the live path is tested without a model.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SCRIPT="$ROOT/scripts/retry-guard-capture.sh"
ADAPTERS="$ROOT/config/hooks/adapters"
FIX="$ROOT/config/hooks/tests/fixtures"
PASS=0
FAIL=0

# One root made here, in the suite's own shell: mk runs inside $(...), so it only makes folders under it.
ROOT_TMP="$(mktemp -d)"
trap 'rc=$?; find "$ROOT_TMP" -delete 2> /dev/null; exit $rc' EXIT
mk() { mktemp -d "$ROOT_TMP/d.XXXXXX"; }

check() {
  if [ "$2" = "$3" ]; then PASS=$((PASS + 1)); else FAIL=$((FAIL + 1)); printf 'FAIL %s\n  expected: %s\n  actual:   %s\n' "$1" "$3" "$2"; fi
}
has() {
  case "$2" in *"$3"*) PASS=$((PASS + 1)) ;; *) FAIL=$((FAIL + 1)); printf 'FAIL %s\n  missing: %s\n  in: %s\n' "$1" "$3" "$2" ;; esac
}

WORK="$(mk)"
NOOP="$WORK/noop.sh"
printf '#!/usr/bin/env bash\ncat > /dev/null\nexit 0\n' > "$NOOP"
chmod +x "$NOOP"

# --- the tap: AW_HOOK_CAPTURE_DIR set -> the raw host payload is saved once per hook run; unset -> nothing
CAP="$(mk)"
AW_HOOK_CAPTURE_DIR="$CAP" bash "$ADAPTERS/codex.sh" "$NOOP" < "$FIX/codex/pretooluse-bash-nosuch.json" > /dev/null 2>&1
AW_HOOK_CAPTURE_DIR="$CAP" bash "$ADAPTERS/cursor.sh" "$NOOP" < "$FIX/cursor/pretooluse-shell-nosuch.json" > /dev/null 2>&1
check "tap: one file per provider" "$(ls "$CAP" | sed 's/\..*//' | sort | tr '\n' ' ')" "codex cursor "
check "tap: the codex file is the raw payload" "$(jq -c . "$CAP"/codex.*)" "$(jq -c . "$FIX/codex/pretooluse-bash-nosuch.json")"
check "tap: the file is private (0600)" "$(stat -c %a "$CAP"/cursor.* 2> /dev/null || stat -f %Lp "$CAP"/cursor.*)" "600"
(cd "$CAP" && bash "$ADAPTERS/codex.sh" "$NOOP" < "$FIX/codex/pretooluse-bash-nosuch.json" > /dev/null 2>&1)
check "tap: unset writes nothing" "$(ls "$CAP" | wc -l | tr -d ' ')" "2"

# from <dir>: lays out captures the way the tap names them
from() {
  local d; d="$(mk)"
  local i=0 p f
  while [ $# -gt 1 ]; do p="$1"; f="$2"; shift 2; i=$((i + 1)); cp "$f" "$d/$p.$i.json"; done
  printf '%s\n' "$d"
}

# --- a matching pair per provider passes
D="$(from codex "$FIX/codex/pretooluse-bash-nosuch.json" codex "$FIX/codex/posttooluse-bash-exit127.json" \
          cursor "$FIX/cursor/pretooluse-shell-nosuch.json" cursor "$FIX/cursor/post-tool-use-failure-shell.json")"
OUT="$(bash "$SCRIPT" --from "$D" 2>&1)"; RC=$?
check "fixtures: exit 0" "$RC" "0"
has "fixtures: codex PASS" "$OUT" "codex: PASS"
has "fixtures: cursor PASS" "$OUT" "cursor: PASS"

# --- a failure whose call differs from the pre call fails, naming both hashes
jq -c '.tool_input.cmd = "aw-nosuch-tool --help"' "$FIX/codex/posttooluse-bash-exit127.json" > "$WORK/codex-other.json"
D="$(from codex "$FIX/codex/pretooluse-bash-nosuch.json" codex "$WORK/codex-other.json")"
OUT="$(bash "$SCRIPT" --from "$D" --providers codex 2>&1)"; RC=$?
check "mismatch: exit 1" "$RC" "1"
has "mismatch: codex FAIL" "$OUT" "codex: FAIL"
has "mismatch: says the hashes differ" "$OUT" "call hashes differ"

# --- Codex's argv form (exec_command, cmd an array) in both events: the adapter names it Bash and the pair matches.
# The canonical input keeps the raw cmd beside the derived command, so an argv pre and a string failure would differ;
# the live capture shows whether Codex ever mixes the two.
jq -c '.tool_input.cmd = ["aw-nosuch-tool", "--version"]' "$FIX/codex/pretooluse-bash-nosuch.json" > "$WORK/codex-argv.json"
jq -c '.tool_input.cmd = ["aw-nosuch-tool", "--version"]' "$FIX/codex/posttooluse-bash-exit127.json" > "$WORK/codex-argv-fail.json"
D="$(from codex "$WORK/codex-argv.json" codex "$WORK/codex-argv-fail.json")"
OUT="$(bash "$SCRIPT" --from "$D" --providers codex 2>&1)"; RC=$?
check "argv pair: exit 0" "$RC" "0"
has "argv pair: PASS with the host tool" "$OUT" "codex: PASS (call hash"
has "argv pair: ...names exec_command" "$OUT" "host tool exec_command"

# --- a Codex PostToolUse that did not fail is no failure event
D="$(from codex "$FIX/codex/pretooluse-bash-nosuch.json" codex "$FIX/codex/posttooluse-bash-exit0.json")"
OUT="$(bash "$SCRIPT" --from "$D" --providers codex 2>&1)"; RC=$?
check "no failure: exit 1" "$RC" "1"
has "no failure: says so" "$OUT" "codex: FAIL (no failure event"

# --- nothing captured for a provider
D="$(from codex "$FIX/codex/pretooluse-bash-nosuch.json" codex "$FIX/codex/posttooluse-bash-exit127.json")"
OUT="$(bash "$SCRIPT" --from "$D" 2>&1)"; RC=$?
check "no cursor capture: exit 2" "$RC" "2"
has "no cursor capture: says so" "$OUT" "cursor: NO CAPTURE"

# --- the diff against the guard's fixture names a key only the real payload has
jq -c '. + {sandbox_policy: "read-only"}' "$FIX/codex/pretooluse-bash-nosuch.json" > "$WORK/codex-extra.json"
D="$(from codex "$WORK/codex-extra.json" codex "$FIX/codex/posttooluse-bash-exit127.json")"
OUT="$(bash "$SCRIPT" --from "$D" --providers codex 2>&1)"
has "diff: names the extra key" "$OUT" "only in the capture: sandbox_policy"

# --- live: fake hosts run their hooks through the real adapter with the tap on; the script saves sanitized pairs
BIN="$(mk)"
cat > "$BIN/codex" << 'FAKE'
#!/usr/bin/env bash
for f in "$FAKE_FIX"/codex/pretooluse-bash-nosuch.json "$FAKE_FIX"/codex/posttooluse-bash-exit127.json; do
  bash "$FAKE_ADAPTERS/codex.sh" "$FAKE_NOOP" < "$f" > /dev/null 2>&1
done
FAKE
cat > "$BIN/cursor-agent" << 'FAKE'
#!/usr/bin/env bash
for f in "$FAKE_FIX"/cursor/pretooluse-shell-nosuch.json "$FAKE_FIX"/cursor/post-tool-use-failure-shell.json; do
  bash "$FAKE_ADAPTERS/cursor.sh" "$FAKE_NOOP" < "$f" > /dev/null 2>&1
done
FAKE
chmod +x "$BIN/codex" "$BIN/cursor-agent"
# The hosts' hooks files, as the installers write them: Codex nested and quoted, Cursor flat.
HK="$(mk)"
jq -n --arg c "'$ADAPTERS/codex.sh' '$ROOT/config/hooks/retry-gate.sh' # aw:retry-gate" \
  '{hooks: {PreToolUse: [{matcher: ".*", hooks: [{type: "command", command: $c}]}], PostToolUse: [{matcher: ".*", hooks: [{type: "command", command: $c}]}]}}' > "$HK/codex.json"
jq -n --arg c "$ADAPTERS/cursor.sh retry-gate.sh # aw:retry-gate" '{version: 1, hooks: {preToolUse: [{command: $c}], postToolUseFailure: [{command: $c}]}}' > "$HK/cursor.json"
live() { PATH="$BIN:$PATH" FAKE_FIX="$FIX" FAKE_ADAPTERS="$ADAPTERS" FAKE_NOOP="$NOOP" CODEX_HOOKS_FILE="$HK/codex.json" CURSOR_HOOKS_FILE="$HK/cursor.json" bash "$SCRIPT" "$@" 2>&1; }
SAVE="$(mk)"
OUT="$(live --out "$SAVE")"; RC=$?
check "live: exit 0" "$RC" "0"
has "live: codex PASS" "$OUT" "codex: PASS"
has "live: cursor PASS" "$OUT" "cursor: PASS"
check "live: saved one pre and one failure per provider" "$(ls "$SAVE" | sort | tr '\n' ' ')" "codex-failure.json codex-pre.json cursor-failure.json cursor-pre.json "

# --- preflight: no hook on Cursor's failure event -> NO CAPTURE with the install command, and the host never runs
jq 'del(.hooks.postToolUseFailure)' "$HK/cursor.json" > "$HK/cursor-nofail.json"
OUT="$(PATH="$BIN:$PATH" FAKE_FIX="$FIX" FAKE_ADAPTERS="$ADAPTERS" FAKE_NOOP="$NOOP" CURSOR_HOOKS_FILE="$HK/cursor-nofail.json" bash "$SCRIPT" --providers cursor 2>&1)"; RC=$?
check "preflight, no failure hook: exit 2" "$RC" "2"
has "preflight, no failure hook: names the event and the fix" "$OUT" "cursor: NO CAPTURE (no aw hook on postToolUseFailure"
has "preflight, no failure hook: ...the install command" "$OUT" "scripts/install-retry-guard.sh --provider cursor"

# --- preflight: an installed adapter without the tap -> NO CAPTURE naming it
OLD="$(mk)"
mkdir -p "$OLD/adapters"
cp "$ADAPTERS/codex.sh" "$OLD/adapters/codex.sh"
grep -v aw_adapter_capture "$ADAPTERS/common.sh" > "$OLD/adapters/common.sh"
jq -n --arg c "'$OLD/adapters/codex.sh' '$ROOT/config/hooks/retry-gate.sh' # aw:retry-gate" \
  '{hooks: {PreToolUse: [{hooks: [{command: $c}]}], PostToolUse: [{hooks: [{command: $c}]}]}}' > "$HK/codex-old.json"
OUT="$(PATH="$BIN:$PATH" CODEX_HOOKS_FILE="$HK/codex-old.json" bash "$SCRIPT" --providers codex 2>&1)"; RC=$?
check "preflight, old adapter: exit 2" "$RC" "2"
has "preflight, old adapter: says it has no tap" "$OUT" "has no capture tap"

# --- usage
bash "$SCRIPT" --providers claude > /dev/null 2>&1; check "usage: an unknown provider exits 2" "$?" "2"

printf '%d passed, 0 skipped, %d failed\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ]
