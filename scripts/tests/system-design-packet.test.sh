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

mkdoc() { # a packet with three cards, EARS criteria and test rows
  D="$TMP/docpkt"; mkdir -p "$D/prs"; find "$D" -type f -delete
  cat >"$D/tdd.html" <<'HTML'
<html><body><section id="scope"><h2>Scope</h2><ul>
<li>When a rider books a seat, the system shall show it booked.</li>
<li>If payment fails, the system shall release the seat | within 10 s.</li>
<li>Not a criterion without the keyword.</li></ul></section>
<section id="ph2-notes"><h2>Ph2 notes &amp; risks</h2></section>
<section id="pr-order"><h2>PR order</h2><!-- PR-CARDS --></section></body></html>
HTML
  printf '# PR 1: Booking form\n\n## Tests\n- Book a seat; it shows booked.\n- Reload | still booked.\n' >"$D/prs/pr-01.md"
  printf -- '---\ntitle: Payments\n---\n# PR 2: ignored\n\n## Test plan\n| check | note |\n|---|---|\n| Fail the payment | ok |\n| Retry \\| twice | ok |\n' >"$D/prs/pr-02.md"
  printf '# PR 3: Cleanup\n\n## Summary\nNothing to test here.\n' >"$D/prs/pr-03.md"
}

test_write_packet_doc_md() {
  mkdoc
  python3 -I -B "$SD/write_packet.py" "$D" --slug harbor-booking --title "Harbor: ferry booking" >/dev/null || fail "write_packet failed"
  [ -f "$D/doc.md" ] || fail "doc.md not written"
  local sum; sum="$(sha256sum "$D/packet.json" | cut -d' ' -f1)"
  has -x -e "source-sha256: $sum" "$D/doc.md" || fail "source-sha256 is not packet.json's sha256"
  has -x -e 'id: harbor-booking-tdd' "$D/doc.md" || fail "id not <slug>-tdd"
  has -x -e 'generated: write_packet.py' "$D/doc.md" || fail "generated key missing"
  has -x -e 'title: "Harbor: ferry booking"' "$D/doc.md" || fail "title not JSON-quoted"
  [ "$(sed -n 1p "$D/doc.md")" = "---" ] && [ "$(sed -n 2p "$D/doc.md")" = "aw-doc: 1" ] || fail "front matter should open with aw-doc: 1"
  ! has -e '^repos:' "$D/doc.md" || fail "repos present without --repo"
  for n in 1 2 3; do has -e "^### Ph$n " "$D/doc.md" || fail "### Ph$n missing"; done
  ! has -e 'Ph0' "$D/doc.md" || fail "leading zero in a phase id"
  has -x -e '### Ph2 Payments' "$D/doc.md" || fail "card title should come from front matter"
  [ "$(grep -c '^- \*\*REQ-' "$D/doc.md")" = 2 ] || fail "expected 2 REQ items"
  [ "$(grep -c '^| TP-' "$D/doc.md")" = 4 ] || fail "expected 4 TP rows"
  has -F -x -e '| TP-4 | Retry \| twice - ok | Ph2 |' "$D/doc.md" || fail "TP-4: an escaped pipe in a card table cell split the cell"
  has -F -x -e '## Section Ph2 notes & risks' "$D/doc.md" || fail "a section titled Ph2 ... must not define Ph2 (and entities unescaped)"
  has -F -x -e '| TP-3 | Fail the payment - ok | Ph2 |' "$D/doc.md" || fail "TP-3 row wrong (phase)"
  has -F -x -e '| TP-2 | Reload \| still booked. | Ph1 |' "$D/doc.md" || fail "TP-2 pipe not escaped"
  has -x -e '## Scope' "$D/doc.md" || fail "section heading missing"
  local before; before="$(cat "$D/doc.md")"
  python3 -I -B "$SD/write_packet.py" "$D" --slug harbor-booking --title "Harbor: ferry booking" >/dev/null
  [ "$before" = "$(cat "$D/doc.md")" ] || fail "rerun changed doc.md"
  echo "hand edit" >>"$D/doc.md"
  python3 -I -B "$SD/write_packet.py" "$D" --slug harbor-booking --title "Harbor: ferry booking" >/dev/null
  [ "$before" = "$(cat "$D/doc.md")" ] || fail "rerun did not overwrite a hand edit"
  python3 -I -B "$SD/write_packet.py" "$D" --slug harbor-booking --title "T" --repo acme/harbor-web >/dev/null
  has -x -e 'repos: \[acme/harbor-web\]' "$D/doc.md" || fail "--repo not written"
  python3 -I -B "$SD/write_packet.py" "$D" --slug "Harbor Booking!" --title "T" >/dev/null
  has -x -e 'id: harbor-booking-tdd' "$D/doc.md" || fail "slug 'Harbor Booking!' should give harbor-booking-tdd"
  python3 -I -B "$SD/write_packet.py" "$D" --slug "$(printf 'word-%.0s' $(seq 1 15))end" --title "T" >/dev/null
  local id; id="$(sed -n 's/^id: //p' "$D/doc.md")"
  [ "${#id}" -le 64 ] && [ "${id%-tdd}" != "$id" ] && [ "${id#*--}" = "$id" ] || fail "long slug: id '$id' should be <= 64 chars, end in -tdd, cut on a - boundary"
  local rc=0; rm -f "$D/packet.json"; python3 -I -B "$SD/write_packet.py" "$D" --slug "a!" --title "T" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || fail "a 1-char id should exit 1, got $rc"
  [ ! -e "$D/packet.json" ] || fail "a refused slug should write nothing"
  echo "PASS: test_write_packet_doc_md"
}

test_write_packet_doc_md_schema() {
  local AJV="$DIR/../node_modules/ajv/dist/2020.js" SCHEMA="$DIR/../../schemas/library/design-doc.schema.json"
  if ! command -v node >/dev/null 2>&1 || [ ! -f "$AJV" ]; then
    echo "SKIP: test_write_packet_doc_md_schema (node or scripts/node_modules/ajv is missing)"; return 0
  fi
  python3 -c 'import yaml' 2>/dev/null || { echo "SKIP: test_write_packet_doc_md_schema (PyYAML is missing)"; return 0; }
  mkdoc
  python3 -I -B "$SD/write_packet.py" "$D" --slug harbor-booking --title "Harbor: ferry booking" --repo acme/harbor-web >/dev/null
  python3 -c '
import json, sys, yaml
t = open(sys.argv[1]).read().split("---\n")[1]
print(json.dumps(yaml.safe_load(t)))' "$D/doc.md" >"$TMP/fm.json" || fail "front matter is not YAML"
  node -e '
const Ajv = require(process.argv[1]).default || require(process.argv[1]);
const ajv = new Ajv({ strict: false, allErrors: true });
const schema = JSON.parse(require("fs").readFileSync(process.argv[2], "utf8"));
ajv.addSchema(schema);
const v = ajv.getSchema(schema.$id + "#/$defs/frontMatter");
const ok = v(JSON.parse(require("fs").readFileSync(process.argv[3], "utf8")));
if (!ok) { console.error(JSON.stringify(v.errors)); process.exit(1); }' "$AJV" "$SCHEMA" "$TMP/fm.json" || fail "front matter fails the design-doc schema"
  echo "PASS: test_write_packet_doc_md_schema"
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
test_write_packet_doc_md
test_write_packet_doc_md_schema
test_storyboard_layout
