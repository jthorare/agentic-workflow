#!/usr/bin/env bash
# skills/ui-evidence/scripts/doctor.sh — read-only health check of the local
# stack. Exits 0 only when the app is healthy and reachable at localhost:3000
# and owned by this convention's app (never "start a second stack" against a
# foreign process on :3000 — RF-1).
#
# Everything deployment-specific comes from config, never a hardcoded value
# (see SKILL.md's "Configuration" section). Precedence, highest first: the
# environment; the local, uncommitted env file (UI_EVIDENCE_CONFIG, else
# .ui-evidence.local.env next to SKILL.md); a committed deployment.env next to
# SKILL.md. The runner (src/settings-files.ts) reads the same files the same way.
#   UI_EVIDENCE_APP_TITLE     required: the <title> :3000/login must show
#   UI_EVIDENCE_DB_CONTAINER  optional: the Postgres container to pg_isready
#   UI_EVIDENCE_DB_USER       optional: its user (default postgres)
#   UI_EVIDENCE_API_PORT      optional: a localhost port that must accept connections
#
# Worktree mode, when the deployment configures slots (all four required
# together, UI_EVIDENCE_SLOT_DATABASE optional):
#   UI_EVIDENCE_SLOT_FILE       the env file in a worktree's root that holds its slot
#   UI_EVIDENCE_SLOT_KEY        the NAME in that file whose value is the slot N
#   UI_EVIDENCE_SLOT_COUNT      the highest slot
#   UI_EVIDENCE_SLOT_PORT_STEP  slot N serves web on 3000+STEP*N, API on UI_EVIDENCE_API_PORT+STEP*N
#   UI_EVIDENCE_SLOT_DATABASE   the slot's database name, `{slot}` replaced by N
# Run from inside a git worktree whose slot file names its slot, or set
# UI_EVIDENCE_WORKTREE=<worktree path>. The checks then target that slot's
# stack, and both ports must be served from that worktree (a slot owned by
# another checkout is foreign). On success it prints the --host to pass to the
# runner and the database to check.
set -uo pipefail
FAILS=()
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$DIR/.."
LOCAL_CONFIG="${UI_EVIDENCE_CONFIG:-$SKILL_DIR/.ui-evidence.local.env}"
DEPLOYMENT_CONFIG="$SKILL_DIR/deployment.env"

