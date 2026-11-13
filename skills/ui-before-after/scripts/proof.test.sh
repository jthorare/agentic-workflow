#!/usr/bin/env bash
# Tests for proof.sh against fake stack, run and compose scripts so no stack or browser starts.
set -u

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; return 0; }
trap __aw_tmp_clean EXIT

PROOF="$(cd "$(dirname "$0")" && pwd)/proof.sh"
LEASE="$(dirname "$PROOF")/lease.sh"
FAILURES=0

fail() {
  echo "FAIL: $1"
  FAILURES=$((FAILURES + 1))
}

# Prints a scratch dir holding fake scripts. Their calls go to calls.log; a file named
# <stack|run>-exit-<worktree or side> makes that call exit with the file's number.
make_fixture() {
  local dir
  dir=$(mktemp -d)
  mkdir -p "$dir/bin" "$dir/state" "$dir/pr-worktree" "$dir/main-repo"
  for repo in main-repo pr-worktree; do
    mkdir -p "$dir/$repo/scripts"
    printf '#!/usr/bin/env bash\necho "  Postgres:  postgres://localhost:5432/%s"\n' "$repo" >"$dir/$repo/scripts/worktree.sh"
    git -C "$dir/$repo" init -q
    git -C "$dir/$repo" add scripts
    git -C "$dir/$repo" -c user.name=t -c user.email=t@t commit -q -m init
  done
  cat >"$dir/bin/stack" <<'EOF'
#!/usr/bin/env bash
echo "stack $*" >>"$FAKE_DIR/calls.log"
name=$(basename "${2:-$1}")
[ -f "$FAKE_DIR/stack-exit-$name" ] && exit "$(cat "$FAKE_DIR/stack-exit-$name")"
if [ "$1" = up ]; then
  [ "$name" = main-repo ] && echo "WEB_URL=http://localhost:3010" || echo "WEB_URL=http://localhost:3020"
  echo "STARTED_BY_UI_BEFORE_AFTER=1"
fi
EOF
  cat >"$dir/bin/main-worktree" <<'EOF'
#!/usr/bin/env bash
echo "main-worktree $*" >>"$FAKE_DIR/calls.log"
echo "$FAKE_DIR/main-repo"
EOF
  cat >"$dir/bin/run" <<'EOF'
#!/usr/bin/env bash
echo "run $*" >>"$FAKE_DIR/calls.log"
while [ $# -gt 0 ]; do
  case "$1" in --side) side="$2" ;; --out) out="$2" ;; --record) record=1 ;; esac
  shift
done
[ -f "$FAKE_DIR/run-exit-$side" ] && { echo "$side: auto sign-in did not happen" >&2; exit "$(cat "$FAKE_DIR/run-exit-$side")"; }
mkdir -p "$out/$side"
touch "$out/$side/field.aria.txt"
if [ -n "${record:-}" ]; then touch "$out/$side/$side.webm"; fi
EOF
  cat >"$dir/bin/compose" <<'EOF'
#!/usr/bin/env bash
echo "compose $*" >>"$FAKE_DIR/calls.log"
printf 'field\t%s/field.png\n' "$1"
EOF
  cat >"$dir/bin/gh" <<'EOF'
