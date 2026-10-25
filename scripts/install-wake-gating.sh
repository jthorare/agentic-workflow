#!/usr/bin/env bash
# Wake gating (lever 1A): send-gate.sh + record-teammate-name.sh (PreToolUse)
# and outbox-flush.sh (TeammateIdle). Deliberately NOT part of installing
# judge itself — the user approves live steps one at a time, and "install judge"
# silently turning on the send gate for every future SendMessage would
# violate that. Split out in the style of install-context-guard.sh.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SETTINGS_FILE="${CLAUDE_SETTINGS_FILE:-$HOME/.claude/settings.json}"
HOOKS_DIR="${CLAUDE_HOOKS_DIR:-$HOME/.claude/hooks}"
STATE_DIR="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
JUDGE_BIN="${AW_JUDGE_BIN:-judge}"
# shellcheck source=../config/lib/merge-hook.sh
source "$ROOT/config/lib/merge-hook.sh"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$ROOT/config/hooks/adapters/install-lib.sh"
# Usage: scripts/install-wake-gating.sh [--provider claude|codex|cursor] [--uninstall]
aw_parse_provider_args "$@" || exit 1
set -- ${AW_ARGS[@]+"${AW_ARGS[@]}"}

if [ "$AW_PROVIDER" != "claude" ]; then
  aw_hooks_init "$AW_PROVIDER"
  aw_unsupported wake-gating "no TeammateIdle event and no teammate SendMessage tool with a verified argument shape"
  exit 0
fi

if [ "${1:-}" = "--uninstall" ]; then
  merge_hook "$SETTINGS_FILE" PreToolUse aw:send-gate null
  merge_hook "$SETTINGS_FILE" PreToolUse aw:record-teammate-name null
  merge_hook "$SETTINGS_FILE" TeammateIdle aw:outbox-flush null
  echo "  wake-gating: hook entries removed from $SETTINGS_FILE"

  OUTBOX_DIR="$STATE_DIR/judge/outbox"
  if [ -d "$OUTBOX_DIR" ]; then
    # Only ever remove state we created and only when it's actually empty of
    # queued items — an item still in outbox/*.jsonl means an orchestrator is
    # relying on it to be flushed; deleting it out from under that would lose
    # real progress, not just tear down a hook.
    QUEUED_FILES="$(find "$OUTBOX_DIR" -maxdepth 1 -name '*.jsonl' ! -name 'expired.jsonl' ! -empty 2>/dev/null)"
    if [ -n "$QUEUED_FILES" ]; then
      echo "  wake-gating: WARN: $OUTBOX_DIR has queued items, leaving it in place:"
      printf '    %s\n' $QUEUED_FILES
    else
      rm -r "$OUTBOX_DIR"
      echo "  wake-gating: removed empty $OUTBOX_DIR"
    fi
  fi

  TEAMMATE_NAMES_FILE="$STATE_DIR/judge/teammate-names.jsonl"
  if [ -f "$TEAMMATE_NAMES_FILE" ]; then
    rm "$TEAMMATE_NAMES_FILE"
    echo "  wake-gating: removed $TEAMMATE_NAMES_FILE"
  fi

  if [ -f "$HOOKS_DIR/lib/locks.sh" ]; then
    rm "$HOOKS_DIR/lib/locks.sh"
    echo "  wake-gating: removed $HOOKS_DIR/lib/locks.sh"
  fi
  if [ -d "$HOOKS_DIR/lib" ] && [ -z "$(ls -A "$HOOKS_DIR/lib" 2>/dev/null)" ]; then
    rmdir "$HOOKS_DIR/lib"
    echo "  wake-gating: removed empty $HOOKS_DIR/lib"
  elif [ -d "$HOOKS_DIR/lib" ]; then
    echo "  wake-gating: left $HOOKS_DIR/lib in place (holds other files)"
  fi
  exit 0
fi

if ! command -v "$JUDGE_BIN" > /dev/null 2>&1; then
  echo "  wake-gating: refusing to install — judge is not installed (run ./setup.sh, or set AW_JUDGE_BIN)" >&2
  exit 1
fi

mkdir -p "$HOOKS_DIR"

# send-gate.sh's locking (config/lib/locks.sh) must be installed alongside
# it — a bare `cp` of send-gate.sh alone left `../lib/locks.sh` unresolvable
# in every real install (only a raw repo checkout has that sibling
# directory), silently disabling the whole outbox-queue/digest mechanism
# with no error (2026-09-27 finding). send-gate.sh looks for it at
# "$(dirname "${BASH_SOURCE[0]}")/lib/locks.sh" first — i.e. right here.
mkdir -p "$HOOKS_DIR/lib"
cp "$ROOT/config/lib/locks.sh" "$HOOKS_DIR/lib/locks.sh"
aw_install_failopen_lib "$HOOKS_DIR"

cp "$ROOT/config/hooks/send-gate.sh" "$HOOKS_DIR/send-gate.sh"
chmod +x "$HOOKS_DIR/send-gate.sh"
SEND_GATE_ENTRY=$(jq -nc --arg c "$HOOKS_DIR/send-gate.sh # aw:send-gate" '{matcher:"SendMessage", hooks:[{type:"command",command:$c}]}')
merge_hook "$SETTINGS_FILE" PreToolUse aw:send-gate "$SEND_GATE_ENTRY"

cp "$ROOT/config/hooks/record-teammate-name.sh" "$HOOKS_DIR/record-teammate-name.sh"
chmod +x "$HOOKS_DIR/record-teammate-name.sh"
NAME_ENTRY=$(jq -nc --arg c "$HOOKS_DIR/record-teammate-name.sh # aw:record-teammate-name" '{matcher:"Agent", hooks:[{type:"command",command:$c}]}')
merge_hook "$SETTINGS_FILE" PreToolUse aw:record-teammate-name "$NAME_ENTRY"

cp "$ROOT/config/hooks/outbox-flush.sh" "$HOOKS_DIR/outbox-flush.sh"
chmod +x "$HOOKS_DIR/outbox-flush.sh"
FLUSH_ENTRY=$(jq -nc --arg c "$HOOKS_DIR/outbox-flush.sh # aw:outbox-flush" '{hooks:[{type:"command",command:$c}]}')
merge_hook "$SETTINGS_FILE" TeammateIdle aw:outbox-flush "$FLUSH_ENTRY"

echo "  wake-gating: send-gate, record-teammate-name, outbox-flush hooks installed (lever 1A)"
