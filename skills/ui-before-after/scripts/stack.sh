#!/usr/bin/env bash
# Brings one web-app worktree's local slot stack up or down for ui-before-after.
#
#   stack.sh up <worktree> [--reseed]
#     Prints WEB_URL=<url> and STARTED_BY_UI_BEFORE_AFTER=1|0. --reseed rebuilds the slot's database,
#     which seeds it as of today.
#     Exit 1: slot 0 or prepare failed. 2: AWS SSO expired. 3: web or API port held by something
#     this checkout does not manage. 4: not ready within 300s, or the dev run died.
#   stack.sh up <worktree> --storybook
#     Starts the slot's Storybook instead: no database, no sign-in, no AWS SSO. Same output and
#     exit codes.
#   stack.sh down <worktree>
#     Stops the stack and Storybook, each only when the run `up` started is still the one running.
#   stack.sh free <worktree>
#     For a worktree about to be removed: stops its Storybook and any dev run, whoever started it,
#     releases its slot, and deletes its state here. Nothing to do without a slot.
#   stack.sh sso
#     Exit 2 when the AWS SSO session `up` needs has expired.
set -u

STATE_DIR="${UI_BEFORE_AFTER_STATE_DIR:-$HOME/.agentic-workflow/vitalizecare-web-app/ui-before-after/.stacks}"
SSO_CHECK="${UI_BEFORE_AFTER_SSO_CHECK:-aws sts get-caller-identity --profile vitalize-health}"
INSTALL="${UI_BEFORE_AFTER_INSTALL:-bun install --frozen-lockfile}"
READY_TIMEOUT_S=300
START_WAIT_S="${UI_BEFORE_AFTER_START_WAIT_S:-600}"

usage() {
  echo "usage: stack.sh up <worktree> [--reseed | --storybook] | down <worktree> | free <worktree> | sso" >&2
  exit 64
}

port_from_info() {
  bash scripts/worktree.sh info | sed -n "s|^ *$1: *http://localhost:\([0-9][0-9]*\).*|\1|p" | head -1
}

port_is_listening() {
  lsof -nP -iTCP:"$1" -sTCP:LISTEN >/dev/null 2>&1
}

# worktree.sh start records its supervisor pid here.
managed_run_pid() {
  local owner
  owner="$(git rev-parse --path-format=absolute --git-path vitalize-dev)/owner"
  [ -f "$owner" ] && sed -n '1p' "$owner"
}

managed_run_is_alive() {
  local pid
  pid=$(managed_run_pid)
  [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null
}

record_field() {
  sed -n "s/.*\"$2\": *\"\([^\"]*\)\".*/\1/p" "$1" 2>/dev/null
}

needs_prepare() {
  local record="$1" prepared_at
  [ -f "$record" ] || return 0
  prepared_at=$(record_field "$record" preparedAt)
  [ -n "$prepared_at" ] || return 0
  git diff --quiet "$prepared_at" HEAD -- bun.lock packages/drizzle/migrations || return 0
  return 1
}

# Two dev:local starts at once clash in the shared Postgres ("tuple concurrently updated"), so
# starts take turns. A lock whose holder died is taken over.
take_start_lock() {
  local lock="$STATE_DIR/.starting" waited=0
  while ! mkdir "$lock" 2>/dev/null; do
    if ! kill -0 "$(cat "$lock/pid" 2>/dev/null)" 2>/dev/null; then
      rm -rf "$lock"
      continue
    fi
    if [ "$waited" -ge "$START_WAIT_S" ]; then
      echo "another ui-before-after start is still running after ${START_WAIT_S}s; try again when it finishes" >&2
      exit 5
    fi
    sleep 1
    waited=$((waited + 1))
  done
  echo "$$" >"$lock/pid"
  trap 'rm -rf "$STATE_DIR/.starting"' EXIT
}

wait_until_ready() {
  local web_port="$1" api_port="$2" supervisor="$3" waited=0 web api
  while [ "$waited" -lt "$READY_TIMEOUT_S" ]; do
    kill -0 "$supervisor" 2>/dev/null || return 1
    web=$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 "http://localhost:$web_port/" || true)
    api=$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 "http://localhost:$api_port/api/auth/get-session" || true)
    if [ "$web" = "200" ] && [ "$api" != "000" ]; then return 0; fi
    sleep 1
    waited=$((waited + 1))
  done
  return 1
}

