#!/usr/bin/env bash
# One iteration of ui-before-after: refreshes proof-main, brings main then the PR up, runs the scenario on
# both builds in parallel, composes the labelled pairs, and prints one line per output file.
#
#   proof.sh <pr-number-or-name> <pr-worktree> <scenario.mjs> [--down] [--storybook] [--axe] [--pixel] [--record]
#     The PR need not exist yet; the label is the worktree's HEAD. Outputs go to ~/.agentic-workflow/vitalizecare-web-app/ui-before-after/pr-<n>/. Stacks stay up for the next iteration unless --down.
#     --storybook runs the scenario on each worktree's Storybook instead of the app. --axe saves
#     each state's axe violations. --pixel writes pixel-diff.txt: which pairs are pixel-identical.
#     --record saves a video of each side.
#     proof-main is shared: each call renews this run's lease on it, and its sha may lag origin/main
#     while other runs hold leases. --down releases the lease and stops main only if it was the last.
#     Exit codes: those of stack.sh (2: AWS SSO expired, 3: port held, 4: not ready, 5: start lock
#     busy), 1: a side's run failed or a step failed, 64: usage.
set -u

HERE="$(cd "$(dirname "$0")" && pwd)"
WEB_APP_ROOT="${WEB_APP_ROOT:-$HOME/work/web-app}"
STATE_DIR="${UI_BEFORE_AFTER_STATE_DIR:-$HOME/.agentic-workflow/vitalizecare-web-app/ui-before-after/.stacks}"
OUT_ROOT="${UI_BEFORE_AFTER_OUT_ROOT:-$HOME/.agentic-workflow/vitalizecare-web-app/ui-before-after}"
STACK="${UI_BEFORE_AFTER_STACK:-bash $HERE/stack.sh}"
MAIN_WORKTREE="${UI_BEFORE_AFTER_MAIN_WORKTREE:-bash $HERE/main-worktree.sh}"
LEASE="${UI_BEFORE_AFTER_LEASE:-bash $HERE/lease.sh}"
RELEASE="${UI_BEFORE_AFTER_RELEASE:-bash $HERE/release.sh}"
RUN="${UI_BEFORE_AFTER_RUN:-node $HERE/run-scenario.mjs}"
COMPOSE="${UI_BEFORE_AFTER_COMPOSE:-node $HERE/compose.mjs}"

[ $# -ge 3 ] || {
  echo "usage: proof.sh <pr-number-or-name> <pr-worktree> <scenario.mjs> [--down] [--storybook] [--axe] [--pixel] [--record]" >&2
  exit 64
}
pr="$1"
pr_worktree="$2"
scenario="$3"
shift 3
down=0
stack_options=""
run_options=""
compose_options=""
for option in "$@"; do
  case "$option" in
    --down) down=1 ;;
    --storybook) stack_options="--storybook"; run_options="$run_options --storybook" ;;
    --axe) run_options="$run_options --axe" ;;
    --pixel) compose_options="--pixel" ;;
    --record) run_options="$run_options --record" ;;
    *) echo "unknown option: $option" >&2; exit 64 ;;
  esac
done
out="$OUT_ROOT/pr-$pr"

# The PR @sha label is HEAD, so it must be what the build shows. This also stops a run on a
# worktree another session is part way through removing.
if [ -n "$(git -C "$pr_worktree" status --porcelain)" ]; then
  echo "$pr_worktree has uncommitted changes; commit them, or wait if another session is removing it" >&2
  exit 1
fi

main_worktree=""
stop_stacks() {
  $RELEASE "$pr" $main_worktree
  $STACK down "$pr_worktree"
}
[ "$down" -eq 1 ] && trap stop_stacks EXIT

seed_day() {
  sed -n 's/.*"seedDay": *"\([^"]*\)".*/\1/p' "$STATE_DIR/$(basename "$1").json" 2>/dev/null
}

# The slot's Postgres, so a scenario can find seeded profiles to impersonate.
database_option() {
  [ -n "$stack_options" ] && return
  echo "--db $(cd "$1" && bash scripts/worktree.sh info | sed -n 's/^ *Postgres: *//p')"
}

# Prints the stack's WEB_URL; the caller's exit code carries stack.sh's.
bring_up() {
  local output status
  output=$($STACK up "$@")
  status=$?
  [ "$status" -eq 0 ] || return "$status"
  echo "$output" | sed -n 's/^WEB_URL=//p'
}

# Before proof-main is refreshed, so an expired session costs nothing.
[ -n "$stack_options" ] || $STACK sso || exit $?
main_worktree=$($MAIN_WORKTREE "$WEB_APP_ROOT" "$pr") || exit 1
main_sha=$(git -C "$main_worktree" rev-parse --short=7 HEAD) || exit 1
pr_sha=$(git -C "$pr_worktree" rev-parse --short=7 HEAD) || exit 1

main_reseed=""
pr_reseed=""
main_day=$(seed_day "$main_worktree")
pr_day=$(seed_day "$pr_worktree")
# Storybook has no database, so seed days don't matter there.
if [ -z "$stack_options" ] && [ -n "$main_day" ] && [ -n "$pr_day" ] && [ "$main_day" != "$pr_day" ]; then
  if [ "$main_day" \< "$pr_day" ]; then main_reseed="--reseed"; else pr_reseed="--reseed"; fi
fi
main_others=$($LEASE others "$pr") || exit 1
if [ -n "$main_reseed" ] && [ "$main_others" -gt 0 ]; then
  echo "not reseeding proof-main: $main_others other run(s) hold it"
  main_reseed=""
fi

main_url=$(bring_up "$main_worktree" $main_reseed $stack_options) || exit $?
pr_url=$(bring_up "$pr_worktree" $pr_reseed $stack_options) || exit $?

mkdir -p "$out"
$RUN "$scenario" --base "$main_url" --side main --out "$out" $(database_option "$main_worktree") $run_options >"$out/run-main.log" 2>&1 &
main_run=$!
$RUN "$scenario" --base "$pr_url" --side pr --out "$out" $(database_option "$pr_worktree") $run_options >"$out/run-pr.log" 2>&1 &
pr_run=$!
failed=0
wait "$main_run" || { echo "main run failed:" >&2; cat "$out/run-main.log" >&2; failed=1; }
wait "$pr_run" || { echo "pr run failed:" >&2; cat "$out/run-pr.log" >&2; failed=1; }
[ "$failed" -eq 0 ] || exit 1

$COMPOSE "$out" --main-sha "$main_sha" --pr-sha "$pr_sha" $compose_options || exit 1
find "$out" -mindepth 2 \( -name '*.webm' -o -name '*.aria.txt' -o -name '*.axe.json' -o -name '*.figma.txt' -o -name '*.missing.txt' \) | sort