# NAME=value lines (blank lines, # comments and an optional `export ` are
# allowed; one layer of matching quotes is stripped). Fills only variables
# that are unset or blank, so the environment and earlier files win. The file
# is parsed, never sourced.
fill_unset_from() {
  local file="$1" line name value current
  [ -f "$file" ] || return 0
  while IFS= read -r line || [ -n "$line" ]; do
    line="${line#"${line%%[![:space:]]*}"}"
    case "$line" in ''|'#'*) continue ;; esac
    line="${line#export }"
    [[ "$line" =~ ^([A-Za-z_][A-Za-z0-9_]*)=(.*)$ ]] || continue
    name="${BASH_REMATCH[1]}"
    value="${BASH_REMATCH[2]}"
    value="${value%"${value##*[![:space:]]}"}"
    if [ "${#value}" -ge 2 ] && { [[ "$value" == \"*\" ]] || [[ "$value" == \'*\' ]]; }; then
      value="${value:1:${#value}-2}"
    fi
    current="${!name-}"
    if [ -z "${current//[[:space:]]/}" ]; then
      export "$name=$value"
    fi
  done < "$file"
}
fill_unset_from "$LOCAL_CONFIG"
fill_unset_from "$DEPLOYMENT_CONFIG"

if [ -z "${UI_EVIDENCE_APP_TITLE:-}" ]; then
  printf 'ui-evidence doctor: UI_EVIDENCE_APP_TITLE is not set — set it in the environment, in %s, or in %s (see SKILL.md)\n' "$LOCAL_CONFIG" "$DEPLOYMENT_CONFIG"
  exit 1
fi

SLOT=0
STEP=0
if [ -n "${UI_EVIDENCE_SLOT_FILE:-}" ]; then
  SLOT_KEY="${UI_EVIDENCE_SLOT_KEY:-}"
  SLOT_COUNT="${UI_EVIDENCE_SLOT_COUNT:-}"
  STEP="${UI_EVIDENCE_SLOT_PORT_STEP:-}"
  if ! [[ "$SLOT_KEY" =~ ^[A-Za-z_][A-Za-z0-9_]*$ && "$SLOT_COUNT" =~ ^[1-9][0-9]*$ && "$STEP" =~ ^[1-9][0-9]*$ ]]; then
    printf 'ui-evidence doctor: UI_EVIDENCE_SLOT_FILE is set, so UI_EVIDENCE_SLOT_KEY must be a variable name and UI_EVIDENCE_SLOT_COUNT and UI_EVIDENCE_SLOT_PORT_STEP positive integers (see SKILL.md "Worktree stacks")\n'
    exit 1
  fi
  WORKTREE="${UI_EVIDENCE_WORKTREE:-$(git rev-parse --show-toplevel 2>/dev/null || true)}"
  SLOT_PATH="$WORKTREE/$UI_EVIDENCE_SLOT_FILE"
  if [ -n "$WORKTREE" ] && [ -f "$SLOT_PATH" ]; then
    SLOT="$(sed -n "s/^${SLOT_KEY}=\([0-9][0-9]*\)\$/\1/p" "$SLOT_PATH" | head -1)"
    if [ -z "$SLOT" ] || [ "$SLOT" -lt 1 ] || [ "$SLOT" -gt "$SLOT_COUNT" ]; then
      printf 'ui-evidence doctor: %s has no %s between 1 and %s\n' "$SLOT_PATH" "$SLOT_KEY" "$SLOT_COUNT"
      exit 1
    fi
  elif [ -n "${UI_EVIDENCE_WORKTREE:-}" ]; then
    printf 'ui-evidence doctor: UI_EVIDENCE_WORKTREE=%s has no %s — claim a stack slot there first\n' "$UI_EVIDENCE_WORKTREE" "$UI_EVIDENCE_SLOT_FILE"
    exit 1
  fi
elif [ -n "${UI_EVIDENCE_WORKTREE:-}" ]; then
  printf 'ui-evidence doctor: UI_EVIDENCE_WORKTREE is set but no worktree slots are configured (set UI_EVIDENCE_SLOT_FILE; see SKILL.md "Worktree stacks")\n'
  exit 1
fi
WEB_PORT=$((3000 + SLOT * STEP))
API_PORT=""
if [ -n "${UI_EVIDENCE_API_PORT:-}" ]; then API_PORT=$((UI_EVIDENCE_API_PORT + SLOT * STEP)); fi
DATABASE=""
if [ "$SLOT" -ne 0 ] && [ -n "${UI_EVIDENCE_SLOT_DATABASE:-}" ]; then
  DATABASE="${UI_EVIDENCE_SLOT_DATABASE//\{slot\}/$SLOT}"
  # It goes into a SQL literal below.
  if ! [[ "$DATABASE" =~ ^[A-Za-z0-9_]+$ ]]; then
    printf 'ui-evidence doctor: UI_EVIDENCE_SLOT_DATABASE must name a database of letters, digits and _ (with {slot})\n'
    exit 1
  fi
fi

LOGIN_HTML="$(curl -s --max-time 5 "http://localhost:${WEB_PORT}/login" 2>/dev/null || true)"
if ! grep -q "<title>${UI_EVIDENCE_APP_TITLE}</title>" <<<"$LOGIN_HTML"; then
  FAILS+=(":${WEB_PORT} does not look like this app (foreign process, or stack not up) — refusing to double-drive it")
fi

DB_USER="${UI_EVIDENCE_DB_USER:-postgres}"
if [ -n "${UI_EVIDENCE_DB_CONTAINER:-}" ]; then
  if ! docker exec "$UI_EVIDENCE_DB_CONTAINER" pg_isready -U "$DB_USER" > /dev/null 2>&1; then
    FAILS+=("postgres in container $UI_EVIDENCE_DB_CONTAINER is not ready")
  elif [ -n "$DATABASE" ] && [ "$(docker exec "$UI_EVIDENCE_DB_CONTAINER" psql -U "$DB_USER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '${DATABASE}'" 2>/dev/null)" != "1" ]; then
    FAILS+=("database ${DATABASE} does not exist in $UI_EVIDENCE_DB_CONTAINER — prepare the slot's database in ${WORKTREE}")
  fi
fi

if [ -n "$API_PORT" ]; then
  if ! nc -z localhost "$API_PORT" 2>/dev/null; then
    FAILS+=("api-server (:$API_PORT) is not accepting connections")
  fi
fi

# A slot's ports must be served from that worktree, so evidence shows its code.
if [ "$SLOT" -ne 0 ]; then
  for port in "$WEB_PORT" $API_PORT; do
    pid="$(lsof -nP -iTCP:"$port" -sTCP:LISTEN -t 2>/dev/null | head -1)"
    [ -z "$pid" ] && continue
    cwd="$(lsof -a -p "$pid" -d cwd -Fn 2>/dev/null | sed -n 's/^n//p')"
    case "$cwd" in
      "$WORKTREE"|"$WORKTREE"/*) ;;
      *) FAILS+=(":${port} is served from ${cwd:-an unknown directory}, not ${WORKTREE} — foreign stack, refusing to drive it") ;;
    esac
  done
fi

if [ "${#FAILS[@]}" -gt 0 ]; then
  printf 'ui-evidence doctor: NOT healthy\n'
  for f in "${FAILS[@]}"; do printf ' - %s\n' "$f"; done
  exit 1
fi
if [ "$SLOT" -eq 0 ]; then
  echo "ui-evidence doctor: healthy"
else
  echo "ui-evidence doctor: healthy (worktree slot ${SLOT}: ${WORKTREE})"
  echo "  runner:    --host http://localhost:${WEB_PORT}"
  [ -n "${UI_EVIDENCE_DB_CONTAINER:-}" ] && [ -n "$DATABASE" ] && echo "  database:  ${DATABASE} (docker exec ${UI_EVIDENCE_DB_CONTAINER} psql -U ${DB_USER} -d ${DATABASE})"
fi
exit 0
