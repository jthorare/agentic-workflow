#!/usr/bin/env bash
# Tests for mods/ (the Claude Code mods pr-line and my-prs) and scripts/install-mods.sh.
#  1. The two copies of hooks/rows.ts are byte-identical and the two types/index.d.ts files differ only in their
#     `declare module` block (each plugin dir must stand alone, so the copies stay; this stops them drifting).
#  2. With `claude` on PATH: `claude plugin validate` on each mod and on mods/ (the marketplace), `claude plugin test` per mod.
#     Without it these print a SKIP line.
#  3. install-mods.sh against a stub `claude`: no claude -> exit 0 with a note, --dry-run runs nothing, a first run adds the
#     marketplace and installs both mods, a second run does neither.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/mods-test.XXXXXX")"
trap 'find "$TMPROOT" -delete' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }
skip() { echo "  SKIP $1"; }

# --- 1. no drift between the two copies ---
if cmp -s "$REPO/mods/pr-line/hooks/rows.ts" "$REPO/mods/my-prs/hooks/rows.ts"; then
  ok "hooks/rows.ts is identical in pr-line and my-prs"
else
  fail "hooks/rows.ts differs between pr-line and my-prs (edit one, copy it to the other)"
  diff "$REPO/mods/pr-line/hooks/rows.ts" "$REPO/mods/my-prs/hooks/rows.ts" | sed 's/^/      | /' || true
fi

# The declare module block (the PluginState of each mod) is the only part allowed to differ.
strip_state() { awk '/^declare module /{skip=1} !skip' "$1"; }
if [ "$(strip_state "$REPO/mods/pr-line/types/index.d.ts")" = "$(strip_state "$REPO/mods/my-prs/types/index.d.ts")" ]; then
  ok "types/index.d.ts matches outside the declare module block"
else
  fail "types/index.d.ts differs outside the declare module block (keep Tone, Person, PrRow, Snapshot the same in both mods)"
fi

# --- 2. claude validate and test ---
if command -v claude >/dev/null 2>&1; then
  for target in mods mods/pr-line mods/my-prs; do
    if (cd "$REPO" && claude plugin validate "$target" >"$TMPROOT/validate.log" 2>&1); then
      ok "claude plugin validate $target"
    else
      fail "claude plugin validate $target"; sed 's/^/      | /' "$TMPROOT/validate.log"
    fi
  done
  for mod in pr-line my-prs; do
    if (cd "$REPO" && claude plugin test "mods/$mod" >"$TMPROOT/test.log" 2>&1); then
      ok "claude plugin test mods/$mod ($(grep -E '^ *[0-9]+ pass' "$TMPROOT/test.log" | tr -s ' ' | head -1 | sed 's/^ //'))"
    else
      fail "claude plugin test mods/$mod"; sed 's/^/      | /' "$TMPROOT/test.log"
    fi
  done
else
  skip "claude is not on PATH: plugin validate and plugin test not run"
fi

# --- 3. install-mods.sh against a stub claude ---
INSTALL="$REPO/scripts/install-mods.sh"
STUB="$TMPROOT/bin"
STATE="$TMPROOT/state"
mkdir -p "$STUB" "$STATE"
cat >"$STUB/claude" <<'STUB'
#!/usr/bin/env bash
# Stub: records each call; `marketplace add` and `install` leave a marker that `list --json` reports.
echo "$*" >>"$STUB_STATE/calls"
case "$1 $2 $3" in
  "plugin marketplace list") if [ -f "$STUB_STATE/market" ]; then echo '[{"name": "aw-mods"}]'; else echo '[]'; fi ;;
  "plugin marketplace add") touch "$STUB_STATE/market" ;;
  "plugin list --json") for f in "$STUB_STATE"/installed.*; do [ -f "$f" ] && printf '[{"id": "%s"}]\n' "$(cat "$f")"; done; true ;;
  "plugin install "*) echo "$3" >"$STUB_STATE/installed.${3%%@*}" ;;
esac
STUB
chmod +x "$STUB/claude"
SAFE_PATH="/usr/bin:/bin"

# No claude on PATH: exit 0 with a note.
out="$(PATH="$SAFE_PATH" bash "$INSTALL" 2>&1)" && rc=0 || rc=$?
if [ "$rc" -eq 0 ] && printf '%s\n' "$out" | grep -q 'claude is not on PATH'; then ok "install-mods.sh without claude exits 0 with a note"; else fail "install-mods.sh without claude (rc=$rc): $out"; fi

run_install() { STUB_STATE="$STATE" PATH="$STUB:$SAFE_PATH" bash "$INSTALL" "$@" 2>&1; }

out="$(run_install --dry-run)"
if printf '%s\n' "$out" | grep -q 'dry-run.*marketplace add .*/mods' && printf '%s\n' "$out" | grep -q 'dry-run.*install pr-line@aw-mods' \
   && printf '%s\n' "$out" | grep -q 'dry-run.*install my-prs@aw-mods' && ! grep -qE 'marketplace add|plugin install' "$STATE/calls" 2>/dev/null; then
  ok "--dry-run prints the three commands and runs none"
else
  fail "--dry-run: $out"
fi

out="$(run_install)"
if grep -q "^plugin marketplace add $REPO/mods\$" "$STATE/calls" && grep -q '^plugin install pr-line@aw-mods --scope user$' "$STATE/calls" \
   && grep -q '^plugin install my-prs@aw-mods --scope user$' "$STATE/calls"; then
  ok "first run adds the marketplace and installs both mods"
else
  fail "first run: $out"; sed 's/^/      | /' "$STATE/calls"
fi

: >"$STATE/calls"
out="$(run_install)"
if ! grep -qE 'marketplace add|plugin install' "$STATE/calls" && printf '%s\n' "$out" | grep -q 'already installed'; then
  ok "second run changes nothing"
else
  fail "second run: $out"; sed 's/^/      | /' "$STATE/calls"
fi

out="$(run_install --scope nowhere)" && rc=0 || rc=$?
if [ "$rc" -eq 2 ]; then ok "a bad --scope exits 2"; else fail "bad --scope exit $rc"; fi

echo ""
echo "mods.test.sh: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
