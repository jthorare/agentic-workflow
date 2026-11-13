#!/usr/bin/env bash
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null; rm -rf "$p"; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; }
trap __aw_tmp_clean EXIT
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$DIR/../apply-profile.sh"

FIXTURES="$DIR/fixtures"

test_writes_skill_overrides_to_empty_file() {
  local tmp manifest
  tmp="$(mktemp)"; manifest="$(mktemp)"; echo '{}' > "$manifest"
  cp "$FIXTURES/settings-local-empty.json" "$tmp"
  apply_profile "$tmp" "$DIR/../../profiles/web-app.json" "$manifest"
  local off_count
  off_count="$(jq '[.skillOverrides | to_entries[] | select(.value == "off")] | length' "$tmp")"
  # 26 = the 11 case-fold pairs + 15 iOS/design-pack skills (autoplan/cso/verify-web
  # excluded per Blocker 3 — they're gated by diff-skill-pairs.sh, not hardcoded).
  if [ "$off_count" -ne 26 ]; then
    echo "FAIL: expected 26 skills set to off, got $off_count"
    rm "$tmp" "$manifest"; exit 1
  fi
  rm "$tmp" "$manifest"
  echo "PASS: test_writes_skill_overrides_to_empty_file"
}

test_preserves_user_own_overrides() {
  local tmp manifest
  tmp="$(mktemp)"; manifest="$(mktemp)"; echo '{}' > "$manifest"
  cp "$FIXTURES/settings-local-with-user-overrides.json" "$tmp"
  apply_profile "$tmp" "$DIR/../../profiles/web-app.json" "$manifest"
  # the user's own manual "name-only" for a skill this profile doesn't mention
  # must survive the merge untouched.
  local preserved
  preserved="$(jq -r '.skillOverrides["some-manual-skill"]' "$tmp")"
  if [ "$preserved" != "name-only" ]; then
    echo "FAIL: expected the user's manual override preserved, got $preserved"
    rm "$tmp" "$manifest"; exit 1
  fi
  rm "$tmp" "$manifest"
  echo "PASS: test_preserves_user_own_overrides"
}

test_is_idempotent() {
  local tmp manifest
  tmp="$(mktemp)"; manifest="$(mktemp)"; echo '{}' > "$manifest"
  cp "$FIXTURES/settings-local-empty.json" "$tmp"
  apply_profile "$tmp" "$DIR/../../profiles/web-app.json" "$manifest"
  local first
  first="$(cat "$tmp")"
  apply_profile "$tmp" "$DIR/../../profiles/web-app.json" "$manifest"
  local second
  second="$(cat "$tmp")"
  if [ "$first" != "$second" ]; then
    echo "FAIL: re-running apply_profile changed the file"
    rm "$tmp" "$manifest"; exit 1
  fi
  rm "$tmp" "$manifest"
  echo "PASS: test_is_idempotent"
}

test_keeps_user_hand_edited_value_and_warns() {
  local tmp manifest warn
  tmp="$(mktemp)"; manifest="$(mktemp)"; warn="$(mktemp)"
  # Simulate: we previously wrote "addressReview": "off" and recorded it in
  # the manifest, but the user has since hand-set it back to "on" in the real file.
  echo '{"skillOverrides": {"addressReview": "off"}}' > "$manifest"
  echo '{"skillOverrides": {"addressReview": "on"}}' > "$tmp"
  apply_profile "$tmp" "$DIR/../../profiles/web-app.json" "$manifest" 2>"$warn"
  local value
  value="$(jq -r '.skillOverrides.addressReview' "$tmp")"
  if [ "$value" != "on" ]; then
    echo "FAIL: expected the user's hand-set 'on' to survive, got '$value'"
    rm "$tmp" "$manifest" "$warn"; exit 1
  fi
  if ! grep -q "addressReview" "$warn"; then
    echo "FAIL: expected a warning naming the hand-edited key"
    rm "$tmp" "$manifest" "$warn"; exit 1
  fi
  rm "$tmp" "$manifest" "$warn"
  echo "PASS: test_keeps_user_hand_edited_value_and_warns"
}

test_writes_skill_overrides_to_empty_file
test_preserves_user_own_overrides
test_is_idempotent
test_keeps_user_hand_edited_value_and_warns
