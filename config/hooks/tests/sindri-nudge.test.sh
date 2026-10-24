#!/usr/bin/env bash
# Tests for config/hooks/sindri-nudge.sh. Run: bash config/hooks/tests/sindri-nudge.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOKS="$(cd "$DIR/.." && pwd)"
HOOK="$HOOKS/sindri-nudge.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export HOME="$WORK/home"; mkdir -p "$HOME"
export AW_STATE_DIR="$WORK/state"
unset AW_JUDGE_CHILD AW_SINDRI_CHILD
REPO="$WORK/repo"; mkdir -p "$REPO"; git -C "$REPO" init -q
BIN="$WORK/bin"; mkdir -p "$BIN"
cat > "$BIN/sindri" <<'EOF'
#!/usr/bin/env bash
echo "$*" >> "$CALLS"
[ -n "${SLEEP:-}" ] && sleep "$SLEEP"
printf '%s\n' "sindri: repo is not onboarded; run: sindri repo onboard" "second line"
EOF
chmod +x "$BIN/sindri"
export CALLS="$WORK/calls"

run() { (cd "$1" && PATH="$2:/usr/bin:/bin" bash "$HOOK"); }

OUT="$(run "$REPO" "$WORK/none")"; RC=$?
check "no sindri: silent" "$OUT" ""
check "no sindri: exit 0" "$RC" "0"
OUT="$(run "$WORK" "$BIN")"
check "outside git: silent" "$OUT" ""
check "outside git: sindri not called" "$(cat "$CALLS" 2>/dev/null)" ""
OUT="$(run "$REPO" "$BIN")"
check "in a repo: exactly the first line" "$OUT" "sindri: repo is not onboarded; run: sindri repo onboard"
check "in a repo: asks repo status --nudge" "$(cat "$CALLS")" "repo status --nudge"
START=$(date +%s)
OUT="$(SLEEP=5 AW_SINDRI_NUDGE_BUDGET_MS=300 run "$REPO" "$BIN")"; RC=$?
check "slow sindri: killed, silent" "$OUT" ""
check "slow sindri: exit 0" "$RC" "0"
check "slow sindri: the timeout is logged" "$(grep -c '"hook":"sindri-nudge","cause":"timeout"' "$AW_STATE_DIR/logs/failopen.log" 2>/dev/null)" "1"
check "slow sindri: returns within 3 s" "$(( $(date +%s) - START < 3 ))" "1"
OUT="$(AW_SINDRI_NUDGE_BUDGET_MS=abc run "$REPO" "$BIN")"
check "bad budget falls back to the default" "$OUT" "sindri: repo is not onboarded; run: sindri repo onboard"
OUT="$(AW_SINDRI_CHILD=1 run "$REPO" "$BIN")"
check "sindri child session: silent" "$OUT" ""
OUT="$(AW_JUDGE_CHILD=1 run "$REPO" "$BIN")"
check "judge child session: silent" "$OUT" ""
# A failing sindri (non-zero exit) is silent, even when it printed something.
FAILBIN="$WORK/failbin"; mkdir -p "$FAILBIN"
printf '#!/usr/bin/env bash\necho "partial output"\nexit 2\n' > "$FAILBIN/sindri"
chmod +x "$FAILBIN/sindri"
OUT="$(run "$REPO" "$FAILBIN")"; RC=$?
check "failing sindri: silent" "$OUT" ""
check "failing sindri: exit 0" "$RC" "0"
# Falls back to ~/.local/bin/sindri when sindri is not on PATH.
mkdir -p "$HOME/.local/bin"; cp "$BIN/sindri" "$HOME/.local/bin/sindri"
OUT="$(run "$REPO" "$WORK/none")"
check "~/.local/bin fallback" "$OUT" "sindri: repo is not onboarded; run: sindri repo onboard"
rm -f "$HOME/.local/bin/sindri"
# A linked worktree is inside git: the hook asks (the CLI decides whether it is onboarded).
git -C "$REPO" -c user.name=t -c user.email=t@example.com commit -q --allow-empty -m init
git -C "$REPO" worktree add -q "$WORK/wt" -b side
: > "$CALLS"
run "$WORK/wt" "$BIN" > /dev/null
check "linked worktree: asks repo status --nudge" "$(cat "$CALLS")" "repo status --nudge"
# The hook itself never writes sindri's state.
check "state dir untouched (only the fail-open log may appear)" "$(find "$AW_STATE_DIR" -type f ! -path "*/logs/failopen.log" 2>/dev/null | wc -l | tr -d " ")" "0"
# Through the Codex adapter (plain text on SessionStart becomes developer context).
sed "s|__CWD__|$REPO|" "$DIR/fixtures/codex/sessionstart.json" > "$WORK/ss.json"
OUT="$(PATH="$BIN:/usr/bin:/bin" bash "$HOOKS/adapters/codex.sh" "$HOOK" < "$WORK/ss.json")"
check "codex adapter: line passes through" "$(printf '%s' "$OUT" | grep -c 'sindri repo onboard')" "1"
# Through the Cursor adapter (text becomes additional_context).
sed "s|__CWD__|$REPO|" "$DIR/fixtures/cursor/session-start.json" > "$WORK/cs.json"
OUT="$(PATH="$BIN:/usr/bin:/bin" bash "$HOOKS/adapters/cursor.sh" "$HOOK" < "$WORK/cs.json")"
check "cursor adapter: additional_context" "$(printf '%s' "$OUT" | jq -r '.additional_context' | grep -c 'sindri repo onboard')" "1"
exit $fail
