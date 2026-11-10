# Sourced by bash test suites; defines functions only and sets no shell options. Works on bash 3.2 (macOS) and Linux.
#
# timeout SECS CMD...: GNU `timeout` where it exists, then gtimeout (Homebrew coreutils), then a bash watcher. macOS has none of
# the first two by default. Same contract: the command keeps this shell's stdin, 124 when it was still running after SECS.
# touch_ago FILE "N second|minute|hour|day[s] ago": sets FILE's mtime in the past; `touch -d '7 hours ago'` is GNU only.
if ! command -v timeout > /dev/null 2>&1; then
  timeout() {
    local secs=$1; shift
    if command -v gtimeout > /dev/null 2>&1; then gtimeout "$secs" "$@"; return; fi
    local fired; fired="$(mktemp -u)"
    exec 3<&0
    "$@" <&3 &
    local pid=$!
    ( sleep "$secs"; : > "$fired"; kill -TERM "$pid" 2> /dev/null ) &
    local watcher=$!
    wait "$pid"; local rc=$?
    exec 3<&-
    kill "$watcher" 2> /dev/null; wait "$watcher" 2> /dev/null
    if [ -e "$fired" ]; then command rm -f "$fired"; return 124; fi
    return "$rc"
  }
fi

touch_ago() {
  local f="$1" n unit s epoch stamp
  n="${2%% *}"; unit="${2#* }"; unit="${unit%% *}"
  case "$unit" in
    second*) s=$n ;; minute*) s=$((n * 60)) ;; hour*) s=$((n * 3600)) ;; day*) s=$((n * 86400)) ;;
    *) echo "touch_ago: unit '$unit'" >&2; return 2 ;;
  esac
  epoch=$(( $(date +%s) - s ))
  stamp="$(date -d "@$epoch" +%Y%m%d%H%M.%S 2> /dev/null || date -r "$epoch" +%Y%m%d%H%M.%S)"
  touch -t "$stamp" "$f"
}
