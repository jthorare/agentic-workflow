#!/usr/bin/env bash
# The /system-design packet scripts: build_prs.py (PR cards + dialog), write_packet.py (packet.json) and, when node can
# import TypeScript, packet-to-storyboard.ts (mermaid -> whiteboard beats; its parser and layout are imported, no bun).
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SD="$DIR/../../skills/system-design/scripts"
TS="$(cd "$DIR/../../skills/system-design-video/scripts" && pwd)/packet-to-storyboard.ts"
TMP="$(mktemp -d)"
trap 'find "$TMP" -delete 2>/dev/null || true' EXIT
fail() { echo "FAIL: $1"; exit 1; }

P="$TMP/packet"; mkdir -p "$P/prs" "$P/diagrams"
cat >"$P/prs/pr-2.md" <<'MD'
---
milestone: "M1 · Model"
milestone_note: "no visible change"
---
# PR 2: Add the ledger

## Summary
Adds the ledger table. Rollback: drop the table.

## Code changes
| path:line | change |
|---|---|
| src/Ledger.cs:10 | new `Ledger` type |
MD
cat >"$P/prs/pr-10.md" <<'MD'
# PR 10: Switch readers

## Summary
Readers use the ledger. Rollback: flip the flag back.
MD
cat >"$P/tdd.html" <<'HTML'
<html><body><section id="model"><h2>The <b>model</b></h2><img src="diagrams/model.svg"></section>
<section id="pr-order"><h2>PR order</h2><!-- PR-CARDS --></section></body></html>
HTML
echo '<html></html>' >"$P/product-design.html"
echo '<html></html>' >"$P/product-design.src.html"
printf 'flowchart TB\n  a["Orders"] --> b["Ledger"]\n' >"$P/diagrams/model.mmd"
echo '<svg/>' >"$P/diagrams/model.svg"

test_build_prs_cards_and_idempotent() {
  python3 -I -B "$SD/build_prs.py" "$P/tdd.html" "$P/prs" >/dev/null || fail "build_prs failed"
  has -F -e 'data-pr="2"' "$P/tdd.html" || fail "card for PR 2 missing"
  has -F -e 'M1 · Model' "$P/tdd.html" || fail "milestone heading missing"
  has -F -e 'no visible change' "$P/tdd.html" || fail "milestone note missing"
  has -F -e '<code>Ledger</code>' "$P/tdd.html" || fail "inline code in the deep-dive table not rendered"
  has -F -e 'drop the table' "$P/tdd.html" || fail "rollback text not taken from the summary"
  [ "$(grep -o 'class="pr" data-pr="[0-9]*"' "$P/tdd.html" | tr '\n' ' ')" = 'class="pr" data-pr="2" class="pr" data-pr="10" ' ] || fail "cards should run PR 2 then PR 10"
  local before; before="$(cat "$P/tdd.html")"
  python3 -I -B "$SD/build_prs.py" "$P/tdd.html" "$P/prs" >/dev/null || fail "second run failed"
  [ "$before" = "$(cat "$P/tdd.html")" ] || fail "second run changed the page"
  echo "PASS: test_build_prs_cards_and_idempotent"
}

test_build_prs_refusals() {
  local rc=0
  echo '<html><body></body></html>' >"$TMP/bare.html"
  python3 -I -B "$SD/build_prs.py" "$TMP/bare.html" "$P/prs" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || fail "page without a placeholder should exit 1, got $rc"
  rc=0; python3 -I -B "$SD/build_prs.py" "$TMP/bare.html" "$TMP/nope" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || fail "missing prs dir should exit 1, got $rc"
  rc=0; python3 -I -B "$SD/build_prs.py" "$TMP/bare.html" "$P/prs" --shots >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || fail "--shots without a file should exit 1, got $rc"
  echo "PASS: test_build_prs_refusals"
}

