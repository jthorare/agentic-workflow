#!/usr/bin/env bash
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null; rm -rf "$p"; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; }
trap __aw_tmp_clean EXIT
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

test_installs_all_four_and_records_their_hashes
test_skips_a_file_hand_edited_since_install
test_upgrades_a_file_unmodified_since_install
