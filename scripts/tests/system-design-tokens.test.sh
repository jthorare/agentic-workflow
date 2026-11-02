#!/usr/bin/env bash
# skills/system-design/scripts/tokens_to_css.py: known tokens map onto the semantic variables, unmapped ones are printed
# and keep the neutral fallback, and a missing tokens file fails without writing a stylesheet.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$DIR/../../skills/system-design/scripts/tokens_to_css.py"
TMP="$(mktemp -d)"
trap 'find "$TMP" -delete 2>/dev/null || true' EXIT

fail() { echo "FAIL: $1"; exit 1; }

cat >"$TMP/tokens.json" <<'JSON'
{
  "color": {
    "background": { "$value": "#faf3e0", "$type": "color" },
    "text": { "$value": "#222222" },
    "accent": { "$value": "{color.teal}" },
    "teal": { "$value": "#0f766e" }
  },
  "font": { "family-body": { "$value": ["Source Serif", "Georgia", "serif"] } }
}
JSON

test_maps_known_tokens() {
  local out
  out="$(python3 -I "$SCRIPT" "$TMP/tokens.json" "$TMP/a.css")"
  has -F -e '--bg: #faf3e0;' "$TMP/a.css" || fail "--bg not mapped from color.background"
  has -F -e '--ink: #222222;' "$TMP/a.css" || fail "--ink not mapped from color.text"
  has -F -e '--accent: #0f766e;' "$TMP/a.css" || fail "alias {color.teal} not resolved for --accent"
  has -F -e '--f-body: "Source Serif", Georgia, serif;' "$TMP/a.css" || fail "font list not joined into a CSS value"
  has -F -e '--color-teal: #0f766e;' "$TMP/a.css" || fail "--color-<name> variable missing"
  has -F -e '.wrap {' "$TMP/a.css" || fail "doc-base.css not included"
  echo "PASS: test_maps_known_tokens"
}

test_prints_unmapped() {
  local out
  out="$(python3 -I "$SCRIPT" "$TMP/tokens.json" "$TMP/b.css")"
  echo "$out" | has -F -e '--good' || fail "unmapped --good not printed: $out"
  echo "$out" | has -F -e '--radius' || fail "unmapped --radius not printed: $out"
  if echo "$out" | has -F -e '--bg'; then fail "--bg is mapped but was printed as unmapped: $out"; fi
  # an unmapped variable keeps the neutral fallback from doc-base.css and gets no override in the :root block
  has -F -e '--good: #2b8a3e;' "$TMP/b.css" || fail "neutral fallback for --good missing"
  echo "PASS: test_prints_unmapped"
}

test_empty_tokens_use_fallbacks() {
  echo '{}' >"$TMP/empty.json"
  local out
  out="$(python3 -I "$SCRIPT" "$TMP/empty.json" "$TMP/c.css")"
  echo "$out" | has -F -e '--bg' || fail "empty tokens should list every variable as unmapped: $out"
  has -F -e '--bg: #fafafa;' "$TMP/c.css" || fail "neutral --bg fallback missing"
  echo "PASS: test_empty_tokens_use_fallbacks"
}

test_missing_file_fails_without_output() {
  local rc=0
  python3 -I "$SCRIPT" "$TMP/nope.json" "$TMP/d.css" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || fail "missing tokens file should exit 1, got $rc"
  [ ! -e "$TMP/d.css" ] || fail "a stylesheet was written for a missing tokens file"
  rc=0
  python3 -I "$SCRIPT" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || fail "no arguments should exit 1, got $rc"
  echo "PASS: test_missing_file_fails_without_output"
}

test_maps_known_tokens
test_prints_unmapped
test_empty_tokens_use_fallbacks
test_missing_file_fails_without_output
