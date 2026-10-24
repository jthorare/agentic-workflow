#!/usr/bin/env bash
# scripts/sindri-ui-app.sh — starts the REAL sindri-ui for desktop UI evidence and stays in the foreground until it is stopped.
# It publishes sindri-ui when the source changed (scripts/sindri-app.sh --publish-only) and runs the newest build. Desktop
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
  "$root/scripts/sindri-app.sh" --publish-only >&2 || { echo "sindri-ui-app: publishing sindri-ui failed" >&2; exit 1; }
  state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
  for dir in $(ls -td "$state"/bin/sindri-app/*/ 2>/dev/null); do
    if [ -x "${dir}sindri-ui" ]; then ui="${dir}sindri-ui"; break; fi
  done
  [ -n "$ui" ] || { echo "sindri-ui-app: no published sindri-ui under $state/bin/sindri-app" >&2; exit 1; }
fi

exec "$ui"
