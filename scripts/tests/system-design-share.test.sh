#!/usr/bin/env bash
# The /system-design shareable TDD and deck: build_share.py renders tdd-share.html and deck.html from share/story.json
# (the shipped example, two chapters, every block type), layers the project tokens tokens_to_css.py writes, inlines
# images, refuses history markers, over-limit text and bad blocks, and write_packet.py records both pages.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SD="$DIR/../../skills/system-design"
TMP="$(mktemp -d)"
trap 'find "$TMP" -delete 2>/dev/null || true' EXIT
fail() { echo "FAIL: $1"; exit 1; }
build() { python3 -I -B "$SD/scripts/build_share.py" "$@"; }

# A packet holding the shipped example story and the one diagram it names.
packet() {
  local p="$TMP/$1"; mkdir -p "$p/share" "$p/diagrams"
  cp "$SD/templates/share-story.example.json" "$p/share/story.json"
  echo '<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"/>' >"$p/diagrams/plan.svg"
  echo "$p"
}
# Rewrite the story with a python expression over `s` (the parsed JSON).
edit_story() {
  python3 -I -B - "$1/share/story.json" "$2" <<'PY'
import json, sys
path, expr = sys.argv[1], sys.argv[2]
s = json.load(open(path))
exec(expr)
json.dump(s, open(path, "w"))
PY
}
expect_refusal() {  # <packet> <message substring> <label>
  local rc=0 out
  out="$(build "$1" 2>&1)" || rc=$?
  [ "$rc" -eq 1 ] || fail "$3: expected exit 1, got $rc"
  printf '%s' "$out" | has -F -e "$2" || fail "$3: message should name '$2', got: $out"
  [ ! -e "$1/tdd-share.html" ] || fail "$3: a refused story must write nothing"
}