cmd_sso() {
  if ! eval "$SSO_CHECK" >/dev/null 2>&1; then
    echo "AWS SSO session expired. Run: aws sso login --profile vitalize-health" >&2
    exit 2
  fi
}

cmd_up() {
  local worktree="$1" reseed=0 name web_port api_port record marker supervisor port
  shift
  while [ $# -gt 0 ]; do
    case "$1" in
      --reseed) reseed=1; shift ;;
      --storybook) cmd_up_storybook "$worktree"; return ;;
      *) usage ;;
    esac
  done

  cmd_sso

  cd "$worktree" || exit 1
  name=$(basename "$worktree")
  mkdir -p "$STATE_DIR"
  record="$STATE_DIR/$name.json"
  marker="$STATE_DIR/$name.started"

  # A marker whose run is gone was left by a crash; it must not let `down` stop someone else's run.
  if [ -f "$marker" ] && ! kill -0 "$(cat "$marker")" 2>/dev/null; then rm -f "$marker"; fi

  take_start_lock
  [ -f .env.worktree ] || bash scripts/worktree.sh claim >/dev/null
  web_port=$(port_from_info Web)
  api_port=$(port_from_info API)
  if [ "$web_port" = "3000" ]; then
    echo "This is slot 0 (the main checkout on :3000); ui-before-after never runs there." >&2
    exit 1
  fi

  if managed_run_is_alive && port_is_listening "$web_port"; then
    # A refresh of this checkout can bring migrations its running database never got.
    if needs_prepare "$record" && [ -f "$marker" ] && [ "$(cat "$marker")" = "$(managed_run_pid)" ]; then
      echo "Migrations or the lockfile changed since prepare; restarting this stack." >&2
      bash scripts/worktree.sh stop
      rm -f "$marker"
    else
      echo "WEB_URL=http://localhost:$web_port"
      echo "STARTED_BY_UI_BEFORE_AFTER=0"
      return
    fi
  fi
  for port in "$web_port" "$api_port"; do
    if port_is_listening "$port"; then
      echo "Port $port is held by a process this checkout does not manage; not touching it." >&2
      exit 3
    fi
  done

  if [ "$reseed" -eq 1 ] || needs_prepare "$record"; then
    bash scripts/worktree.sh prepare >"$STATE_DIR/$name.prepare.log" 2>&1 || {
      echo "prepare failed; see $STATE_DIR/$name.prepare.log" >&2
      exit 1
    }
    printf '{ "preparedAt": "%s", "seedDay": "%s" }\n' "$(git rev-parse HEAD)" "$(date +%F)" >"$record"
  fi

  # `dev:local` starts the slot's Storybook on the same port, and stops everything if it is taken.
  down_storybook "$STATE_DIR/$name.storybook.started" >/dev/null
  DEV_LOCAL_AUTO_LOGIN=admin nohup bash scripts/worktree.sh start >"$STATE_DIR/$name.dev.log" 2>&1 &
  supervisor=$!
  echo "$supervisor" >"$marker"
  if ! wait_until_ready "$web_port" "$api_port" "$supervisor"; then
    echo "Not ready within ${READY_TIMEOUT_S}s or the dev run died; see $STATE_DIR/$name.dev.log" >&2
    exit 4
  fi
  echo "WEB_URL=http://localhost:$web_port"
  echo "STARTED_BY_UI_BEFORE_AFTER=1"
}

