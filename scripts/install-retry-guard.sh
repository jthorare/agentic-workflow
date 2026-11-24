#!/usr/bin/env bash
# Install the retry guard (issue 4248; docs/hooks/retry-guard.md) for one host: retry-gate.sh before every tool call
# (aw:retry-gate) and retry-record.sh after it (aw:retry-record). The provider installers run it.
#
#   install-retry-guard.sh [--provider claude|codex|cursor] [--uninstall]   AW_DRY_RUN=1 prints what it would do.
#
#   claude  PreToolUse .* (gate); PostToolUseFailure .* and PostToolUse .* (record: a failure, or a success that clears)
#   codex   PreToolUse .* (gate); PostToolUse .* (record: the adapter marks a nonzero shell exit as a failure)
#   cursor  preToolUse (gate); postToolUseFailure and postToolUse (record). beforeShellExecution is left out: Cursor
#           fires preToolUse for Shell too, and a second gate would count each deny twice.
# The hooks need lib/retry-guard.sh, lib/locks.sh (with lib/heavy-config.sh) and lib/failopen.sh beside them.
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
  *) echo "usage: install-retry-guard.sh [--provider claude|codex|cursor] [--uninstall]" >&2; exit 1 ;;
esac

aw_hooks_init "$AW_PROVIDER"
case "$AW_PROVIDER" in
  claude) GATE_EVENTS="PreToolUse"; RECORD_EVENTS="PostToolUseFailure PostToolUse"; MATCHER=".*" ;;
  codex) GATE_EVENTS="PreToolUse"; RECORD_EVENTS="PostToolUse"; MATCHER=".*" ;;
  cursor) GATE_EVENTS="preToolUse"; RECORD_EVENTS="postToolUseFailure postToolUse"; MATCHER="" ;;
esac

if [ "${AW_DRY_RUN:-0}" = "1" ]; then
  echo "  [dry-run] would $([ "$UNINSTALL" = 1 ] && echo remove || echo install) retry-gate ($GATE_EVENTS) and retry-record ($RECORD_EVENTS) for $AW_PROVIDER in $AW_HOOKS_CONFIG"
  exit 0
fi

if [ "$UNINSTALL" = 1 ]; then
  for ev in $GATE_EVENTS; do aw_hook_unset "$ev" aw:retry-gate; done
  for ev in $RECORD_EVENTS; do aw_hook_unset "$ev" aw:retry-record; done
  echo "  retry-guard: hook entries removed from $AW_HOOKS_CONFIG (state files under \$AW_STATE_DIR/retry-guard stay; turn-origin sweeps them after a day)"
  exit 0
fi

if [ "$AW_PROVIDER" = claude ]; then
  mkdir -p "$AW_HOOKS_INSTALL_DIR/lib"
  aw_install_failopen_lib "$AW_HOOKS_INSTALL_DIR"
  for h in retry-gate.sh retry-record.sh; do
    cp "$SCRIPT_DIR/config/hooks/$h" "$AW_HOOKS_INSTALL_DIR/$h"
    chmod +x "$AW_HOOKS_INSTALL_DIR/$h"
  done
else
  aw_hooks_stage
fi
for f in retry-guard.sh locks.sh heavy-config.sh; do
  cp "$SCRIPT_DIR/config/lib/$f" "$AW_HOOKS_INSTALL_DIR/lib/$f"
done
for ev in $GATE_EVENTS; do aw_hook_set "$ev" aw:retry-gate retry-gate.sh "$MATCHER"; done
for ev in $RECORD_EVENTS; do aw_hook_set "$ev" aw:retry-record retry-record.sh "$MATCHER"; done
echo "  retry-guard: retry-gate ($GATE_EVENTS) and retry-record ($RECORD_EVENTS) installed for $AW_PROVIDER in $AW_HOOKS_CONFIG"
