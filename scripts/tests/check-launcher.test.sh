#!/usr/bin/env bash
# Tests for scripts/check-launcher.mjs (the launcher.json contract, SL-0): the repo passes, and each kind of drift fails.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK="$REPO/scripts/check-launcher.mjs"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-launcher-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# Ajv is a dev dependency of scripts/ (scripts/package.json); install it when missing.
if [ ! -d "$REPO/scripts/node_modules/ajv" ]; then
  (cd "$REPO/scripts" && npm ci --ignore-scripts --no-audit --no-fund >/dev/null 2>&1) || { echo "cannot install Ajv (npm ci --prefix scripts)"; exit 1; }
fi

run() { set +e; OUT="$(node "$CHECK" --root "$1" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | grep -qF -- "$2"; then ok "$1"; else fail "$1: output lacks '$2'"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }

# A throwaway copy of the files the check reads.
copy() {
  local d="$TMPROOT/$1"
  mkdir -p "$d/schemas" "$d/config" "$d/tests/fixtures" "$d/docs/csharp"
  cp "$REPO/docs/csharp/sindri-launcher.md" "$d/docs/csharp/"
  cp "$REPO/schemas/launcher.schema.json" "$d/schemas/"
  cp -R "$REPO/config/launcher" "$d/config/"
  cp -R "$REPO/tests/fixtures/launcher" "$d/tests/fixtures/"
  echo "$d"
}

echo "repo"
run "$REPO"; expect_rc "the repo's fixtures, data files and goldens pass" 0; expect_out "prints ok" "check-launcher: ok"

echo "drift"
d="$(copy verdict)"
cp "$d/tests/fixtures/launcher/minimal.json" "$d/tests/fixtures/launcher/unknown-top-key.json"
run "$d"; expect_rc "a fixture whose verdict differs from its manifest entry fails" 1; expect_out "names the fixture" "unknown-top-key.json: expected invalid"

d="$(copy unlisted)"
echo '{"schema":1}' > "$d/tests/fixtures/launcher/stray.json"
run "$d"; expect_rc "a fixture missing from the manifest fails" 1; expect_out "names it" "stray.json: not listed in manifest.json"

d="$(copy hash)"
g="$(ls "$d"/tests/fixtures/launcher/argv-hash/*.json | head -1)"
perl -pi -e 's/"argvHash": "sha256:(.)/"argvHash": "sha256:z$1/' "$g"
run "$d"; expect_rc "a tampered argvHash golden fails" 1; expect_out "reports the right hash" "argvHash is sha256:"

d="$(copy builtin)"
perl -pi -e 's/"--resume", "\{sessionId\}"/"--resume", "{sessionid}"/' "$d/config/launcher/builtins.json"
run "$d"; expect_rc "a built-in with an unknown placeholder fails" 1; expect_out "names the entry" "builtins.json: claude-code"

d="$(copy terminal)"
perl -pi -e 's/"--title", "\{title\}", "--directory"/"--directory"/' "$d/config/launcher/terminals.json"
run "$d"; expect_rc "a terminal template without {title} fails" 1; expect_out "names it" "kitty: argv has no {title}"

d="$(copy keys)"
perl -0pi -e 's/"mcpForms": \[[^]]*\]/"mcpForms": ["none"]/' "$d/schemas/launcher.schema.json"
run "$d"; expect_rc "x-aw-keys out of step with the schema fails" 1; expect_out "names the list" "x-aw-keys.mcpForms"

d="$(copy doc)"
perl -pi -e 's/"--directory", "\{cwd\}", "--"/"--dir", "{cwd}", "--"/' "$d/docs/csharp/sindri-launcher.md"
run "$d"; expect_rc "a worked example that differs from terminals.json fails" 1; expect_out "names the terminal" "kitty: the worked example"

echo
echo "check-launcher.test.sh: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
