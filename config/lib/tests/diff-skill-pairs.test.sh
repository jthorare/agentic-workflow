#!/usr/bin/env bash
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local rc=$? p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; exit "$rc"; }
trap __aw_tmp_clean EXIT
. "$(dirname "${BASH_SOURCE[0]}")/../../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$DIR/../diff-skill-pairs.sh"

FIXTURES="$DIR/fixtures/skill-pairs"

test_reports_identical_pair() {
  local pairs_file
  pairs_file="$(mktemp)"
  printf 'identical-pair\tidentical-pair-kebab\n' > "$pairs_file"
  local out
  out="$(diff_skill_pairs "$FIXTURES/aw" "$FIXTURES/wa" "$pairs_file")"
  if ! echo "$out" | has "^IDENTICAL	identical-pair	identical-pair-kebab$"; then
    echo "FAIL: expected IDENTICAL verdict, got: $out"; rm "$pairs_file"; exit 1
  fi
  rm "$pairs_file"
  echo "PASS: test_reports_identical_pair"
}

test_reports_differing_pair() {
  local pairs_file
  pairs_file="$(mktemp)"
  printf 'differing-pair\tdiffering-pair-kebab\n' > "$pairs_file"
  local out
  out="$(diff_skill_pairs "$FIXTURES/aw" "$FIXTURES/wa" "$pairs_file")"
  if ! echo "$out" | has "^DIFFERS	differing-pair	differing-pair-kebab$"; then
    echo "FAIL: expected DIFFERS verdict, got: $out"; rm "$pairs_file"; exit 1
  fi
  rm "$pairs_file"
  echo "PASS: test_reports_differing_pair"
}

test_does_not_refuse_on_differs_for_a_case_fold_pair() {
  # RF-6 (revised 2026-09-27): the web-app kebab-case skills are intentional, adapted
  # ports (smaller, repo-specific) — DIFFERS is expected and informational, not a refusal.
  if skill_pair_refuse "DIFFERS" "addressReview" "autoplan cso verify-web"; then
    echo "FAIL: expected DIFFERS on a case-fold pair not to refuse"; exit 1
  fi
  echo "PASS: test_does_not_refuse_on_differs_for_a_case_fold_pair"
}

test_refuses_on_missing_for_a_case_fold_pair() {
  # MISSING means the kebab-case copy doesn't exist — turning the camelCase copy
  # "off" would remove the skill entirely, so this must still refuse.
  if ! skill_pair_refuse "MISSING" "addressReview" "autoplan cso verify-web"; then
    echo "FAIL: expected MISSING on a case-fold pair to refuse"; exit 1
  fi
  echo "PASS: test_refuses_on_missing_for_a_case_fold_pair"
}

test_never_refuses_on_an_exact_name_pair() {
  # autoplan/cso/verify-web are never gated — they're excluded from the profile
  # entirely (overriding the name would also hit the project copy).
  if skill_pair_refuse "MISSING" "autoplan" "autoplan cso verify-web"; then
    echo "FAIL: expected an exact-name pair never to refuse"; exit 1
  fi
  echo "PASS: test_never_refuses_on_an_exact_name_pair"
}

test_reports_identical_pair
test_reports_differing_pair
test_does_not_refuse_on_differs_for_a_case_fold_pair
test_refuses_on_missing_for_a_case_fold_pair
test_never_refuses_on_an_exact_name_pair
