#!/usr/bin/env bash
# Tests for stack.sh against a stub worktree, so no real dev stack starts.
set -u

STACK="$(cd "$(dirname "$0")" && pwd)/stack.sh"
FAILURES=0
PORT=39917

# stack.sh starts Storybook with `bun run dev`. The CI runner has no bun, so a stub runs the stub worktree's
# package.json script with sh; any other bun command fails loudly.
STUB_BIN=$(mktemp -d)
cat >"$STUB_BIN/bun" <<'EOF'
#!/usr/bin/env bash
if [ "${1:-}" = run ] && [ -n "${2:-}" ]; then
  exec sh -c "$(node -e 'process.stdout.write(require(process.cwd() + "/package.json").scripts[process.argv[1]])' "$2")"
fi
echo "stub bun: unexpected: $*" >&2
exit 1
EOF
chmod +x "$STUB_BIN/bun"
PATH="$STUB_BIN:$PATH"

fail() {
  echo "FAIL: $1"
  FAILURES=$((FAILURES + 1))
}

make_stub_worktree() {
  local dir web_port="${1:-$PORT}"
  dir=$(mktemp -d)
  git -C "$dir" init -q
  mkdir -p "$dir/scripts"
  cat >"$dir/scripts/worktree.sh" <<EOF
#!/usr/bin/env bash
case "\$1" in
  info) echo "  Web:       http://localhost:$web_port"; echo "  API:       http://localhost:$((web_port + 2))"; echo "  Storybook: http://localhost:$((web_port + 6))" ;;
  *) echo "stub \$1" ;;
esac
EOF
  # Stands in for Storybook: serves iframe.html on STORYBOOK_PORT.
  mkdir -p "$dir/apps/storybook"
  touch "$dir/apps/storybook/iframe.html"
  cat >"$dir/apps/storybook/package.json" <<'EOF'
{ "scripts": { "dev": "python3 -m http.server $STORYBOOK_PORT" } }
EOF
  echo "WORKTREE_SLOT=9" >"$dir/.env.worktree"
  echo "$dir"
}

storybook() {
  local dir="$1"
  shift
  UI_BEFORE_AFTER_SSO_CHECK=false UI_BEFORE_AFTER_INSTALL=true UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" "$@" 2>&1
}

test_storybook_up_needs_no_sso_and_reuses_its_own_run_until_down() {
  local dir out status
  dir=$(make_stub_worktree)
  out=$(storybook "$dir" up "$dir" --storybook)
  status=$?
  [ "$status" -eq 0 ] || fail "storybook up: exit $status, want 0: $out"
  [[ "$out" == *"WEB_URL=http://localhost:$((PORT + 6))"* ]] || fail "storybook up: output was: $out"
  [[ "$out" == *"STARTED_BY_UI_BEFORE_AFTER=1"* ]] || fail "storybook up: not marked as started: $out"
  out=$(storybook "$dir" up "$dir" --storybook)
  [[ "$out" == *"STARTED_BY_UI_BEFORE_AFTER=0"* ]] || fail "storybook up again: did not reuse the run: $out"
  out=$(storybook "$dir" down "$dir")
  sleep 0.3
  ! lsof -nP -iTCP:"$((PORT + 6))" -sTCP:LISTEN >/dev/null 2>&1 || fail "storybook down: port still held: $out"
}

test_storybook_up_refuses_a_port_held_by_something_unmanaged() {
  local dir out status listener
  dir=$(make_stub_worktree)
  nc -l "$((PORT + 6))" >/dev/null 2>&1 &
  listener=$!
  sleep 0.3
  out=$(storybook "$dir" up "$dir" --storybook)
  status=$?
  kill "$listener" 2>/dev/null
  wait "$listener" 2>/dev/null
  [ "$status" -eq 3 ] || fail "storybook unmanaged port: exit $status, want 3: $out"
}

