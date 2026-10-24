#!/usr/bin/env bash
# scripts/aw-ui-driver.sh — runs the UI driver's command line (aw-ui-driver, src/UiDriver/Aw.UiDriver.Cli; docs/csharp/ui-driver.md)
# with the arguments given. Desktop evidence (.agentic-workflow/evidence.json "desktop.driver") calls it once per verb.
# AW_UI_DRIVER_BIN names a built aw-ui-driver to run instead; otherwise the CLI is published (Release, framework-dependent) into
# $AW_STATE_DIR/bin/aw-ui-driver/ when any UiDriver source is newer than that build, and run from there.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ -n "${AW_UI_DRIVER_BIN:-}" ]; then
  [ -x "$AW_UI_DRIVER_BIN" ] || { echo "aw-ui-driver.sh: AW_UI_DRIVER_BIN=$AW_UI_DRIVER_BIN is not an executable" >&2; exit 1; }
  exec "$AW_UI_DRIVER_BIN" "$@"
fi

out="${AW_STATE_DIR:-$HOME/.agentic-workflow}/bin/aw-ui-driver"
bin="$out/aw-ui-driver"
stale=1
if [ -x "$bin" ] && [ -z "$(find "$root/src/UiDriver" -type f \( -name '*.cs' -o -name '*.csproj' \) -newer "$bin" -print -quit)" ]; then stale=0; fi
if [ "$stale" = 1 ]; then
  echo "aw-ui-driver.sh: publishing aw-ui-driver to $out" >&2
  dotnet publish "$root/src/UiDriver/Aw.UiDriver.Cli" -c Release -o "$out" --nologo -v quiet >&2 || { echo "aw-ui-driver.sh: publishing aw-ui-driver failed" >&2; exit 1; }
fi
exec "$bin" "$@"
