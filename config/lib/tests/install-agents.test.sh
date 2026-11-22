#!/usr/bin/env bash
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$DIR/../install-agents.sh"

sha() { shasum -a 256 "$1" | awk '{print $1}'; }

test_installs_all_four_and_records_their_hashes() {
  local src dest manifest
  src="$(mktemp -d)"; dest="$(mktemp -d)"; manifest="$(mktemp)"; echo '{}' > "$manifest"
  cp "$DIR/../../agents/"*.md "$src/"
  install_agents "$src" "$dest" "$manifest"
  local count
  count="$(ls "$dest"/*.md | wc -l | tr -d ' ')"
  if [ "$count" -ne 4 ]; then
    echo "FAIL: expected 4 installed agent files, got $count"; rm -rf "$src" "$dest" "$manifest"; exit 1
  fi
  local recorded actual
  recorded="$(jq -r '.["lean-coder.md"]' "$manifest")"
  actual="$(sha "$dest/lean-coder.md")"
  if [ "$recorded" != "$actual" ]; then
    echo "FAIL: manifest hash doesn't match installed file"; rm -rf "$src" "$dest" "$manifest"; exit 1
  fi
  rm -rf "$src" "$dest" "$manifest"
  echo "PASS: test_installs_all_four_and_records_their_hashes"
}

test_skips_a_file_hand_edited_since_install() {
  local src dest manifest
  src="$(mktemp -d)"; dest="$(mktemp -d)"; manifest="$(mktemp)"
  cp "$DIR/../../agents/lean-coder.md" "$src/"
  echo "---
name: lean-coder
description: the user's own hand-edited version
---
Not the managed content." > "$dest/lean-coder.md"
  # Manifest claims we installed a DIFFERENT hash than what's on disk now — simulates a hand-edit since install.
  echo '{"lean-coder.md": "0000000000000000000000000000000000000000000000000000000000000000"}' > "$manifest"
  local before
  before="$(cat "$dest/lean-coder.md")"
  local warn; warn="$(mktemp)"
  install_agents "$src" "$dest" "$manifest" 2>"$warn"
  local after
  after="$(cat "$dest/lean-coder.md")"
  if [ "$before" != "$after" ]; then
    echo "FAIL: overwrote a hand-edited agent file"; rm -rf "$src" "$dest" "$manifest" "$warn"; exit 1
  fi
  if ! grep -q "lean-coder.md" "$warn"; then
    echo "FAIL: expected a warning naming the skipped file"; rm -rf "$src" "$dest" "$manifest" "$warn"; exit 1
  fi
  rm -rf "$src" "$dest" "$manifest" "$warn"
  echo "PASS: test_skips_a_file_hand_edited_since_install"
}

test_upgrades_a_file_unmodified_since_install() {
  local src dest manifest
  src="$(mktemp -d)"; dest="$(mktemp -d)"; manifest="$(mktemp)"; echo '{}' > "$manifest"
  cp "$DIR/../../agents/lean-coder.md" "$src/"
  install_agents "$src" "$dest" "$manifest"   # first install
  echo "model: haiku" >> "$src/lean-coder.md"  # a real upstream update to the source
  install_agents "$src" "$dest" "$manifest"   # second install — target still matches manifest, so it upgrades
  if ! grep -q "model: haiku" "$dest/lean-coder.md"; then
    echo "FAIL: expected the unmodified installed file to be upgraded"; rm -rf "$src" "$dest" "$manifest"; exit 1
  fi
  rm -rf "$src" "$dest" "$manifest"
  echo "PASS: test_upgrades_a_file_unmodified_since_install"
}

# #3660: `printf | grep -q` under pipefail. grep exits at the first match, printf dies of SIGPIPE (141) and pipefail turns a found
# write tool into "no write tool". The tools value is one line today; this feeds the helper a long multi-line one with the match on line 1.
test_readonly_check_finds_a_write_tool_on_line_one_of_a_long_value() {
  _agent_field() { printf 'Read, Edit\n'; head -c 100000 /dev/zero | tr '\0' x | fold -w 80; }
  local i wrong=0
  for i in $(seq 20); do
    if _agent_is_readonly /nonexistent; then wrong=$((wrong + 1)); fi
  done
  unset -f _agent_field
  if [ "$wrong" -ne 0 ]; then echo "FAIL: a found write tool read as read-only $wrong of 20 times"; exit 1; fi
  echo "PASS: test_readonly_check_finds_a_write_tool_on_line_one_of_a_long_value"
}

test_installs_all_four_and_records_their_hashes
test_skips_a_file_hand_edited_since_install
test_upgrades_a_file_unmodified_since_install
test_readonly_check_finds_a_write_tool_on_line_one_of_a_long_value