# The marker holds "<pid> <port>" of the `bun run dev` that `up --storybook` started.
storybook_run_is_alive() {
  local marker="$1"
  [ -f "$marker" ] && kill -0 "$(cut -d' ' -f1 "$marker")" 2>/dev/null
}

cmd_up_storybook() {
  local worktree="$1" name port marker waited=0
  cd "$worktree" || exit 1
  name=$(basename "$worktree")
  mkdir -p "$STATE_DIR"
  marker="$STATE_DIR/$name.storybook.started"
  [ -f .env.worktree ] || bash scripts/worktree.sh claim >/dev/null
  if [ "$(port_from_info Web)" = "3000" ]; then
    echo "This is slot 0 (the main checkout on :3000); ui-before-after never runs there." >&2
    exit 1
  fi
  port=$(port_from_info Storybook)

  # `dev:local` runs the slot's Storybook too, so a running app stack already has one.
  if { storybook_run_is_alive "$marker" || managed_run_is_alive; } && port_is_listening "$port"; then
    echo "WEB_URL=http://localhost:$port"
    echo "STARTED_BY_UI_BEFORE_AFTER=0"
    return
  fi
  rm -f "$marker"
  if port_is_listening "$port"; then
    echo "Port $port is held by a process this checkout does not manage; not touching it." >&2
    exit 3
  fi

  $INSTALL >"$STATE_DIR/$name.install.log" 2>&1 || {
    echo "install failed; see $STATE_DIR/$name.install.log" >&2
    exit 1
  }
  (cd apps/storybook && STORYBOOK_PORT="$port" exec nohup bun run dev) >"$STATE_DIR/$name.storybook.log" 2>&1 &
  echo "$! $port" >"$marker"
  until [ "$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 "http://localhost:$port/iframe.html" || true)" = "200" ]; do
    if [ "$waited" -ge "$READY_TIMEOUT_S" ] || ! storybook_run_is_alive "$marker"; then
      echo "Storybook not ready within ${READY_TIMEOUT_S}s or it died; see $STATE_DIR/$name.storybook.log" >&2
      exit 4
    fi
    sleep 1
    waited=$((waited + 1))
  done
  echo "WEB_URL=http://localhost:$port"
  echo "STARTED_BY_UI_BEFORE_AFTER=1"
}

down_app() {
  local marker="$1"
  if ! managed_run_is_alive; then
    rm -f "$marker"
    echo "not running; nothing to stop"
    return
  fi
  if [ ! -f "$marker" ] || [ "$(cat "$marker")" != "$(managed_run_pid)" ]; then
    rm -f "$marker"
    echo "not started by ui-before-after; leaving it running"
    return
  fi
  bash scripts/worktree.sh stop
  rm -f "$marker"
}

down_storybook() {
  local marker="$1" pid port
  [ -f "$marker" ] || return 0
  if storybook_run_is_alive "$marker"; then
    read -r pid port <"$marker"
    lsof -t -nP -iTCP:"$port" -sTCP:LISTEN 2>/dev/null | xargs kill 2>/dev/null
    kill "$pid" 2>/dev/null
    echo "Storybook stopped"
  fi
  rm -f "$marker"
}

cmd_down() {
  local worktree="$1" name
  name=$(basename "$worktree")
  cd "$worktree" || exit 1
  down_app "$STATE_DIR/$name.started"
  down_storybook "$STATE_DIR/$name.storybook.started"
}

cmd_free() {
  local worktree="$1" name
  name=$(basename "$worktree")
  cd "$worktree" || exit 1
  down_storybook "$STATE_DIR/$name.storybook.started"
  if [ -f .env.worktree ]; then
    bash scripts/worktree.sh stop
    bash scripts/worktree.sh release
  fi
  rm -f "$STATE_DIR/$name".*
}

[ $# -ge 2 ] || [ "${1:-}" = sso ] || usage
case "$1" in
  up) shift; cmd_up "$@" ;;
  down) cmd_down "$2" ;;
  free) cmd_free "$2" ;;
  sso) cmd_sso ;;
  *) usage ;;
esac
