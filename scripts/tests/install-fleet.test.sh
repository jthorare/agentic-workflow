#!/usr/bin/env bash
# Tests for scripts/install-fleet.sh (the aw:fleet hook installer; session collector CO-1) and setup.sh --with-fleet.
# Run: bash scripts/tests/install-fleet.test.sh   Everything runs under a temp HOME; no real settings file is touched.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
INSTALL="$ROOT/scripts/install-fleet.sh"
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
export CLAUDE_SETTINGS_FILE="$WORK/claude/settings.json"
export CLAUDE_HOOKS_DIR="$WORK/claude/hooks"
export CODEX_HOOKS_FILE="$WORK/codex/hooks.json"
export CURSOR_HOOKS_FILE="$WORK/cursor/hooks.json"
export AW_HOOKS_DIR="$WORK/aw-hooks"
mkdir -p "$WORK/claude"
EVENTS="SessionStart UserPromptSubmit PermissionRequest Notification PreToolUse PostToolUse Stop SessionEnd"
fleet_entries() { jq -r '[.hooks // {} | to_entries[] | .value[] | .hooks[] | .command | select(endswith("# aw:fleet"))] | length' "$1"; }

# A settings file with other people's hooks on the same events: the install must leave them alone.
cat > "$CLAUDE_SETTINGS_FILE" <<'JSON'
{"model": "x", "hooks": {
  "Stop": [{"hooks": [{"type": "command", "command": "~/.claude/hooks/done-gate.sh # aw:done-gate"}]}],
  "PostToolUse": [{"matcher": "Bash", "hooks": [{"type": "command", "command": "/usr/local/bin/theirs.sh"}]}],
  "Notification": [{"hooks": [{"type": "command", "command": "notify-send hi"}]}]
}}
JSON
jq -S . "$CLAUDE_SETTINGS_FILE" > "$WORK/before.json"

OUT="$(bash "$INSTALL" --provider claude 2>&1)"; RC=$?
check "install: exit 0" "$RC" "0"
check "install: the hook script is copied and executable" "$([ -x "$CLAUDE_HOOKS_DIR/fleet-state.sh" ] && echo yes || echo no)" "yes"
check "install: a copy of the repo's hook" "$(cmp -s "$CLAUDE_HOOKS_DIR/fleet-state.sh" "$ROOT/config/hooks/fleet-state.sh" && echo same || echo differs)" "same"
for e in $EVENTS; do
  check "install: $e has the aw:fleet entry" "$(jq -r --arg e "$e" '[.hooks[$e][].hooks[].command | select(endswith("# aw:fleet"))] | length' "$CLAUDE_SETTINGS_FILE")" "1"
done
check "install: PreToolUse matcher is .* (every tool)" "$(jq -r '[.hooks.PreToolUse[] | select(.hooks[0].command | endswith("# aw:fleet")) | .matcher] | join(",")' "$CLAUDE_SETTINGS_FILE")" ".*"
check "install: PostToolUse matcher is .* (every tool)" "$(jq -r '[.hooks.PostToolUse[] | select(.hooks[0].command | endswith("# aw:fleet")) | .matcher] | join(",")' "$CLAUDE_SETTINGS_FILE")" ".*"
check "install: the command runs the installed script" "$(jq -r '.hooks.SessionStart[] | select(.hooks[0].command | endswith("# aw:fleet")) | .hooks[0].command' "$CLAUDE_SETTINGS_FILE")" "$CLAUDE_HOOKS_DIR/fleet-state.sh # aw:fleet"
check "install: other hooks and keys survive" "$(jq -r '[.model, .hooks.Stop[0].hooks[0].command, .hooks.PostToolUse[0].hooks[0].command, .hooks.Notification[0].hooks[0].command] | join("|")' "$CLAUDE_SETTINGS_FILE")" "x|~/.claude/hooks/done-gate.sh # aw:done-gate|/usr/local/bin/theirs.sh|notify-send hi"

