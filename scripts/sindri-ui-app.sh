#!/usr/bin/env bash
# scripts/sindri-ui-app.sh — starts the REAL sindri-ui for desktop UI evidence and stays in the foreground until it is stopped.
# It publishes sindri-ui when the source changed (scripts/install-cs.sh --publish, which leaves `current` and the wrappers alone) and runs the build for this source from $AW_STATE_DIR/bin/cs/<stamp>/. Desktop
# evidence (Evidence `desktop-run`, .agentic-workflow/evidence.json "desktop.app") starts this with AW_UI_DRIVER=1 and
# AW_UI_DRIVER_DIR set, which makes the app drivable by aw-ui-driver (docs/csharp/ui-driver.md), and with AW_SINDRI_HOME pointing at
# the seeded fixture's control plane; the display is Evidence's (the user's DISPLAY, else xvfb-run). It takes no arguments.
# AW_SINDRI_UI names a sindri-ui binary to run instead of publishing one. Exit: sindri-ui's; 1 when no binary could be found or published.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
[ -n "${AW_SINDRI_HOME:-}" ] || { echo "sindri-ui-app: AW_SINDRI_HOME is not set" >&2; exit 1; }

ui="${AW_SINDRI_UI:-}"
if [ -n "$ui" ]; then
  [ -x "$ui" ] || { echo "sindri-ui-app: AW_SINDRI_UI=$ui is not an executable" >&2; exit 1; }
else
  state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
  log="$(mktemp "${TMPDIR:-/tmp}/sindri-ui-app.XXXXXX")"
  trap 'rm -f "$log"' EXIT
  "$root/scripts/install-cs.sh" --publish 2>&1 | tee "$log" >&2 || { echo "sindri-ui-app: publishing sindri-ui failed" >&2; exit 1; }
  # install-cs.sh says which stamp is this source's build: "build <stamp> is current" or "built <dir>/<stamp> (<revision>)".
  stamp="$(sed -n -e 's/^.*build \([0-9a-f]\{16\}\) is current.*$/\1/p' -e 's/^.*built .*\/\([0-9a-f]\{16\}\) (.*$/\1/p' "$log" | tail -1)"
  [ -n "$stamp" ] && [ -x "$state/bin/cs/$stamp/sindri-ui" ] \
    || { echo "sindri-ui-app: no published sindri-ui under $state/bin/cs" >&2; exit 1; }
  ui="$state/bin/cs/$stamp/sindri-ui"
  rm -f "$log"   # exec below skips the EXIT trap
fi

exec "$ui"
