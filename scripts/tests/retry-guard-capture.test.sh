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

TMPS=()
mk() { local d; d="$(mktemp -d)"; TMPS+=("$d"); printf '%s\n' "$d"; }
trap 'rc=$?; for d in ${TMPS[@]+"${TMPS[@]}"}; do find "$d" -delete 2> /dev/null; done; exit $rc' EXIT

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

# --- Codex's argv form (exec_command, cmd an array) hashes like the string form
jq -c '.tool_input.cmd = ["aw-nosuch-tool", "--version"]' "$FIX/codex/pretooluse-bash-nosuch.json" > "$WORK/codex-argv.json"
D="$(from codex "$WORK/codex-argv.json" codex "$FIX/codex/posttooluse-bash-exit127.json")"
OUT="$(bash "$SCRIPT" --from "$D" --providers codex 2>&1)"; RC=$?
check "argv pre + string failure: exit 0" "$RC" "0"
has "argv pre + string failure: PASS" "$OUT" "codex: PASS"

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
SAVE="$(mk)"
OUT="$(PATH="$BIN:$PATH" FAKE_FIX="$FIX" FAKE_ADAPTERS="$ADAPTERS" FAKE_NOOP="$NOOP" bash "$SCRIPT" --out "$SAVE" 2>&1)"; RC=$?
check "live: exit 0" "$RC" "0"
has "live: codex PASS" "$OUT" "codex: PASS"
has "live: cursor PASS" "$OUT" "cursor: PASS"
check "live: saved one pre and one failure per provider" "$(ls "$SAVE" | sort | tr '\n' ' ')" "codex-failure.json codex-pre.json cursor-failure.json cursor-pre.json "

# --- usage
bash "$SCRIPT" --providers claude > /dev/null 2>&1; check "usage: an unknown provider exits 2" "$?" "2"

printf '%d passed, 0 skipped, %d failed\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ]
