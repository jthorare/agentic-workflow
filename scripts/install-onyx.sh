#!/usr/bin/env bash
# Stand the pinned Onyx up with Sindri (setup.sh --with-sindri runs this unless --no-onyx): check that Docker is
# there and answering, then run the C# Sindri's `onyx up` through `sindri`. `onyx up` is the human's verb: it does its
# own preflight (docker, compose, memory, disk, port), fetches and verifies the pinned deployment files, starts the stack,
# opens Onyx's sign-up page, asks once for your Onyx login to make Sindri's key, and writes and approves the profile
# section. This script is only the bridge; it holds no state, so a rerun on a set-up box is `onyx up`'s no-op.
#
#   install-onyx.sh                      run it (needs a terminal: Onyx's sign-up and the profile approval are yours)
#   AW_DRY_RUN=1 install-onyx.sh         print what would happen, run nothing
#   AW_ONYX_SINDRI_APP=PATH              the sindri to run (default: `sindri` on PATH, linked by scripts/install-cs.sh)
#   AW_ONYX_FORCE_TERMINAL=1             treat this run as being at a terminal (tests)
#
# Exit: 0 Onyx is up (or was); 3 skipped with a warning and the command to run later (no Docker, Docker not answering,
# no terminal); 1 `onyx up` failed (its message names the fix; rerun this script or `sindri onyx up`). `onyx up` runs for any caller
# (owner decision q93).
# Bash 3.2 compatible (macOS /bin/bash).
set -euo pipefail

APP="${AW_ONYX_SINDRI_APP:-sindri}"
# the command to run later
LATER="$APP onyx up"

echo "=== Onyx (Sindri's business-context search) ==="

if [ "${AW_DRY_RUN:-0}" = "1" ]; then
  echo "  [dry-run] would check that Docker is installed and answering"
  echo "  [dry-run] would run: $LATER   (preflight, pinned files, start, your account, Sindri's key, the profile section; a terminal is needed)"
  exit 0
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "  WARN: Docker isn't installed, so Onyx is not set up."
  echo "        Install Docker Desktop (macOS, Windows) or Docker Engine with Compose v2 (Linux), start it, then run: $LATER"
  exit 3
fi
if ! docker info >/dev/null 2>&1; then
  echo "  WARN: Docker is installed but not answering, so Onyx is not set up."
  echo "        Start Docker Desktop (or the docker service) and wait until it is ready, then run: $LATER"
  exit 3
fi

if [ "${AW_ONYX_FORCE_TERMINAL:-0}" != "1" ] && { [ ! -t 0 ] || [ ! -t 1 ]; }; then
  echo "  Onyx setup needs you at a terminal (you create your Onyx account in the browser and approve the profile)."
  echo "        Run this later in a terminal: $LATER"
  exit 3
fi

if [ ! -x "$APP" ] && ! command -v "$APP" >/dev/null 2>&1; then
  echo "  WARN: $APP isn't there, so Onyx is not set up. Run it from a full checkout, then: $LATER"
  exit 3
fi

echo "  Running: $LATER   (safe to rerun: a set-up box changes nothing)"
rc=0
"$APP" onyx up || rc=$?
if [ "$rc" -ne 0 ]; then
  echo "  Onyx setup did not finish (exit $rc); the message above names the fix. Rerun: $LATER"
  exit 1
fi
echo "  Onyx: done. '$APP onyx status' shows where it stands; '$APP onyx open' opens it."
