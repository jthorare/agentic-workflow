#!/usr/bin/env bash
# skills/system-design/scripts/draw_diagrams.mjs against a stub tldraw server: the dark export asks tldraw for darkMode and
# swaps the palette it reports for Sindri's by role, light leaves the SVG as tldraw wrote it, a bad theme exits 2.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$DIR/../../skills/system-design/scripts/draw_diagrams.mjs"
TMP="$(mktemp -d)"
SERVER_PID=""
cleanup() { [ -z "$SERVER_PID" ] || kill "$SERVER_PID" 2>/dev/null || true; find "$TMP" -delete 2>/dev/null || true; }
trap cleanup EXIT
fail() { echo "FAIL: $1"; exit 1; }
command -v node >/dev/null || { echo "SKIP: node missing"; exit 0; }

mkdir -p "$TMP/spec"
printf 'flowchart TB\n  A[new] --> B[old]\n' >"$TMP/spec/plan.mmd"
echo '[["new", "blue"]]' | sed 's/^/{"plan": /; s/$/}/' >"$TMP/spec/colors.json"

# The stub app: /api/search finds no doc, /api/docs/create returns an id, /exec records the code it was sent and answers
# with an SVG drawn in "tldraw's dark colors" (#4465e9 stroke and #282e49 fill for blue, #9fa0a8 and #25262b for grey).
cat >"$TMP/stub.mjs" <<'JS'
import http from "node:http";
import fs from "node:fs";
const [dir] = process.argv.slice(2);
const srv = http.createServer((req, res) => {
  let body = "";
  req.on("data", (c) => (body += c));
  req.on("end", () => {
    const send = (o) => { res.setHeader("content-type", "application/json"); res.end(JSON.stringify(o)); };
    if (req.url === "/api/search") return send({ success: true, result: [] });
    if (req.url === "/api/docs/create") return send({ success: true, id: "doc1" });
    if (req.url.endsWith("/exec")) {
      fs.writeFileSync(`${dir}/exec-code.txt`, JSON.parse(body).code);
      const svg = '<svg><rect fill="#282e49" stroke="#4465e9"/><text fill="#4465e9">new</text><rect fill="#25262b" stroke="#9fa0a8"/></svg>';
      return send({ success: true, result: { shapes: 2, lints: [], svg, found: { blue: { solid: "#4465e9", semi: "#282e49" }, grey: { solid: "#9fa0a8", semi: "#25262b" } } } });
    }
    res.statusCode = 404; send({ success: false });
  });
});
srv.listen(0, "127.0.0.1", () => {
  fs.writeFileSync(`${dir}/server.json`, JSON.stringify({ port: srv.address().port, token: "t" }));
});
JS
node "$TMP/stub.mjs" "$TMP" &
SERVER_PID=$!
for _ in $(seq 1 50); do [ -s "$TMP/server.json" ] && break; sleep 0.1; done
[ -s "$TMP/server.json" ] || fail "stub server did not start"
export TLDRAW_SERVER_JSON="$TMP/server.json"
draw() { node "$SCRIPT" --spec "$TMP/spec" --out "$1" "${@:2}"; }

test_dark_is_the_default_and_swaps_the_palette() {
  draw "$TMP/dark" >/dev/null || fail "dark draw failed"
  has -F -e 'const DARK = true;' "$TMP/exec-code.txt" || fail "export should ask tldraw for darkMode"
  has -F -e '#828fff' "$TMP/dark/plan.svg" || fail "blue stroke should be Sindri's primary-hover"
  has -F -e '#1d2036' "$TMP/dark/plan.svg" || fail "blue fill should be Sindri's primary-soft"
  has -F -e '#8a8f98' "$TMP/dark/plan.svg" || fail "grey stroke should be Sindri's ink-subtle"
  ! has -F -e '#4465e9' "$TMP/dark/plan.svg" || fail "tldraw's blue should be gone"
  ! has -F -e '#9fa0a8' "$TMP/dark/plan.svg" || fail "tldraw's grey should be gone"
  echo "PASS: test_dark_is_the_default_and_swaps_the_palette"
}

test_light_leaves_tldraw_colors() {
  draw "$TMP/light" --theme light >/dev/null || fail "light draw failed"
  has -F -e 'const DARK = false;' "$TMP/exec-code.txt" || fail "light export should not ask for darkMode"
  has -F -e '#4465e9' "$TMP/light/plan.svg" || fail "light should keep the colors tldraw wrote"
  echo "PASS: test_light_leaves_tldraw_colors"
}

test_bad_theme_exits_2() {
  local rc=0
  draw "$TMP/x" --theme sepia >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 2 ] || fail "a bad --theme should exit 2, got $rc"
  echo "PASS: test_bad_theme_exits_2"
}

test_dark_is_the_default_and_swaps_the_palette
test_light_leaves_tldraw_colors
test_bad_theme_exits_2
