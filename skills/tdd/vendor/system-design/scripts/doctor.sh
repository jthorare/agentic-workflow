#!/usr/bin/env bash
# Check what /system-design needs: python3, Node 20+, the tldraw offline app running, playwright-core and its Chromium.
#
#   doctor.sh            # report; exit 0 when everything is ready, 1 otherwise
#
# Install commands are printed, never run. Playwright is looked up at $PLAYWRIGHT_CORE, else
# $SYSTEM_DESIGN_DEPS/node_modules (default ~/.local/share/system-design-skills, shared with system-design-video).
set -uo pipefail
ok=0
need() { command -v "$1" >/dev/null 2>&1 && echo "ok    $1" || { echo "MISSING $1: $2"; ok=1; }; }
need python3 "install Python 3.12+ (brew install python)"
need node "install Node 20+ (fnm install --lts)"
if command -v node >/dev/null 2>&1; then
  major=$(node -p 'process.versions.node.split(".")[0]')
  [ "$major" -ge 20 ] && echo "ok    node $major" || { echo "MISSING node 20+: found $major (fnm install --lts)"; ok=1; }
fi
sj=${TLDRAW_SERVER_JSON:-}
if [ -z "$sj" ]; then
  if [ "$(uname -s)" = Darwin ]; then sj="$HOME/Library/Application Support/tldraw/server.json"
  else sj="${XDG_CONFIG_HOME:-$HOME/.config}/tldraw/server.json"; fi
fi
if [ -f "$sj" ]; then
  echo "ok    tldraw offline running ($sj)"
else
  echo "MISSING tldraw offline (diagrams only): brew install --cask tldraw, then open -a \"tldraw offline\""; ok=1
fi
deps=${SYSTEM_DESIGN_DEPS:-$HOME/.local/share/system-design-skills}
core=${PLAYWRIGHT_CORE:-$deps/node_modules/playwright-core}
if [ -f "$core/package.json" ]; then
  echo "ok    playwright-core at $core"
  # the package alone is not enough: its Chromium must be downloaded too (playwright-core install chromium)
  if node -e 'process.exit(require("fs").existsSync(require(process.argv[1]).chromium.executablePath()) ? 0 : 1)' "$core" 2>/dev/null; then
    echo "ok    chromium for playwright-core"
  else
    echo "MISSING chromium for playwright-core: (cd $(dirname "$(dirname "$core")") && npx playwright-core install chromium)"; ok=1
  fi
else
  echo "MISSING playwright-core (page verification): mkdir -p $deps && (cd $deps && npm i playwright-core && npx playwright-core install chromium)"
  ok=1
fi
exit $ok
