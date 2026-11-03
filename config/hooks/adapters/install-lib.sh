# Provider-aware hook installation helpers. Source this file (bash 3.2 safe).
# Used by providers/<name>/install-hooks.sh, the scripts/install-*.sh lever
# installers, and scripts/probe.sh.
#
#   aw_hooks_init <claude|codex|cursor>   sets AW_HOOK_PROVIDER, AW_HOOKS_CONFIG,
#                                         AW_HOOKS_INSTALL_DIR, AW_HOOKS_FLAT
#   aw_hooks_stage                        copy canonical hooks + adapters + lib/locks.sh
#                                         into AW_HOOKS_INSTALL_DIR (codex/cursor)
#   aw_hook_set <event> <id> <script> [matcher] [timeout] [script-args...]
#                                         install one owned entry, wrapped in the
#                                         provider adapter (codex/cursor)
#   aw_hook_set_cmd <event> <id> <command> [matcher] [timeout]
#                                         install one owned entry verbatim
#   aw_hook_unset <event> <id>            remove our entry (never touches others)
#   aw_parse_provider_args "$@"           sets AW_PROVIDER (default claude) and
#                                         AW_ARGS (remaining args, bash array)
#
# Locations (all overridable, so tests never touch a real home dir):
#   claude  config ${CLAUDE_SETTINGS_FILE:-~/.claude/settings.json}
#           scripts ${CLAUDE_HOOKS_DIR:-~/.claude/hooks}
#   codex   config ${CODEX_HOOKS_FILE:-${CODEX_HOME:-~/.codex}/hooks.json}
#           scripts ${AW_HOOKS_DIR:-${AW_STATE_DIR:-~/.agentic-workflow}/hooks}
#   cursor  config ${CURSOR_HOOKS_FILE:-~/.cursor/hooks.json}
#           scripts ${AW_HOOKS_DIR:-${AW_STATE_DIR:-~/.agentic-workflow}/hooks}

AW_INSTALL_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AW_TOOLKIT_ROOT="$(cd "$AW_INSTALL_LIB_DIR/../../.." && pwd)"
# shellcheck source=../../lib/merge-hook.sh
source "$AW_TOOLKIT_ROOT/config/lib/merge-hook.sh"

aw_parse_provider_args() {
  AW_PROVIDER="claude"
  AW_ARGS=()
  while [ $# -gt 0 ]; do
    case "$1" in
      --provider) AW_PROVIDER="${2:-}"; shift 2 || shift ;;
      --provider=*) AW_PROVIDER="${1#--provider=}"; shift ;;
      *) AW_ARGS+=("$1"); shift ;;
    esac
  done
  case "$AW_PROVIDER" in
    claude|codex|cursor) return 0 ;;
    *) echo "unknown provider '$AW_PROVIDER' (expected claude, codex, or cursor)" >&2; return 1 ;;
  esac
}

aw_hooks_init() {
  AW_HOOK_PROVIDER="$1"
  local state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
  case "$AW_HOOK_PROVIDER" in
    claude)
      AW_HOOKS_CONFIG="${CLAUDE_SETTINGS_FILE:-$HOME/.claude/settings.json}"
      AW_HOOKS_INSTALL_DIR="${CLAUDE_HOOKS_DIR:-$HOME/.claude/hooks}"
      AW_HOOKS_FLAT=0 ;;
    codex)
      AW_HOOKS_CONFIG="${CODEX_HOOKS_FILE:-${CODEX_HOME:-$HOME/.codex}/hooks.json}"
      AW_HOOKS_INSTALL_DIR="${AW_HOOKS_DIR:-$state/hooks}"
      AW_HOOKS_FLAT=0 ;;
    cursor)
      AW_HOOKS_CONFIG="${CURSOR_HOOKS_FILE:-$HOME/.cursor/hooks.json}"
      AW_HOOKS_INSTALL_DIR="${AW_HOOKS_DIR:-$state/hooks}"
      AW_HOOKS_FLAT=1 ;;
    *) echo "unknown provider '$AW_HOOK_PROVIDER'" >&2; return 1 ;;
  esac
}

