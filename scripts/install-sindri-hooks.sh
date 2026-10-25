#!/usr/bin/env bash
# Install the SessionStart nudge (aw:sindri-nudge) for one host. The C# `sindri` itself is installed by
# scripts/install-cs.sh; this script is only the hook half, run by the provider installers.
#
#   install-sindri-hooks.sh [--provider claude|codex|cursor]
#                                     install the nudge for that host (default claude); AW_DRY_RUN=1 prints it.
#
# The git template hook is a separate, explicit opt-in (it changes global git config): `sindri repo onboard --template`.
# The nudge is silent until sindri is installed, so it may land before or after the install.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$SCRIPT_DIR/config/hooks/adapters/install-lib.sh"
aw_parse_provider_args "$@" || exit 1
if [ ${#AW_ARGS[@]} -gt 0 ]; then
  echo "usage: install-sindri-hooks.sh [--provider claude|codex|cursor]" >&2
  exit 1
fi

aw_hooks_init "$AW_PROVIDER"
case "$AW_PROVIDER" in
  cursor) EVENT=sessionStart ;;
  *) EVENT=SessionStart ;;
esac
if [ "${AW_DRY_RUN:-0}" = "1" ]; then
  echo "  [dry-run] would install sindri-nudge ($EVENT) for $AW_PROVIDER in $AW_HOOKS_CONFIG"
elif [ "$AW_PROVIDER" = "claude" ]; then
  mkdir -p "$AW_HOOKS_INSTALL_DIR"
  cp "$SCRIPT_DIR/config/hooks/sindri-nudge.sh" "$AW_HOOKS_INSTALL_DIR/sindri-nudge.sh"
  chmod +x "$AW_HOOKS_INSTALL_DIR/sindri-nudge.sh"
  ENTRY=$(jq -nc --arg c "$AW_HOOKS_INSTALL_DIR/sindri-nudge.sh # aw:sindri-nudge" '{hooks:[{type:"command",command:$c}]}')
  merge_hook "$AW_HOOKS_CONFIG" SessionStart aw:sindri-nudge "$ENTRY"
  echo "  sindri: SessionStart nudge installed for claude in $AW_HOOKS_CONFIG"
else
  aw_hooks_stage
  aw_hook_set "$EVENT" aw:sindri-nudge sindri-nudge.sh
  echo "  sindri: $EVENT nudge installed for $AW_PROVIDER in $AW_HOOKS_CONFIG"
fi
