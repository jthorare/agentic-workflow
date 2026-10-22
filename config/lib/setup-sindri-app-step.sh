#!/usr/bin/env bash
# The Sindri app step of setup.sh --with-sindri (setup-once spec §9, SO-A7a): publish the app right after the provider installs,
# then start it detached when a desktop session exists. Sourced by setup.sh and by scripts/tests/setup-sindri-app.test.sh.
#
#   aw_setup_sindri_app_publish <toolkit-dir>   publish (`sindri-app --publish-only`); returns 0 published (or dry run), 1 failed
#   aw_setup_sindri_app_start <toolkit-dir>     start detached and check it answers; returns 0 running, headless or dry run, 1 did not answer
#
# AW_SINDRI_APP=PATH replaces the launcher for both calls (tests point it at a stub). AW_SETUP_DESKTOP=1|0 overrides the desktop
# detection (tests). AW_SETUP_SINDRI_WAIT=N is how many seconds the start check waits for an answer (default 60).
# Needs aw_dry and $AW_STATE_DIR (providers/lib.sh). Bash 3.2 compatible (macOS /bin/bash).

# A desktop session: always on macOS; on Linux DISPLAY or WAYLAND_DISPLAY set and SSH_CONNECTION unset.
aw_setup_has_desktop() {
  case "${AW_SETUP_DESKTOP:-}" in 1) return 0 ;; 0) return 1 ;; esac
  [ "$(uname)" = "Darwin" ] && return 0
  [ -z "${SSH_CONNECTION:-}" ] && { [ -n "${DISPLAY:-}" ] || [ -n "${WAYLAND_DISPLAY:-}" ]; }
}

# The launcher to run after the publish: AW_SINDRI_APP, the `sindri-app` link the publish made, else the checkout's script.
aw_setup_sindri_app_launcher() {
  if [ -n "${AW_SINDRI_APP:-}" ]; then printf '%s' "$AW_SINDRI_APP"
  elif command -v sindri-app >/dev/null 2>&1; then command -v sindri-app
  else printf '%s' "$1/scripts/sindri-app.sh"; fi
}

aw_setup_sindri_app_publish() {
  local toolkit="$1" app="${AW_SINDRI_APP:-$1/scripts/sindri-app.sh}" log="$AW_STATE_DIR/cache/sindri-app-build/publish.log"
  echo ""
  echo "=== Sindri app ==="
  if aw_dry; then
    echo "  [dry-run] would run sindri-app --publish-only"
    return 0
  fi
  echo "  Publishing the Sindri app, about 5 to 10 minutes; it may download the .NET SDK..."
  if "$app" --publish-only; then
    echo "  Sindri app: published"
    return 0
  fi
  echo "FATAL: The Sindri app could not be published; see $log" >&2
  return 1
}

aw_setup_sindri_app_start() {
  local toolkit="$1" app logfile="$AW_STATE_DIR/logs/sindri-app.log" i wait_s="${AW_SETUP_SINDRI_WAIT:-60}" rc
  if ! aw_setup_has_desktop; then
    app="$(aw_setup_sindri_app_launcher "$toolkit")"
    if aw_dry; then echo "  [dry-run] no desktop session: would print: $app --headless"; return 0; fi
    echo "  No desktop session here. Start Sindri in a terminal: $app --headless"
    return 0
  fi
  if aw_dry; then
    echo "  [dry-run] would start the app detached (AW_SINDRI_APP_NO_MCP=1 nohup sindri-app, log $logfile) and check sindri-app status --json"
    return 0
  fi
  app="$(aw_setup_sindri_app_launcher "$toolkit")"
  rc=0; "$app" status --json >/dev/null 2>&1 || rc=$?
  if [ "$rc" -le 1 ]; then
    echo "  Sindri is already running. If the app was just updated, run: $app stop, then: $app"
    return 0
  fi
  (umask 077; mkdir -p "$AW_STATE_DIR/logs"; : >> "$logfile")
  AW_SINDRI_APP_NO_MCP=1 nohup "$app" </dev/null >>"$logfile" 2>&1 &
  for i in $(seq 1 "$wait_s"); do
    rc=0; "$app" status --json >/dev/null 2>&1 || rc=$?
    if [ "$rc" -le 1 ]; then echo "  Sindri is running"; return 0; fi
    sleep 1
  done
  echo "Sindri didn't start, see $logfile" >&2
  return 1
}