test_builds_both_pages_from_the_example() {
  local p; p="$(packet example)"
  build "$p" >/dev/null || fail "example story should build"
  [ -s "$p/tdd-share.html" ] && [ -s "$p/deck.html" ] || fail "both pages should be written"
  has -F -e 'id="planner"' "$p/tdd-share.html" || fail "chapter 1 anchor missing"
  has -F -e 'id="experiences"' "$p/tdd-share.html" || fail "chapter 2 anchor missing"
  has -F -e '2 parts, one design' "$p/tdd-share.html" || fail "multi-part designs list their parts"
  has -F -e 'class="sim js-sim"' "$p/tdd-share.html" || fail "lifecycle simulator missing"
  has -F -e '<mark>forecast each dock</mark>' "$p/tdd-share.html" || fail "==highlight== not rendered"
  has -F -e 'id="asks"' "$p/tdd-share.html" || fail "asks section missing on the page"
  [ "$(grep -o 'class="slide' "$p/deck.html" | wc -l | tr -d ' ')" = 20 ] || fail "deck should have 20 slides"
  has -F -e 'Decide in the room' "$p/deck.html" || fail "deck should end on the asks"
  has -F -e 'go planner first: the reward' "$p/deck.html" || fail "an ask's notes become speaker notes"
  ! has -F -e 'src="diagrams/' "$p/tdd-share.html" "$p/deck.html" || fail "images should be inlined"
  has -F -e 'src="data:image/svg+xml;base64,' "$p/tdd-share.html" || fail "diagram not inlined as a data URI"
  local before; before="$(cat "$p/tdd-share.html" "$p/deck.html")"
  build "$p" >/dev/null || fail "second build failed"
  [ "$before" = "$(cat "$p/tdd-share.html" "$p/deck.html")" ] || fail "a rebuild should be byte-identical"
  echo "PASS: test_builds_both_pages_from_the_example"
}

test_single_chapter_has_no_chapter_chrome() {
  local p; p="$(packet single)"
  edit_story "$p" 's["chapters"] = [{"sections": s["chapters"][0]["sections"]}]'
  build "$p" >/dev/null || fail "single-chapter story should build"
  ! has -F -e 'Part 1' "$p/tdd-share.html" "$p/deck.html" || fail "one part needs no part labels"
  has -F -e 'href="#why">Why</a>' "$p/tdd-share.html" || fail "nav should link sections by their short labels"
  has -F -e '02 · Why it keeps happening' "$p/tdd-share.html" || fail "sections should be numbered 01, 02, ..."
  echo "PASS: test_single_chapter_has_no_chapter_chrome"
}

test_project_tokens_win() {
  local p; p="$(packet tokens)"
  echo '{"color": {"primary": {"$value": "#ff0000"}}, "font": {"family-display": {"$value": "Display Sans"}}}' >"$TMP/tokens.json"
  python3 -I -B "$SD/scripts/tokens_to_css.py" "$TMP/tokens.json" "$p/doc-style.css" >"$TMP/tok.out" || fail "tokens_to_css failed"
  ! has -F -e '--f-heading' "$TMP/tok.out" || fail "the optional heading font should never be reported as unmapped"
  build "$p" >/dev/null || fail "build with doc-style.css failed"
  python3 -I -B - "$p/tdd-share.html" <<'PY' || fail "token block not layered after the base theme"
import sys
s = open(sys.argv[1]).read()
i = s.index("/* tokens */")
assert "--accent: #ff0000;" in s[i:] and "--f-heading: Display Sans;" in s[i:], "tokens missing after the marker"
assert "body { margin: 0; background: var(--bg); color: var(--ink); font: 400 15px" not in s, "doc-base.css layout leaked into the share page"
PY
  echo "PASS: test_project_tokens_win"
}

test_text_is_escaped() {
  local p; p="$(packet esc)"
  edit_story "$p" 's["lede"] = "Fixes <script>alert(1)</script> and `a<b`"'
  build "$p" >/dev/null || fail "build failed"
  ! has -F -e '<script>alert(1)' "$p/tdd-share.html" || fail "text fields must be escaped"
  has -F -e '<code>a&lt;b</code>' "$p/tdd-share.html" || fail "inline code should render, escaped"
  echo "PASS: test_text_is_escaped"
}

test_refuses_history() {
  local p
  p="$(packet hist1)"; edit_story "$p" 's["chapters"][0]["sections"][0]["lede"] = "This superseded the 2 Nov plan."'
  expect_refusal "$p" 'story.chapters[0].sections[0].lede: carries history' "superseded"
  p="$(packet hist2)"; edit_story "$p" 's["chapters"][1]["sections"][2]["items"][0]["a"] = "~~Any dispatcher~~ Always a dispatcher"'
  expect_refusal "$p" 'carries history' "strike-through"
  p="$(packet hist3)"; edit_story "$p" 's["chapters"].append({"title": "Addendum: week view", "sections": [{"type": "text", "title": "x", "paragraphs": ["y"]}]})'
  expect_refusal "$p" 'carries history' "addendum chapter"
  echo "PASS: test_refuses_history"
}

test_refuses_bad_stories() {
  local p
  p="$(packet long)"; edit_story "$p" 's["headline"] = "x" * 111'
  expect_refusal "$p" 'story.headline: 111 characters, the cap is 110' "long headline"
  p="$(packet list)"; edit_story "$p" 's["chapters"][0]["sections"][0]["pains"] = [{"title": "p", "text": "t"}] * 9'
  expect_refusal "$p" '9 items, the cap is 8' "long list"
  p="$(packet type)"; edit_story "$p" 's["chapters"][0]["sections"][0]["type"] = "essay"'
  expect_refusal "$p" "unknown type 'essay'" "unknown type"
  p="$(packet state)"; edit_story "$p" 's["chapters"][0]["sections"][3]["actions"][0]["to"] = "Approved"'
  expect_refusal "$p" "'Approved' is not one of the states" "lifecycle state"
  p="$(packet untitled)"; edit_story "$p" 'del s["chapters"][1]["title"]'
  expect_refusal "$p" 'names each one (title)' "untitled chapter"
  p="$(packet img)"; rm "$p/diagrams/plan.svg"
  expect_refusal "$p" 'missing image(s): diagrams/plan.svg' "missing image"
  local rc=0; build >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || fail "no arguments should exit 1, got $rc"
  rc=0; build "$TMP/nope" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || fail "a missing packet should exit 1, got $rc"
  echo "PASS: test_refuses_bad_stories"
}

test_check_writes_nothing() {
  local p; p="$(packet check)"
  build "$p" --check | has -F -e 'ok:' || fail "--check should say ok"
  [ ! -e "$p/tdd-share.html" ] && [ ! -e "$p/deck.html" ] || fail "--check must not write pages"
  echo "PASS: test_check_writes_nothing"
}

test_packet_records_share_and_deck() {
  local p; p="$(packet manifest)"
  build "$p" >/dev/null || fail "build failed"
  echo '<html><body><section id="model"><h2>Model</h2></section></body></html>' >"$p/tdd.html"
  python3 -I -B "$SD/scripts/write_packet.py" "$p" --slug demo --title Demo >/dev/null || fail "write_packet failed"
  python3 -I -B - "$p/packet.json" <<'PY' || fail "packet.json should carry share and deck, not as personas"
import json, sys
m = json.load(open(sys.argv[1]))
assert m["share"] == "tdd-share.html" and m["deck"] == "deck.html" and m["story"] == "share/story.json", m
assert set(m["personas"]) == {"engineering"}, m["personas"]
PY
  echo "PASS: test_packet_records_share_and_deck"
}

test_builds_both_pages_from_the_example
test_single_chapter_has_no_chapter_chrome
test_project_tokens_win
test_text_is_escaped
test_refuses_history
test_refuses_bad_stories
test_check_writes_nothing
test_packet_records_share_and_deck
