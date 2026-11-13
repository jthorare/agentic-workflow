#!/usr/bin/env bash
# Tests for lease.sh against a throwaway state dir.
set -u

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null; rm -rf "$p"; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; }
trap __aw_tmp_clean EXIT

SCRIPT="$(cd "$(dirname "$0")" && pwd)/lease.sh"
FAILURES=0

fail() {
  echo "FAIL: $1"
  FAILURES=$((FAILURES + 1))
}

lease() {
  UI_BEFORE_AFTER_STATE_DIR="$STATE" bash "$SCRIPT" "$@"
}

test_counts_only_other_live_leases() {
  STATE=$(mktemp -d)
  lease take a
  lease take b
  [ "$(lease others a)" = "1" ] || fail "others: a should see 1, saw $(lease others a)"
  [ "$(lease others c)" = "2" ] || fail "others: c should see 2"
}

test_release_removes_the_lease_and_prints_the_live_leases_left() {
  STATE=$(mktemp -d)
  lease take a
  lease take b
  [ "$(lease release a)" = "1" ] || fail "release: should leave 1"
  [ "$(lease release b)" = "0" ] || fail "release: should leave 0"
  [ -e "$STATE/proof-main.leases/a" ] && fail "release: lease file remains"
}

test_take_renews_the_timestamp() {
  STATE=$(mktemp -d)
  lease take a
  echo 1 >"$STATE/proof-main.leases/a"
  lease take a
  [ "$(cat "$STATE/proof-main.leases/a")" -gt 1000 ] || fail "renew: timestamp not updated"
}

test_a_lease_older_than_the_ttl_is_not_live() {
  STATE=$(mktemp -d)
  lease take a
  echo 1 >"$STATE/proof-main.leases/a"
  [ "$(lease others b)" = "0" ] || fail "ttl: expired lease still counted"
  [ -e "$STATE/proof-main.leases/a" ] && fail "ttl: expired lease not removed"
  lease take a
  [ "$(UI_BEFORE_AFTER_LEASE_TTL_S=3600 lease others b)" = "1" ] || fail "ttl: fresh lease not counted"
}

test_takes_over_a_lock_whose_holder_died() {
  STATE=$(mktemp -d)
  mkdir -p "$STATE/proof-main.leases.lock"
  echo 999999 >"$STATE/proof-main.leases.lock/pid"
  lease take a || fail "stale lock: take failed"
  [ -e "$STATE/proof-main.leases/a" ] || fail "stale lock: lease not taken"
}

test_exits_5_when_a_live_holder_keeps_the_lock() {
  local out status
  STATE=$(mktemp -d)
  mkdir -p "$STATE/proof-main.leases.lock"
  echo "$$" >"$STATE/proof-main.leases.lock/pid"
  out=$(UI_BEFORE_AFTER_LEASE_LOCK_WAIT_S=1 lease take a 2>&1)
  status=$?
  [ "$status" -eq 5 ] || fail "busy lock: exit $status, want 5"
  [[ "$out" == *"lock still held"* ]] || fail "busy lock: message: $out"
}

test_counts_only_other_live_leases
test_release_removes_the_lease_and_prints_the_live_leases_left
test_take_renews_the_timestamp
test_a_lease_older_than_the_ttl_is_not_live
test_takes_over_a_lock_whose_holder_died
test_exits_5_when_a_live_holder_keeps_the_lock

if [ "$FAILURES" -eq 0 ]; then
  echo "6 passed"
else
  echo "$FAILURES failed"
  exit 1
fi
