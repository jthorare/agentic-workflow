#!/usr/bin/env bash
# Install the duplicate-script gate (#4221; docs/hooks/duplicate-script.md) for one host: dup-script-gate.sh before a shell
# call or a script write (aw:dup-script-gate). The provider installers run it.
#
#   install-dup-script-guard.sh [--provider claude|codex|cursor] [--uninstall]   AW_DRY_RUN=1 prints what it would do.
#
#   claude  PreToolUse Bash|Write
#   codex   PreToolUse .* (the adapter maps shell to Bash; apply_patch keeps its name and gets advice only)
#   cursor  preToolUse (Shell and Write; Write gets advice only). beforeShellExecution is left out: Cursor fires
#           preToolUse for Shell too, and a second gate would count each suggestion twice.
# The gate needs lib/dup-script.sh, lib/failopen.sh, lib/policy.sh and policy/interventions.json beside it, and finds the toolkit catalog that install-cs ships at
# $AW_STATE_DIR/bin/cs/current/commands.json.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$SCRIPT_DIR/config/hooks/adapters/install-lib.sh"
aw_parse_provider_args "$@" || exit 1
set -- ${AW_ARGS[@]+"${AW_ARGS[@]}"}
UNINSTALL=0
case "${1:-}" in
  "") ;;
  --uninstall) UNINSTALL=1 ;;
  *) echo "usage: install-dup-script-guard.sh [--provider claude|codex|cursor] [--uninstall]" >&2; exit 1 ;;
esac

aw_hooks_init "$AW_PROVIDER"
case "$AW_PROVIDER" in
  claude) EVENT=PreToolUse; MATCHER="Bash|Write" ;;
  codex) EVENT=PreToolUse; MATCHER=".*" ;;
  cursor) EVENT=preToolUse; MATCHER="" ;;
esac

WHERE="$EVENT"; [ -n "$MATCHER" ] && WHERE="$EVENT $MATCHER"
if [ "${AW_DRY_RUN:-0}" = "1" ]; then
  echo "  [dry-run] would $([ "$UNINSTALL" = 1 ] && echo remove || echo install) dup-script-gate ($WHERE) for $AW_PROVIDER in $AW_HOOKS_CONFIG"
  exit 0
fi

if [ "$UNINSTALL" = 1 ]; then
  aw_hook_unset "$EVENT" aw:dup-script-gate
  echo "  dup-script: hook entry removed from $AW_HOOKS_CONFIG (state under \$AW_STATE_DIR/duplicate-script stays)"
  exit 0
fi

if [ "$AW_PROVIDER" = claude ]; then
  mkdir -p "$AW_HOOKS_INSTALL_DIR/lib"
  aw_install_failopen_lib "$AW_HOOKS_INSTALL_DIR"
  cp "$SCRIPT_DIR/config/hooks/dup-script-gate.sh" "$AW_HOOKS_INSTALL_DIR/dup-script-gate.sh"
  chmod +x "$AW_HOOKS_INSTALL_DIR/dup-script-gate.sh"
else
  aw_hooks_stage
fi
cp "$SCRIPT_DIR/config/lib/dup-script.sh" "$AW_HOOKS_INSTALL_DIR/lib/dup-script.sh"
# The mode reader (#4257) and the repo's policy file, laid out as policy.sh looks for it: <hooks>/policy/ beside lib/.
cp "$SCRIPT_DIR/config/lib/policy.sh" "$AW_HOOKS_INSTALL_DIR/lib/policy.sh"
mkdir -p "$AW_HOOKS_INSTALL_DIR/policy"
cp "$SCRIPT_DIR/config/policy/interventions.json" "$AW_HOOKS_INSTALL_DIR/policy/interventions.json"
aw_hook_set "$EVENT" aw:dup-script-gate dup-script-gate.sh "$MATCHER"
echo "  dup-script: dup-script-gate ($WHERE) installed for $AW_PROVIDER in $AW_HOOKS_CONFIG"