bash "$INSTALL" --provider claude >/dev/null 2>&1
check "install twice: still exactly one entry per event" "$(fleet_entries "$CLAUDE_SETTINGS_FILE")" "8"

OUT="$(bash "$INSTALL" --uninstall --provider claude 2>&1)"; RC=$?
check "uninstall: exit 0" "$RC" "0"
check "uninstall: no aw:fleet entry is left" "$(fleet_entries "$CLAUDE_SETTINGS_FILE")" "0"
check "uninstall: the settings are back to what they were, other entries untouched" "$(jq -S . "$CLAUDE_SETTINGS_FILE" | cmp -s - "$WORK/before.json" && echo same || echo differs)" "same"
check "uninstall: the copied script is removed" "$([ -e "$CLAUDE_HOOKS_DIR/fleet-state.sh" ] && echo left || echo gone)" "gone"
bash "$INSTALL" --uninstall --provider claude >/dev/null 2>&1
check "uninstall twice: exit 0" "$?" "0"

# Uninstall keeps the state files: they belong to the collector, which prunes them.
mkdir -p "$AW_STATE_DIR/fleet/claude-code"; echo '{}' > "$AW_STATE_DIR/fleet/claude-code/x.json"
bash "$INSTALL" --provider claude >/dev/null 2>&1; bash "$INSTALL" --uninstall --provider claude >/dev/null 2>&1
check "uninstall: the fleet state directory is left alone" "$([ -f "$AW_STATE_DIR/fleet/claude-code/x.json" ] && echo kept || echo gone)" "kept"

# No settings file yet: the install makes one with only the fleet entries.
rm -rf "$WORK/claude"
bash "$INSTALL" --provider claude >/dev/null 2>&1
check "install with no settings file: creates it, 8 entries" "$(fleet_entries "$CLAUDE_SETTINGS_FILE")" "8"
bash "$INSTALL" --uninstall --provider claude >/dev/null 2>&1

# Uninstall with no settings file at all: exit 0, and none is made.
rm -r "$WORK/claude"
bash "$INSTALL" --uninstall --provider claude >/dev/null 2>&1
check "uninstall with no settings file: exit 0, none created" "$?:$([ -e "$CLAUDE_SETTINGS_FILE" ] && echo made || echo none)" "0:none"

# Codex and Cursor are CO-1b: say so, write nothing.
for p in codex cursor; do
  OUT="$(bash "$INSTALL" --provider "$p" 2>&1)"; RC=$?
  check "$p: exit 0 and says it is not installed yet" "$RC:$(printf '%s' "$OUT" | grep -c 'skipped for')" "0:1"
done
check "codex and cursor: nothing written" "$([ -e "$CODEX_HOOKS_FILE" ] || [ -e "$CURSOR_HOOKS_FILE" ] || [ -e "$AW_HOOKS_DIR" ] && echo written || echo none)" "none"

OUT="$(bash "$INSTALL" --provider bogus 2>&1)"; RC=$?
check "unknown provider: exit 1" "$RC" "1"

# --- setup.sh --with-fleet --dry-run prints the installs; without the flag it prints none ---
mkdir -p "$WORK/shome"
run_setup() { PATH="$PATH" HOME="$WORK/shome" AW_STATE_DIR="$WORK/sstate" AW_NO_SYSTEM_INSTALL=1 bash "$ROOT/setup.sh" --providers claude,codex,cursor --dry-run "$@" 2>&1; }
OUT="$(run_setup --with-fleet)"; RC=$?
check "setup.sh --with-fleet --dry-run: exit 0" "$RC" "0"
check "setup.sh --with-fleet --dry-run: prints the install for each provider" "$(printf '%s' "$OUT" | grep -c 'would run scripts/install-fleet.sh --provider')" "3"
OUT="$(run_setup)"
check "setup.sh --dry-run without the flag: no fleet install" "$(printf '%s' "$OUT" | grep -c 'install-fleet')" "0"
check "setup.sh --dry-run wrote nothing under the temp HOME" "$([ -e "$WORK/shome/.claude/settings.json" ] && echo written || echo none)" "none"
exit $fail
