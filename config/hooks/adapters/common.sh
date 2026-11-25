# Shared plumbing for the provider hook adapters (codex.sh, cursor.sh).
# Sourced, never executed. bash 3.2 compatible.
#
# Every adapter is invoked as:
#   <adapter>.sh [--raw] [--event <provider-event>] <hook-script> [args...] [# aw:<id>]
#
#   --raw     pass the provider's stdin through unchanged (probe logging)
#   --event   override the event name (default: stdin .hook_event_name)
#   <hook-script>  a canonical config/hooks/*.sh script — absolute path, or a
#             bare name resolved against the directory above adapters/
#   A trailing "# aw:<id>" owner tag is dropped if the host passed it as argv
#   (i.e. when it exec'd the command without a shell).
#
# The canonical script always sees Claude Code's hook protocol: JSON on stdin
# with {hook_event_name, session_id, transcript_path, cwd, tool_name,
# tool_input, ...}; exit 2 = deny/block with the reason on stdout or stderr;
# exit 0 with optional JSON {hookSpecificOutput: {...}} on stdout.

AW_ADAPTER_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AW_ADAPTER_HOOKS_ROOT="$(cd "$AW_ADAPTER_DIR/.." && pwd)"

# aw_adapter_failopen <provider> <cause> [raw-input] — HOOK-FAILOPEN logging
# for the adapter's own fail-open paths (config/lib/failopen.sh: <hooks>/lib
# when installed, config/lib in a checkout). Cause "input" classifies the host
# payload (empty-input, input-unparsable, …). Runs in a subshell with its output
# discarded, so it can never change what the adapter prints or exits with.
aw_adapter_failopen() {
  (
    set +eu
    for f in "$AW_ADAPTER_DIR/../lib/failopen.sh" "$AW_ADAPTER_DIR/../../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen "$1-adapter" "$2" "${3-}"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}

# aw_adapter_capture <provider> <raw-input> — the capture tap (issue 4382, scripts/retry-guard-capture.sh): with
# AW_HOOK_CAPTURE_DIR naming a folder, saves the host's raw payload there as <provider>.<n>.json (0600), one file per
# hook run. Off unless that variable is set; a failed write is ignored, so it never changes what the adapter does.
aw_adapter_capture() {
  [ -n "${AW_HOOK_CAPTURE_DIR:-}" ] && [ -d "$AW_HOOK_CAPTURE_DIR" ] || return 0
  (
    umask 077
    printf '%s' "$2" > "$AW_HOOK_CAPTURE_DIR/$1.$(date +%s).$$.$RANDOM.json"
  ) 2> /dev/null || true
}

AW_ADAPTER_RAW=0
AW_ADAPTER_EVENT=""
AW_ADAPTER_SCRIPT=""
AW_ADAPTER_ARGS=()

# aw_adapter_parse_args "$@" — fills the AW_ADAPTER_* globals. Returns 1 when
# no hook script was given.
aw_adapter_parse_args() {
  while [ $# -gt 0 ]; do
    case "$1" in
      --raw) AW_ADAPTER_RAW=1; shift ;;
      --event) AW_ADAPTER_EVENT="${2:-}"; shift 2 || shift ;;
      *) break ;;
    esac
  done
  [ $# -gt 0 ] || return 1
  AW_ADAPTER_SCRIPT="$1"; shift
  case "$AW_ADAPTER_SCRIPT" in
    /*) ;;
    *) AW_ADAPTER_SCRIPT="$AW_ADAPTER_HOOKS_ROOT/$AW_ADAPTER_SCRIPT" ;;
  esac
  while [ $# -gt 0 ]; do
    case "$1" in
      '#'*) break ;;
    esac
    AW_ADAPTER_ARGS+=("$1")
    shift
  done
  return 0
}

# aw_adapter_run <normalized-json> <provider> — runs the canonical script with
# the normalized payload on stdin, from the session's working directory.
# Sets AW_RC, AW_OUT, AW_ERR. A missing/non-executable script fails open
# (AW_RC=0, empty output).
aw_adapter_run() {
  local payload="$1" provider="$2" cwd out_f err_f
  AW_RC=0; AW_OUT=""; AW_ERR=""
  if [ ! -f "$AW_ADAPTER_SCRIPT" ]; then
    aw_adapter_failopen "$provider" hook-script-missing
    AW_ERR="adapter: hook script not found: $AW_ADAPTER_SCRIPT"
    return 0
  fi
  cwd="$(printf '%s' "$payload" | jq -r '.cwd // empty' 2>/dev/null)"
  out_f="$(mktemp)"; err_f="$(mktemp)"
  (
    if [ -n "$cwd" ] && [ -d "$cwd" ]; then cd "$cwd" || true; fi
    printf '%s' "$payload" | AW_HOOK_PROVIDER="$provider" \
      bash "$AW_ADAPTER_SCRIPT" ${AW_ADAPTER_ARGS[@]+"${AW_ADAPTER_ARGS[@]}"} > "$out_f" 2> "$err_f"
  )
  AW_RC=$?
  AW_OUT="$(cat "$out_f")"
  AW_ERR="$(cat "$err_f")"
  rm -f "$out_f" "$err_f"
}

# aw_adapter_message — the human-readable reason a canonical script gave for
# an exit 2: its stdout and stderr (either may be empty), joined, trimmed.
aw_adapter_message() {
  local msg=""
  if [ -n "$AW_OUT" ] && ! printf '%s' "$AW_OUT" | jq -e 'type == "object"' > /dev/null 2>&1; then
    msg="$AW_OUT"
  fi
  if [ -n "$AW_ERR" ]; then
    if [ -n "$msg" ]; then msg="$msg
$AW_ERR"; else msg="$AW_ERR"; fi
  fi
  printf '%s' "$msg"
}

# aw_adapter_out_json — AW_OUT if it is a JSON object, else "null".
aw_adapter_out_json() {
  if [ -n "$AW_OUT" ] && printf '%s' "$AW_OUT" | jq -e 'type == "object"' > /dev/null 2>&1; then
    printf '%s' "$AW_OUT" | jq -c .
  else
    printf 'null'
  fi
}
