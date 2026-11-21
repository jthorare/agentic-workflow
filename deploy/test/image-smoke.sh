#!/usr/bin/env bash
# deploy/test/image-smoke.sh — the heavy proof of the Sindri image (spec 2026-10-23-sindri-deployable-context-design.md §7, phase K-A1):
# build the image, run it on a fresh volume with a read-only root, and check that serve comes up with Temporal, that doctor reports
# no failure, that a normal restart takes the lease at once, and that a killed container's restart does too (spec A2b).
#
#   deploy/test/image-smoke.sh [--image TAG] [--no-build] [--keep]
#   --image TAG  the tag to build and run (default sindri-smoke:local)
#   --no-build   run an image that already exists
#   --keep       leave the container and volume for inspection
# Needs Docker access (the user in the docker group). Exit 0 all checks pass, 1 a check failed, 2 usage or Docker error.
# Heavy: a NativeAOT image build; run it by hand or in the nightly lane, never in a PR gate's unit lanes.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
image="sindri-smoke:local"; build=1; keep=0
while [ $# -gt 0 ]; do
  case "$1" in
    --image) image="${2:?--image needs a tag}"; shift 2 ;;
    --no-build) build=0; shift ;;
    --keep) keep=1; shift ;;
    *) sed -n '2,13p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 2 ;;
  esac
done
docker info >/dev/null 2>&1 || { echo "image-smoke: docker is not usable here (is the user in the docker group?)" >&2; exit 2; }

suffix="$$-$(date +%s)"
name="sindri-smoke-$suffix"
volume="sindri-smoke-data-$suffix"
failures=0
ok()  { echo "  ok   $1"; }
bad() { echo "  FAIL $1"; failures=$((failures + 1)); }

cleanup() {
  if [ "$keep" = 1 ]; then echo "kept container $name and volume $volume"; return; fi
  docker rm --force "$name" >/dev/null 2>&1 || true
  docker volume rm "$volume" >/dev/null 2>&1 || true
}
trap cleanup EXIT

start() {
  docker run --detach --name "$name" --read-only --tmpfs /tmp --cap-drop ALL --security-opt no-new-privileges \
    --volume "$volume:/data" "$image" >/dev/null
}
# Prints the seconds until `sindri status` answers inside the container; fails after the limit or when the container stopped.
wait_up() {
  local limit="${1:-90}" t0
  t0="$(date +%s)"
  while :; do
    if docker exec "$name" /opt/sindri/sindri status >/dev/null 2>&1; then echo $(( $(date +%s) - t0 )); return 0; fi
    docker inspect --format '{{.State.Running}}' "$name" 2>/dev/null | grep -q true || return 1
    [ $(( $(date +%s) - t0 )) -lt "$limit" ] || return 1
    sleep 1
  done
}
epoch() { docker exec "$name" /opt/sindri/sindri status --json 2>/dev/null | jq -r '.epoch // empty' 2>/dev/null || true; }

echo "image-smoke: $image"
if [ "$build" = 1 ]; then
  docker build --tag "$image" "$ROOT" || { echo "image-smoke: the build failed" >&2; exit 1; }
fi

# 1. a fresh volume: serve comes up
docker volume create "$volume" >/dev/null
start
if secs="$(wait_up 120)"; then
  ok "serve is up on a fresh volume after ${secs}s"
else
  bad "serve did not come up"; docker logs "$name" 2>&1 | tail -30 || true; exit 1
fi

# 2. the user, PID 1, the filesystem, the seeded Temporal binary
[ "$(docker exec "$name" id -u)" = 1000 ] && ok "runs as uid 1000" || bad "does not run as uid 1000"
[ "$(docker exec "$name" cat /proc/1/comm)" = tini ] && ok "tini is PID 1" || bad "PID 1 is not tini"
if docker exec "$name" sh -c 'touch /opt/sindri/x' 2>/dev/null; then bad "the root filesystem is writable"; else ok "the root filesystem is read-only"; fi
if docker exec "$name" sh -c 'test -x /data/state/cache/bin/temporal-*/temporal'; then ok "start.sh seeded the temporal binary on the volume"; else bad "no temporal binary on the volume"; fi

# 3. doctor: exit 0 all ok, 1 a warning (no model, no Ollama sidecar in this run), 2 a failing check
set +e
doc="$(docker exec "$name" /opt/sindri/sindri doctor --json 2>&1)"; rc=$?
set -e
if [ "$rc" -le 1 ]; then ok "doctor has no failing check (exit $rc)"; else bad "doctor failed (exit $rc)"; printf '%s\n' "$doc" | tail -20; fi
if grep -q 'deploy:mode' <<<"$doc"; then ok "doctor echoes pod mode"; else bad "doctor does not echo pod mode"; fi

# 4. a normal restart (SIGTERM, a clean stop) takes the lease at once
e1="$(epoch)"
docker restart --time 60 "$name" >/dev/null
if secs="$(wait_up 60)"; then
  if [ "$secs" -le 30 ]; then ok "a restart is serving again after ${secs}s"; else bad "a restart took ${secs}s (want <= 30)"; fi
else
  bad "serve did not come back after a restart"
fi
e2="$(epoch)"
if [ -n "$e1" ] && [ -n "$e2" ] && [ "$e2" -gt "$e1" ]; then ok "the lease epoch advanced ($e1 -> $e2)"; else bad "the lease epoch did not advance (${e1:-?} -> ${e2:-?})"; fi

# 5. a killed container (no clean stop, the lease unexpired) restarts at once too: the old holder is provably dead
docker kill --signal KILL "$name" >/dev/null
docker start "$name" >/dev/null
if secs="$(wait_up 60)"; then
  if [ "$secs" -le 30 ]; then ok "after SIGKILL, serve is up after ${secs}s (no wait for the lease to lapse)"; else bad "after SIGKILL serve took ${secs}s: the lease wait shows (spec A2b: add a pod-mode host override)"; fi
else
  bad "serve did not come back after SIGKILL"
fi

echo "image-smoke: $failures failed"
[ "$failures" -eq 0 ]