# aw_install_failopen_lib <hooks-dir> — copy config/lib/failopen.sh to
# <hooks-dir>/lib/, where every fail-open hook looks for its HOOK-FAILOPEN logger, and config/lib/image-paths.sh beside it,
# where the hooks that attach images for the judge (done-gate, scope-gate, send-gate, prompt-sort) look.
aw_install_failopen_lib() {
  mkdir -p "$1/lib"
  cp "$AW_TOOLKIT_ROOT/config/lib/failopen.sh" "$1/lib/failopen.sh"
  cp "$AW_TOOLKIT_ROOT/config/lib/image-paths.sh" "$1/lib/image-paths.sh"
}

# Copy every canonical hook, the adapters, lib/locks.sh (send-gate.sh
# resolves it at <hooks-dir>/lib/locks.sh) and lib/failopen.sh into the
# provider-neutral dir.
aw_hooks_stage() {
  local f
  mkdir -p "$AW_HOOKS_INSTALL_DIR/adapters" "$AW_HOOKS_INSTALL_DIR/lib"
  for f in "$AW_TOOLKIT_ROOT/config/hooks/"*.sh; do
    [ -f "$f" ] || continue
    cp "$f" "$AW_HOOKS_INSTALL_DIR/$(basename "$f")"
    chmod +x "$AW_HOOKS_INSTALL_DIR/$(basename "$f")"
  done
  for f in "$AW_TOOLKIT_ROOT/config/hooks/adapters/"*.sh; do
    [ -f "$f" ] || continue
    cp "$f" "$AW_HOOKS_INSTALL_DIR/adapters/$(basename "$f")"
    chmod +x "$AW_HOOKS_INSTALL_DIR/adapters/$(basename "$f")"
  done
  cp "$AW_TOOLKIT_ROOT/config/lib/locks.sh" "$AW_HOOKS_INSTALL_DIR/lib/locks.sh"
  aw_install_failopen_lib "$AW_HOOKS_INSTALL_DIR"
}

aw_hook_set_cmd() {
  local event="$1" id="$2" cmd="$3" matcher="${4:-}" timeout="${5:-}" entry
  cmd="$cmd # $id"
  if [ "$AW_HOOKS_FLAT" = "1" ]; then
    entry="$(jq -nc --arg c "$cmd" --arg m "$matcher" --arg t "$timeout" \
      '{command: $c} + (if $m == "" then {} else {matcher: $m} end)
                     + (if $t == "" then {} else {timeout: ($t | tonumber)} end)')"
    merge_hook_flat "$AW_HOOKS_CONFIG" "$event" "$id" "$entry"
  else
    entry="$(jq -nc --arg c "$cmd" --arg m "$matcher" --arg t "$timeout" \
      '(if $m == "" then {} else {matcher: $m} end)
       + {hooks: [{type: "command", command: $c}
                  + (if $t == "" then {} else {timeout: ($t | tonumber)} end)]}')"
    merge_hook "$AW_HOOKS_CONFIG" "$event" "$id" "$entry"
  fi
}

aw_hook_set() {
  local event="$1" id="$2" script="$3" matcher="${4:-}" timeout="${5:-}" cmd
  shift 3; [ $# -gt 0 ] && shift; [ $# -gt 0 ] && shift
  case "$AW_HOOK_PROVIDER" in
    claude) cmd="$AW_HOOKS_INSTALL_DIR/$script" ;;
    *) cmd="$AW_HOOKS_INSTALL_DIR/adapters/$AW_HOOK_PROVIDER.sh ${AW_HOOK_ADAPTER_FLAGS:+$AW_HOOK_ADAPTER_FLAGS }$AW_HOOKS_INSTALL_DIR/$script" ;;
  esac
  [ $# -gt 0 ] && cmd="$cmd $*"
  aw_hook_set_cmd "$event" "$id" "$cmd" "$matcher" "$timeout"
}

aw_hook_unset() {
  if [ "$AW_HOOKS_FLAT" = "1" ]; then
    [ -f "$AW_HOOKS_CONFIG" ] || return 0
    merge_hook_flat "$AW_HOOKS_CONFIG" "$1" "$2" null
  else
    [ -f "$AW_HOOKS_CONFIG" ] || return 0
    merge_hook "$AW_HOOKS_CONFIG" "$1" "$2" null
  fi
}

# aw_unsupported <lever> <reason> — a lever that has no equivalent event on
# the chosen provider. Prints why and succeeds (so a multi-provider loop
# keeps going); nothing is written.
aw_unsupported() {
  echo "  $1: skipped for $AW_HOOK_PROVIDER — $2 (see config/hooks/adapters/README.md)"
}
