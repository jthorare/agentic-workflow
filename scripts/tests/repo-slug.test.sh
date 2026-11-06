#!/usr/bin/env bash
# Tests for skills/_shared/repo-slug.sh: AW_DIR is computed from the remote and AW_STATE_DIR as before, and a preset AW_DIR
# (a Sindri Step gets the run's folder from the host, skill-flows spec D8) is kept, whatever the checkout's remotes say.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
SLUG_SH="$REPO/skills/_shared/repo-slug.sh"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/repo-slug-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }
expect() { if [ "$2" = "$3" ]; then ok "$1"; else fail "$1: got '$2', want '$3'"; fi; }

git init -q "$TMPROOT/checkout"
git -C "$TMPROOT/checkout" remote add origin git@github.com:acme/api.git

# slug_dir [VAR=VALUE...]: AW_DIR as the script leaves it, run from the checkout under a clean environment.
slug_dir() { (cd "$TMPROOT/checkout" && env -u AW_DIR -u AW_STATE_DIR HOME="$TMPROOT/home" "$@" bash -c '. "$0"; printf %s "$AW_DIR"' "$SLUG_SH"); }

echo "repo-slug.sh"
expect "unset AW_DIR: the remote's slug under the default state dir" "$(slug_dir)" "$TMPROOT/home/.agentic-workflow/acme-api"
expect "unset AW_DIR: AW_STATE_DIR moves it" "$(slug_dir AW_STATE_DIR="$TMPROOT/state")" "$TMPROOT/state/acme-api"
expect "a preset AW_DIR is kept" "$(slug_dir AW_DIR="$TMPROOT/run/dir")" "$TMPROOT/run/dir"
expect "a preset AW_DIR beats AW_STATE_DIR" "$(slug_dir AW_DIR="$TMPROOT/run/dir" AW_STATE_DIR="$TMPROOT/state")" "$TMPROOT/run/dir"
slug_dir AW_DIR="$TMPROOT/run/dir" >/dev/null
if [ -d "$TMPROOT/run/dir" ]; then ok "a preset AW_DIR is created"; else fail "a preset AW_DIR was not created"; fi
expect "sourced twice, the first value stays" "$(cd "$TMPROOT/checkout" && env -u AW_DIR HOME="$TMPROOT/home" AW_STATE_DIR="$TMPROOT/state" bash -c '. "$0"; . "$0"; printf %s "$AW_DIR"' "$SLUG_SH")" "$TMPROOT/state/acme-api"

echo
echo "passed: $PASS, failed: $FAIL"
[ "$FAIL" -eq 0 ]
