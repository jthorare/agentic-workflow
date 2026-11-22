#!/usr/bin/env bash
# #3778 (d): each installer's host timeout is at least twice the hook's own wall-clock budget, so the host kill is a backstop
# that a stall doesn't reach: the hook ends itself, prints nothing and writes one failopen `timeout` row.
# Reads the numbers from the sources: the hook's default budget (${AW_*_BUDGET_MS:-N}) and the `timeout:N` of its installer entry.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
# budget_ms <hook file> <env name>: the default after ${NAME:-
budget_ms() { sed -n "s/.*\${$2:-\([0-9][0-9]*\)}.*/\1/p" "$1" | head -1; }
# host_timeout <installer> <hook marker>: the timeout:N on the line that registers the hook
host_timeout() { grep -F "$2" "$1" | sed -n 's/.*timeout:\([0-9][0-9]*\).*/\1/p' | head -1; }
# covers <label> <budget ms> <host seconds>
covers() {
  local label="$1" ms="$2" s="$3"
  if [ -z "$ms" ] || [ -z "$s" ]; then check "$label: budget and timeout found" "ms=$ms s=$s" "both numbers"; return; fi
  check "$label: host timeout ${s}s is at least 2x the ${ms} ms budget" "$([ $((s * 1000)) -ge $((ms * 2)) ] && echo yes || echo no)" "yes"
}

covers "sindri-tasks" "$(budget_ms "$ROOT/config/hooks/sindri-tasks.sh" AW_SINDRI_TASKS_BUDGET_MS)" "$(host_timeout "$ROOT/scripts/install-sindri-hooks.sh" '# aw:sindri-tasks')"
covers "prompt-sort" "$(budget_ms "$ROOT/config/hooks/prompt-sort.sh" AW_PROMPT_SORT_BUDGET_MS)" "$(host_timeout "$ROOT/scripts/install-judge-hooks.sh" '# aw:prompt-sort')"
covers "done-gate" "$(budget_ms "$ROOT/config/hooks/done-gate.sh" AW_DONE_GATE_BUDGET_MS)" "$(host_timeout "$ROOT/scripts/install-done-gate.sh" '# aw:done-gate"')"

[ "$fail" = 0 ] && echo "all passed" || exit 1
