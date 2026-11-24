#!/usr/bin/env bash
# scripts/lib/ensure-scripts-deps.sh — ensure_scripts_deps [DIR]: make sure scripts/'s dev dependencies (Ajv, from
# scripts/package.json) are installed in DIR/node_modules (default: this repo's scripts/). Source it; it defines
# functions only and sets no options. Used by the suites that run scripts/check-launcher.mjs and check-fleet.mjs.
#
# bash-tests.sh runs suites in parallel in one tree, so two of them can need the install at once. One `npm ci` runs
# at a time under a lock directory (mkdir is atomic, and works on macOS, which has no flock); a caller that finds the
# lock waits for it, then sees the finished install and returns. The fast path is Ajv resolving from DIR, not only
# its directory existing, so a half-written install from another caller is never taken as done.
# Returns 0 when Ajv resolves, 1 (with one line on stderr) when the install fails or the lock is held past ~5 min.

_scripts_deps_ready() { (cd "$1" && node -e "require.resolve('ajv/package.json')" >/dev/null 2>&1); }

ensure_scripts_deps() {
  local dir="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}" lock tries=0
  _scripts_deps_ready "$dir" && return 0
  lock="$dir/.npm-ci.lock"
  until mkdir "$lock" 2>/dev/null; do
    tries=$((tries + 1))
    if [ "$tries" -ge 600 ]; then echo "cannot install Ajv: $lock held for 5 min (remove it if no npm ci is running)" >&2; return 1; fi
    sleep 0.5
  done
  if ! _scripts_deps_ready "$dir"; then
    # why: Node writes a node-compile-cache folder into TMPDIR during npm ci, and bash-tests.sh fails a suite that leaves anything there (#3262)
    if ! (cd "$dir" && NODE_DISABLE_COMPILE_CACHE=1 npm ci --ignore-scripts --no-audit --no-fund >/dev/null 2>&1); then
      rmdir "$lock"
      echo "cannot install Ajv (npm ci in $dir)" >&2
      return 1
    fi
  fi
  rmdir "$lock"
  _scripts_deps_ready "$dir" || { echo "Ajv is not installed in $dir after npm ci" >&2; return 1; }
}
