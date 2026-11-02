#!/usr/bin/env bash
# Tests for --target-dir on providers/{claude,codex}/install-hooks.sh (SB-8: a separate-login agent home).
# Run: bash providers/tests/install-hooks-target.test.sh
# Everything runs under a temp HOME and a temp target dir; the temp HOME stands in for the owner's real one.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export HOME="$WORK/home"; mkdir -p "$HOME"
unset AW_STATE_DIR AW_HOOKS_DIR CODEX_HOOKS_FILE CODEX_HOME CURSOR_HOOKS_FILE CLAUDE_SETTINGS_FILE CLAUDE_HOOKS_DIR CLAUDE_DIR SETTINGS_FILE TOOLKIT_DIR AW_DRY_RUN
BIN="$WORK/bin"; mkdir -p "$BIN"
printf '#!/usr/bin/env bash\nexit 0\n' > "$BIN/judge"; chmod +x "$BIN/judge"
export PATH="$BIN:$PATH"
q() { "$@" > /dev/null 2>&1; }
# The temp HOME must stay empty: a file here means an installer reached for the owner's home.
home_files() { find "$HOME" -mindepth 1 2>/dev/null | wc -l | tr -d ' '; }

# ---------------- claude ----------------
CT="$WORK/claude-home"
q bash "$ROOT/providers/claude/install-hooks.sh" --target-dir "$CT"
check "claude: settings.json lands in the target dir" "$([ -f "$CT/settings.json" ] && echo y)" "y"
check "claude: hook scripts land in DIR/hooks" "$([ -x "$CT/hooks/block-destructive.sh" ] && [ -f "$CT/hooks/lib/failopen.sh" ] && echo y)" "y"
check "claude: Bash safety commands are rooted at DIR/hooks" \
  "$(jq -r '[.hooks.PreToolUse[].hooks[].command] | join(",")' "$CT/settings.json")" \
  "$CT/hooks/block-destructive.sh,$CT/hooks/block-push-main.sh,$CT/hooks/detect-secrets.sh,$CT/hooks/rtk-rewrite.sh"
check "claude: SessionStart commands are rooted at DIR/hooks" \
  "$(jq -r '[.hooks.SessionStart[].hooks[].command] | join(",")' "$CT/settings.json")" \
  "$CT/hooks/git-context.sh,$CT/hooks/prism-context.sh"
check "claude: no command points at ~/.claude" "$(grep -c '\.claude' "$CT/settings.json")" "0"
check "claude: the real home is untouched" "$(home_files)" "0"
snap="$(jq -Sc . "$CT/settings.json")"
q bash "$ROOT/providers/claude/install-hooks.sh" --target-dir "$CT"
check "claude: second run is idempotent" "$(jq -Sc . "$CT/settings.json")" "$snap"
q bash "$ROOT/providers/claude/install-hooks.sh" --target-dir="$CT"
check "claude: --target-dir=DIR form is the same install" "$(jq -Sc . "$CT/settings.json")" "$snap"
q bash "$ROOT/providers/claude/install-hooks.sh" --uninstall --target-dir "$CT"
check "claude: --uninstall with a target removes its entries" "$(jq -c '.hooks // {}' "$CT/settings.json")" "{}"
check "claude: the real home is still untouched after uninstall" "$(home_files)" "0"
out="$(bash "$ROOT/providers/claude/install-hooks.sh" --target-dir 2>&1 >/dev/null)"; rc=$?
check "claude: missing DIR exits 2" "$rc" "2"
check "claude: missing DIR prints usage" "$(printf '%s' "$out" | grep -c 'Usage:')" "1"
check "claude: a refused run writes nothing" "$(home_files)" "0"

# Without the flag, behavior is unchanged: commands keep the literal ~/.claude path.
mkdir -p "$HOME/.claude"; echo '{}' > "$HOME/.claude/settings.json"
q bash "$ROOT/providers/claude/install-hooks.sh"
check "claude: default install keeps ~/.claude/hooks commands" \
  "$(jq -r '.hooks.PreToolUse[].hooks[0].command' "$HOME/.claude/settings.json")" "~/.claude/hooks/block-destructive.sh"

# ---------------- codex ----------------
rm -r "$HOME/.claude"
XT="$WORK/codex-home"
q bash "$ROOT/providers/codex/install-hooks.sh" --target-dir "$XT"
check "codex: hooks.json lands in the target dir" "$([ -f "$XT/hooks.json" ] && echo y)" "y"
check "codex: scripts and adapter land in DIR/hooks" "$([ -x "$XT/hooks/block-destructive.sh" ] && [ -x "$XT/hooks/adapters/codex.sh" ] && [ -f "$XT/hooks/lib/locks.sh" ] && echo y)" "y"
check "codex: commands go through DIR's adapter" \
  "$(jq -r '.hooks.PreToolUse[].hooks[].command | select(endswith("# aw:block-destructive"))' "$XT/hooks.json")" \
  "$XT/hooks/adapters/codex.sh $XT/hooks/block-destructive.sh # aw:block-destructive"
check "codex: every command is rooted at DIR" \
  "$(jq -r '[.hooks[][].hooks[].command | select(contains("'"$XT"'/hooks/") | not)] | length' "$XT/hooks.json")" "0"
check "codex: the real home is untouched" "$(home_files)" "0"
snap="$(jq -Sc . "$XT/hooks.json")"
q bash "$ROOT/providers/codex/install-hooks.sh" --target-dir "$XT"
check "codex: second run is idempotent" "$(jq -Sc . "$XT/hooks.json")" "$snap"
q bash "$ROOT/providers/codex/install-hooks.sh" --uninstall --target-dir "$XT"
check "codex: --uninstall with a target empties its entries" "$(jq -r '[.hooks[]?[]?] | length' "$XT/hooks.json")" "0"
out="$(bash "$ROOT/providers/codex/install-hooks.sh" --target-dir 2>&1 >/dev/null)"; rc=$?
check "codex: missing DIR exits 2" "$rc" "2"
check "codex: missing DIR prints usage" "$(printf '%s' "$out" | grep -c 'Usage:')" "1"
check "codex: a refused run writes nothing" "$(home_files)" "0"

exit "$fail"
