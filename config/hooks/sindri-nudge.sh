#!/usr/bin/env bash
# aw:sindri-nudge — SessionStart hook. Prints one line when the session's repo is not in the
# approved sindri profile ("run: sindri repo onboard"). Silent outside git, when sindri isn't
# installed, when no profile is approved yet, when the repo is onboarded, and in child sessions
# (AW_JUDGE_CHILD, AW_SINDRI_CHILD). Fails open: always exits 0, and the CLI is killed after
# AW_SINDRI_NUDGE_BUDGET_MS (default 4000). The CLI reads the approved profile read-only: it never
# migrates or writes the ledger and takes no lock.
[ -n "${AW_JUDGE_CHILD:-}${AW_SINDRI_CHILD:-}" ] && exit 0
# HOOK-FAILOPEN: record why this hook printed nothing (config/lib/failopen.sh, installed at <hooks>/lib/failopen.sh).
# Logging only, in a subshell with its output discarded: it never changes this hook's exit code or output.
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen sindri-nudge "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
SINDRI="$(command -v sindri 2>/dev/null || echo "$HOME/.local/bin/sindri")"
[ -x "$SINDRI" ] || exit 0
git rev-parse --is-inside-work-tree >/dev/null 2>&1 || exit 0
BUDGET_MS="${AW_SINDRI_NUDGE_BUDGET_MS:-4000}"
case "$BUDGET_MS" in ''|*[!0-9]*) BUDGET_MS=4000 ;; esac
TICKS=$((BUDGET_MS / 100))
OUT_FILE="$(mktemp 2>/dev/null)" || exit 0
trap 'rm -f "$OUT_FILE"' EXIT
( "$SINDRI" repo status --nudge > "$OUT_FILE" 2>/dev/null ) > /dev/null 2>&1 &
PID=$!
while kill -0 "$PID" 2>/dev/null; do
  if [ "$TICKS" -le 0 ]; then
    pkill -P "$PID" 2>/dev/null
    kill -9 "$PID" 2>/dev/null
    failopen timeout
    exit 0
  fi
  TICKS=$((TICKS - 1))
  sleep 0.1
done
wait "$PID" 2>/dev/null || exit 0
head -n 1 "$OUT_FILE"
exit 0