test_up_fails_fast_when_sso_expired() {
  local dir err status
  dir=$(make_stub_worktree)
  err=$(UI_BEFORE_AFTER_SSO_CHECK=false UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" up "$dir" 2>&1 >/dev/null)
  status=$?
  [ "$status" -eq 2 ] || fail "sso expired: exit $status, want 2"
  [[ "$err" == *"aws sso login --profile vitalize-health"* ]] || fail "sso expired: stderr lacks the login command: $err"
}

test_sso_reports_an_expired_session_with_the_login_command() {
  local err status
  err=$(UI_BEFORE_AFTER_SSO_CHECK=false bash "$STACK" sso 2>&1 >/dev/null)
  status=$?
  [ "$status" -eq 2 ] || fail "sso command expired: exit $status, want 2"
  [[ "$err" == *"aws sso login --profile vitalize-health"* ]] || fail "sso command expired: stderr lacks the login command: $err"
  UI_BEFORE_AFTER_SSO_CHECK=true bash "$STACK" sso || fail "sso command valid: exit $?, want 0"
}

test_up_refuses_a_port_held_by_something_unmanaged() {
  local dir err status listener
  dir=$(make_stub_worktree)
  nc -l "$PORT" >/dev/null 2>&1 &
  listener=$!
  sleep 0.3
  err=$(UI_BEFORE_AFTER_SSO_CHECK=true UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" up "$dir" 2>&1 >/dev/null)
  status=$?
  kill "$listener" 2>/dev/null
  wait "$listener" 2>/dev/null
  [ "$status" -eq 3 ] || fail "unmanaged port: exit $status, want 3"
  [[ "$err" == *"$PORT"* ]] || fail "unmanaged port: stderr does not name port $PORT: $err"
}

test_down_when_nothing_runs_says_so() {
  local dir out status
  dir=$(make_stub_worktree)
  mkdir -p "$dir/.state"
  # The run ui-before-after started has already exited: its marker is left, no supervisor is alive.
  echo 999999 >"$dir/.state/$(basename "$dir").started"
  out=$(UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" down "$dir" 2>&1)
  status=$?
  [ "$status" -eq 0 ] || fail "down when nothing runs: exit $status, want 0"
  [[ "$out" == *"not running"* ]] || fail "down when nothing runs: output was: $out"
  [ ! -f "$dir/.state/$(basename "$dir").started" ] || fail "down when nothing runs: marker left behind"
}

test_up_refuses_an_api_port_held_by_something_unmanaged() {
  local dir err status listener
  dir=$(make_stub_worktree)
  nc -l "$((PORT + 2))" >/dev/null 2>&1 &
  listener=$!
  sleep 0.3
  err=$(UI_BEFORE_AFTER_SSO_CHECK=true UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" up "$dir" 2>&1 >/dev/null)
  status=$?
  kill "$listener" 2>/dev/null
  wait "$listener" 2>/dev/null
  [ "$status" -eq 3 ] || fail "unmanaged api port: exit $status, want 3"
  [[ "$err" == *"$((PORT + 2))"* ]] || fail "unmanaged api port: stderr does not name port $((PORT + 2)): $err"
}

test_up_refuses_slot_zero() {
  local dir err status
  dir=$(make_stub_worktree 3000)
  err=$(UI_BEFORE_AFTER_SSO_CHECK=true UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" up "$dir" 2>&1 >/dev/null)
  status=$?
  [ "$status" -eq 1 ] || fail "slot 0: exit $status, want 1"
  [[ "$err" == *"slot 0"* ]] || fail "slot 0: stderr was: $err"
}

test_down_with_a_stale_marker_leaves_a_run_it_did_not_start() {
  local dir out run_dir
  dir=$(make_stub_worktree)
  mkdir -p "$dir/.state"
  # A crashed earlier run left this marker; the live run now belongs to someone else.
  echo 999999 >"$dir/.state/$(basename "$dir").started"
  run_dir="$dir/$(git -C "$dir" rev-parse --git-path vitalize-dev)"
  mkdir -p "$run_dir"
  printf '%s\nstarted\n' "$$" >"$run_dir/owner"
  out=$(UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" down "$dir" 2>&1)
  [[ "$out" != *"stub stop"* ]] || fail "stale marker: down stopped a run it did not start"
  [[ "$out" == *"not started by ui-before-after"* ]] || fail "stale marker: output was: $out"
}

test_up_waits_for_another_start_and_gives_up_when_it_never_finishes() {
  local dir err status
  dir=$(make_stub_worktree)
  mkdir -p "$dir/.state/.starting"
  echo "$$" >"$dir/.state/.starting/pid"
  err=$(UI_BEFORE_AFTER_SSO_CHECK=true UI_BEFORE_AFTER_START_WAIT_S=1 UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" up "$dir" 2>&1 >/dev/null)
  status=$?
  [ "$status" -eq 5 ] || fail "start lock held: exit $status, want 5"
  [[ "$err" == *"another ui-before-after start"* ]] || fail "start lock held: stderr was: $err"
}

test_up_takes_over_a_start_lock_left_by_a_dead_run() {
  local dir err status listener
  dir=$(make_stub_worktree)
  mkdir -p "$dir/.state/.starting"
  echo 999999 >"$dir/.state/.starting/pid"
  # A held web port makes `up` stop with exit 3 right after the lock, so this stays fast.
  nc -l "$PORT" >/dev/null 2>&1 &
  listener=$!
  sleep 0.3
  err=$(UI_BEFORE_AFTER_SSO_CHECK=true UI_BEFORE_AFTER_START_WAIT_S=1 UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" up "$dir" 2>&1 >/dev/null)
  status=$?
  kill "$listener" 2>/dev/null
  wait "$listener" 2>/dev/null
  [ "$status" -eq 3 ] || fail "dead start lock: exit $status, want 3 (got past the lock): $err"
  [ ! -d "$dir/.state/.starting" ] || fail "dead start lock: lock not released"
}

test_app_up_stops_its_own_storybook_first_because_dev_local_starts_one() {
  local dir out
  dir=$(make_stub_worktree)
  storybook "$dir" up "$dir" --storybook >/dev/null
  # The stub `start` exits at once, so `up` ends with exit 4; the Storybook must be gone by then.
  out=$(UI_BEFORE_AFTER_SSO_CHECK=true UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" up "$dir" 2>&1)
  sleep 0.3
  if lsof -nP -iTCP:"$((PORT + 6))" -sTCP:LISTEN >/dev/null 2>&1; then
    fail "app up: ui-before-after's Storybook still holds the slot's Storybook port: $out"
    storybook "$dir" down "$dir" >/dev/null
  fi
}

test_storybook_up_reuses_the_app_stacks_storybook() {
  local dir out run_dir listener
  dir=$(make_stub_worktree)
  run_dir="$dir/$(git -C "$dir" rev-parse --git-path vitalize-dev)"
  mkdir -p "$run_dir"
  printf '%s\nstarted\n' "$$" >"$run_dir/owner"
  nc -l "$((PORT + 6))" >/dev/null 2>&1 &
  listener=$!
  sleep 0.3
  out=$(storybook "$dir" up "$dir" --storybook)
  kill "$listener" 2>/dev/null
  wait "$listener" 2>/dev/null
  [[ "$out" == *"WEB_URL=http://localhost:$((PORT + 6))"* && "$out" == *"STARTED_BY_UI_BEFORE_AFTER=0"* ]] ||
    fail "app storybook: did not reuse it: $out"
}

test_free_stops_everything_releases_the_slot_and_forgets_the_worktree() {
  local dir out name
  dir=$(make_stub_worktree)
  name=$(basename "$dir")
  storybook "$dir" up "$dir" --storybook >/dev/null
  echo '{ "preparedAt": "x", "seedDay": "2026-10-28" }' >"$dir/.state/$name.json"
  out=$(UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" free "$dir" 2>&1)
  sleep 0.3
  ! lsof -nP -iTCP:"$((PORT + 6))" -sTCP:LISTEN >/dev/null 2>&1 || fail "free: Storybook still up: $out"
  [[ "$out" == *"stub stop"*"stub release"* ]] || fail "free: dev run not stopped then slot released: $out"
  [ -z "$(ls "$dir/.state" | grep "^$name\.")" ] || fail "free: state left: $(ls "$dir/.state")"
  lsof -t -nP -iTCP:"$((PORT + 6))" -sTCP:LISTEN 2>/dev/null | xargs kill 2>/dev/null
}

test_free_without_a_slot_does_nothing() {
  local dir out
  dir=$(make_stub_worktree)
  rm "$dir/.env.worktree"
  out=$(UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" free "$dir" 2>&1)
  [[ "$out" != *"stub"* ]] || fail "free without slot: called worktree.sh: $out"
}

# A live run this checkout started: a sleeping supervisor plus a listener on the web port. The stub
# `stop` ends both, as worktree.sh stop does. Commits a migration after `preparedAt` when asked.
start_own_run() {
  local dir="$1" with_new_migration="$2" name run_dir supervisor listener
  name=$(basename "$dir")
  git -C "$dir" -c user.email=t@t -c user.name=t commit -q --allow-empty -m prepared
  mkdir -p "$dir/.state"
  echo "{ \"preparedAt\": \"$(git -C "$dir" rev-parse HEAD)\", \"seedDay\": \"$(date +%F)\" }" >"$dir/.state/$name.json"
  if [ "$with_new_migration" -eq 1 ]; then
    mkdir -p "$dir/packages/drizzle/migrations"
    touch "$dir/packages/drizzle/migrations/0001_add_floor.sql"
    git -C "$dir" add packages
    git -C "$dir" -c user.email=t@t -c user.name=t commit -q -m migration
  fi
  sleep 60 >/dev/null 2>&1 &
  supervisor=$!
  nc -l "$PORT" >/dev/null 2>&1 &
  listener=$!
  sleep 0.3
  run_dir="$dir/$(git -C "$dir" rev-parse --git-path vitalize-dev)"
  mkdir -p "$run_dir"
  printf '%s\nstarted\n' "$supervisor" >"$run_dir/owner"
  echo "$supervisor" >"$dir/.state/$name.started"
  cat >>"$dir/scripts/worktree.sh" <<EOF
if [ "\$1" = stop ]; then kill $supervisor $listener 2>/dev/null; fi
EOF
  echo "$supervisor $listener"
}

test_up_restarts_its_own_run_when_migrations_changed_since_prepare() {
  local dir out name pids
  dir=$(make_stub_worktree)
  name=$(basename "$dir")
  pids=$(start_own_run "$dir" 1)
  # The stub `start` exits at once, so `up` ends with exit 4 after preparing.
  out=$(UI_BEFORE_AFTER_SSO_CHECK=true UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" up "$dir" 2>&1)
  kill $pids 2>/dev/null
  [[ "$out" == *"stub stop"* ]] || fail "new migration: running stack not stopped: $out"
  grep -q "stub prepare" "$dir/.state/$name.prepare.log" 2>/dev/null || fail "new migration: prepare did not run: $out"
  [[ "$(cat "$dir/.state/$name.json")" == *"$(git -C "$dir" rev-parse HEAD)"* ]] || fail "new migration: record not moved to HEAD"
}

test_up_reuses_its_own_run_when_nothing_needs_preparing() {
  local dir out pids
  dir=$(make_stub_worktree)
  pids=$(start_own_run "$dir" 0)
  out=$(UI_BEFORE_AFTER_SSO_CHECK=true UI_BEFORE_AFTER_STATE_DIR="$dir/.state" bash "$STACK" up "$dir" 2>&1)
  kill $pids 2>/dev/null
  [[ "$out" == *"STARTED_BY_UI_BEFORE_AFTER=0"* ]] || fail "prepared run: not reused: $out"
  [[ "$out" != *"stub stop"* ]] || fail "prepared run: stopped a stack that needed nothing: $out"
}

test_up_restarts_its_own_run_when_migrations_changed_since_prepare
test_up_reuses_its_own_run_when_nothing_needs_preparing
test_up_fails_fast_when_sso_expired
test_sso_reports_an_expired_session_with_the_login_command
test_free_stops_everything_releases_the_slot_and_forgets_the_worktree
test_free_without_a_slot_does_nothing
test_app_up_stops_its_own_storybook_first_because_dev_local_starts_one
test_storybook_up_reuses_the_app_stacks_storybook
test_up_refuses_a_port_held_by_something_unmanaged
test_up_waits_for_another_start_and_gives_up_when_it_never_finishes
test_up_takes_over_a_start_lock_left_by_a_dead_run
test_down_when_nothing_runs_says_so
test_up_refuses_an_api_port_held_by_something_unmanaged
test_up_refuses_slot_zero
test_down_with_a_stale_marker_leaves_a_run_it_did_not_start
test_storybook_up_needs_no_sso_and_reuses_its_own_run_until_down
test_storybook_up_refuses_a_port_held_by_something_unmanaged

if [ "$FAILURES" -eq 0 ]; then
  echo "17 passed"
else
  echo "$FAILURES failed"
  exit 1
fi