test_write_packet_manifest() {
  python3 -I -B "$SD/write_packet.py" "$P" --slug demo --title "Demo" >/dev/null || fail "write_packet failed"
  python3 -I -B - "$P/packet.json" <<'PY' || fail "packet.json content wrong"
import json, sys
m = json.load(open(sys.argv[1]))
assert m["slug"] == "demo" and m["title"] == "Demo", m
assert set(m["personas"]) == {"engineering", "product-design"}, m["personas"]  # no product-design.src
assert m["diagrams"]["model"] == {"mmd": "diagrams/model.mmd", "svg": "diagrams/model.svg"}, m["diagrams"]
assert [s["id"] for s in m["sections"]] == ["model", "pr-order"], m["sections"]
assert m["sections"][0]["title"] == "The model" and m["sections"][0]["anchor"] == "#model"
assert m["sections"][0]["diagrams"] == ["model"] and m["sections"][1]["diagrams"] == []
assert m["prs"] == ["prs/pr-2.md", "prs/pr-10.md"], m["prs"]  # numeric order
PY
  echo "PASS: test_write_packet_manifest"
}

test_write_packet_refusals() {
  local rc=0
  mkdir -p "$TMP/empty"
  python3 -I -B "$SD/write_packet.py" "$TMP/empty" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || fail "packet without tdd.html should exit 1, got $rc"
  rc=0; python3 -I -B "$SD/write_packet.py" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || fail "no arguments should exit 1, got $rc"
  echo "PASS: test_write_packet_refusals"
}

test_storyboard_layout() {
  if ! node -e 'process.exit(Number(process.versions.node.split(".")[0]) >= 22 ? 0 : 1)' >/dev/null 2>&1; then
    echo "SKIP: test_storyboard_layout (node 22+ is needed to import the .ts)"; return 0
  fi
  cat >"$TMP/t.mjs" <<JS
import { parseMermaid, layout, scene } from "$TS";
const g = parseMermaid(\`flowchart TB
  subgraph old["Old model"]
    a["Orders table"] --> b["Totals"]
  end
  b -->|"backfill"| c(("Done"))
  c -.-> d[Unquoted label]
\`);
const size = layout(g);
const s = scene(g, 0, 0, [["Done", "blue"]], "TDD #model");
const st = parseMermaid("stateDiagram-v2\\n  [*] --> Off\\n  Off --> On: flip\\n  On: lights on");
let empty = "no error";
try { parseMermaid(""); } catch (e) { empty = e.message; }
console.log(JSON.stringify({ ids: [...g.nodes.keys()].join(","), labels: [...g.nodes.values()].map((n) => n.label).join("|"),
  groups: g.groups.size, edges: g.edges.length, dashed: g.edges.filter((e) => e.dashed).length, edgeLabel: g.edges[1].label,
  placed: size.w > 0 && size.h > 0, srcs: s.beats.map((b) => b.src), first: s.beats[0].say,
  states: [...st.nodes.values()].map((n) => n.label).join("|"), empty }));
JS
  local out; out="$(node "$TMP/t.mjs" 2>/dev/null)" || fail "node could not run the harness"
  python3 -I -B -c '
import json, sys
r = json.loads(sys.argv[1])
assert r["ids"] == "a,b,c,d", r
assert r["labels"] == "Orders table|Totals|Done|Unquoted label", r["labels"]
assert r["groups"] == 1 and r["edges"] == 3 and r["dashed"] == 1 and r["edgeLabel"] == "backfill", r
assert r["placed"] and r["srcs"] and all(x == "TDD #model" for x in r["srcs"]), r["srcs"]
assert r["first"] == "Old model.", r["first"]
assert r["states"] == "start|Off|lights on|end" or r["states"].startswith("start|Off|"), r["states"]
assert r["empty"] == "empty mermaid source", r["empty"]
' "$out" || fail "storyboard layout wrong: $out"
  echo "PASS: test_storyboard_layout"
}

test_build_prs_cards_and_idempotent
test_build_prs_refusals
test_write_packet_manifest
test_write_packet_refusals
test_storyboard_layout