#!/usr/bin/env bash
echo "gh $*" >>"$FAKE_DIR/calls.log"
exit 1
EOF
  chmod +x "$dir"/bin/*
  echo "$dir"
}

run_proof() {
  local dir="$1"
  shift
  FAKE_DIR="$dir" PATH="$dir/bin:$PATH" \
    UI_BEFORE_AFTER_STACK="$dir/bin/stack" UI_BEFORE_AFTER_MAIN_WORKTREE="$dir/bin/main-worktree" \
    UI_BEFORE_AFTER_RUN="$dir/bin/run" UI_BEFORE_AFTER_COMPOSE="$dir/bin/compose" \
    UI_BEFORE_AFTER_STATE_DIR="$dir/state" UI_BEFORE_AFTER_OUT_ROOT="$dir/out" WEB_APP_ROOT="$dir/web-app" \
    bash "$PROOF" 20057 "$dir/pr-worktree" "$dir/scenario.mjs" "$@" 2>&1
}

test_brings_main_up_then_the_pr_runs_both_sides_and_composes_with_both_shas() {
  local dir out status main_sha pr_sha
  dir=$(make_fixture)
  main_sha=$(git -C "$dir/main-repo" rev-parse --short=7 HEAD)
  pr_sha=$(git -C "$dir/pr-worktree" rev-parse --short=7 HEAD)
  out=$(run_proof "$dir")
  status=$?
  [ "$status" -eq 0 ] || fail "happy path: exit $status: $out"
  [ "$(grep -n '^stack up' "$dir/calls.log" | head -1 | cut -d' ' -f3)" = "$dir/main-repo" ] || fail "happy path: main did not come up first: $(grep '^stack' "$dir/calls.log")"
  grep -q "^stack up $dir/pr-worktree\$" "$dir/calls.log" || fail "happy path: pr stack not brought up"
  grep -q -- "--base http://localhost:3010 --side main --out $dir/out/pr-20057" "$dir/calls.log" || fail "happy path: main run args: $(grep '^run' "$dir/calls.log")"
  grep -q -- "--base http://localhost:3020 --side pr --out $dir/out/pr-20057" "$dir/calls.log" || fail "happy path: pr run args"
  grep -q -- "--main-sha $main_sha --pr-sha $pr_sha" "$dir/calls.log" || fail "happy path: compose shas: $(grep '^compose' "$dir/calls.log")"
  grep -q '^gh' "$dir/calls.log" && fail "happy path: asked GitHub for the sha"
  grep -q '^stack down' "$dir/calls.log" && fail "happy path: stacks were brought down without --down"
  [[ "$out" == *"field"*"field.png"* ]] || fail "happy path: output lacks the composed image: $out"
  [[ "$out" == *".webm"* ]] && fail "happy path: recorded without --record: $out"
  [[ "$out" == *"field.aria.txt"* ]] || fail "happy path: output lacks the aria text: $out"
}

test_stops_with_the_stack_exit_code_when_a_stack_will_not_come_up() {
  local dir out status
  dir=$(make_fixture)
  echo 2 >"$dir/stack-exit-main-repo"
  out=$(run_proof "$dir")
  status=$?
  [ "$status" -eq 2 ] || fail "stack failure: exit $status, want 2"
  grep -q '^run' "$dir/calls.log" && fail "stack failure: a scenario ran anyway"
  grep -q "^stack up $dir/pr-worktree" "$dir/calls.log" && fail "stack failure: pr stack started after main failed"
}

test_stops_before_touching_proof_main_when_sso_expired() {
  local dir status
  dir=$(make_fixture)
  echo 2 >"$dir/stack-exit-sso"
  run_proof "$dir" >/dev/null
  status=$?
  [ "$status" -eq 2 ] || fail "sso expired: exit $status, want 2"
  [ "$(head -1 "$dir/calls.log")" = "stack sso" ] || fail "sso expired: first call was not the sso check: $(cat "$dir/calls.log")"
  grep -q '^main-worktree' "$dir/calls.log" && fail "sso expired: proof-main was refreshed anyway"
}

test_storybook_runs_skip_the_sso_check() {
  local dir
  dir=$(make_fixture)
  echo 2 >"$dir/stack-exit-sso"
  run_proof "$dir" --storybook >/dev/null
  grep -q '^stack sso' "$dir/calls.log" && fail "storybook: checked sso"
  grep -q '^compose' "$dir/calls.log" || fail "storybook: did not compose: $(cat "$dir/calls.log")"
}

test_skips_compose_and_exits_1_when_a_side_fails() {
  local dir out status
  dir=$(make_fixture)
  echo 1 >"$dir/run-exit-pr"
  out=$(run_proof "$dir")
  status=$?
  [ "$status" -eq 1 ] || fail "run failure: exit $status, want 1"
  [[ "$out" == *"auto sign-in did not happen"* ]] || fail "run failure: output lacks the run error: $out"
  grep -q '^compose' "$dir/calls.log" && fail "run failure: composed anyway"
}

test_down_flag_stops_both_stacks_even_after_a_failure() {
  local dir
  dir=$(make_fixture)
  echo 1 >"$dir/run-exit-main"
  run_proof "$dir" --down >/dev/null
  grep -q "^stack down $dir/main-repo\$" "$dir/calls.log" || fail "--down: main stack not stopped"
  grep -q "^stack down $dir/pr-worktree\$" "$dir/calls.log" || fail "--down: pr stack not stopped"
}

test_reseeds_the_stack_with_the_older_seed_day() {
  local dir
  dir=$(make_fixture)
  echo '{ "preparedAt": "x", "seedDay": "2026-10-27" }' >"$dir/state/main-repo.json"
  echo '{ "preparedAt": "x", "seedDay": "2026-10-28" }' >"$dir/state/pr-worktree.json"
  run_proof "$dir" >/dev/null
  grep -q "^stack up $dir/main-repo --reseed\$" "$dir/calls.log" || fail "reseed: older main not reseeded: $(grep '^stack' "$dir/calls.log")"
  grep -q "^stack up $dir/pr-worktree\$" "$dir/calls.log" || fail "reseed: pr stack should come up without --reseed"
}

take_lease() {
  UI_BEFORE_AFTER_STATE_DIR="$1/state" bash "$LEASE" take "$2"
}

test_does_not_reseed_main_while_another_run_holds_a_lease() {
  local dir out
  dir=$(make_fixture)
  echo '{ "preparedAt": "x", "seedDay": "2026-10-27" }' >"$dir/state/main-repo.json"
  echo '{ "preparedAt": "x", "seedDay": "2026-10-28" }' >"$dir/state/pr-worktree.json"
  take_lease "$dir" other
  out=$(run_proof "$dir")
  grep -q "^stack up $dir/main-repo\$" "$dir/calls.log" || fail "shared reseed: main reseeded: $(grep '^stack' "$dir/calls.log")"
  [[ "$out" == *"not reseeding proof-main"* ]] || fail "shared reseed: no line: $out"
}

test_down_leaves_main_running_while_another_run_holds_a_lease() {
  local dir
  dir=$(make_fixture)
  take_lease "$dir" 20057
  take_lease "$dir" other
  run_proof "$dir" --down >/dev/null
  grep -q "^stack down $dir/main-repo\$" "$dir/calls.log" && fail "shared down: main stopped under another run"
  grep -q "^stack down $dir/pr-worktree\$" "$dir/calls.log" || fail "shared down: pr stack not stopped"
  [ -e "$dir/state/proof-main.leases/20057" ] && fail "shared down: own lease remains"
  [ -e "$dir/state/proof-main.leases/other" ] || fail "shared down: other lease removed"
}

test_storybook_axe_pixel_and_record_options_reach_every_step() {
  local dir out
  dir=$(make_fixture)
  echo '{ "preparedAt": "x", "seedDay": "2026-10-27" }' >"$dir/state/main-repo.json"
  echo '{ "preparedAt": "x", "seedDay": "2026-10-28" }' >"$dir/state/pr-worktree.json"
  out=$(run_proof "$dir" --pixel --storybook --axe --record)
  grep -q "^stack up $dir/main-repo --storybook\$" "$dir/calls.log" || fail "storybook: main stack args: $(grep '^stack' "$dir/calls.log")"
  grep -q "^stack up $dir/pr-worktree --storybook\$" "$dir/calls.log" || fail "storybook: pr stack args"
  [ "$(grep -c -- '--side .* --storybook --axe --record$' "$dir/calls.log")" -eq 2 ] || fail "storybook: run args: $(grep '^run' "$dir/calls.log")"
  grep -q -- "--pixel" "$dir/calls.log" || fail "pixel: compose args: $(grep '^compose' "$dir/calls.log")"
  [[ "$out" == *"main/main.webm"* && "$out" == *"pr/pr.webm"* ]] || fail "record: output lacks the recordings: $out"
}

test_app_runs_get_their_own_slot_database_for_impersonation() {
  local dir
  dir=$(make_fixture)
  run_proof "$dir" >/dev/null
  grep -q -- "--side main --out .* --db postgres://localhost:5432/main-repo" "$dir/calls.log" || fail "db: main run args: $(grep '^run' "$dir/calls.log")"
  grep -q -- "--side pr --out .* --db postgres://localhost:5432/pr-worktree" "$dir/calls.log" || fail "db: pr run args"
}

test_refuses_a_pr_worktree_with_uncommitted_changes() {
  local dir out status
  dir=$(make_fixture)
  rm "$dir/pr-worktree/scripts/worktree.sh"
  out=$(run_proof "$dir")
  status=$?
  [ "$status" -eq 1 ] || fail "dirty pr worktree: exit $status, want 1"
  [[ "$out" == *"uncommitted changes"* ]] || fail "dirty pr worktree: output was: $out"
  [ ! -f "$dir/calls.log" ] || fail "dirty pr worktree: started something: $(cat "$dir/calls.log")"
}

test_without_options_runs_the_app_with_no_extra_checks() {
  local dir
  dir=$(make_fixture)
  run_proof "$dir" >/dev/null
  grep -q -- "--storybook\|--axe\|--pixel\|--record" "$dir/calls.log" && fail "no options: an option leaked: $(cat "$dir/calls.log")"
}

test_rejects_an_unknown_option() {
  local dir status
  dir=$(make_fixture)
  run_proof "$dir" --storybok >/dev/null
  status=$?
  [ "$status" -eq 64 ] || fail "unknown option: exit $status, want 64"
}

test_brings_main_up_then_the_pr_runs_both_sides_and_composes_with_both_shas
test_storybook_axe_pixel_and_record_options_reach_every_step
test_without_options_runs_the_app_with_no_extra_checks
test_rejects_an_unknown_option
test_app_runs_get_their_own_slot_database_for_impersonation
test_refuses_a_pr_worktree_with_uncommitted_changes
test_does_not_reseed_main_while_another_run_holds_a_lease
test_down_leaves_main_running_while_another_run_holds_a_lease
test_stops_with_the_stack_exit_code_when_a_stack_will_not_come_up
test_stops_before_touching_proof_main_when_sso_expired
test_storybook_runs_skip_the_sso_check
test_skips_compose_and_exits_1_when_a_side_fails
test_down_flag_stops_both_stacks_even_after_a_failure
test_reseeds_the_stack_with_the_older_seed_day

if [ "$FAILURES" -eq 0 ]; then
  echo "14 passed"
else
  echo "$FAILURES failed"
  exit 1
fi
