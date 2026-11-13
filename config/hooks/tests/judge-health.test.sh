#!/usr/bin/env bash
# Tests for config/hooks/judge-health.sh. Run: bash config/hooks/tests/judge-health.test.sh
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; return 0; }
trap __aw_tmp_clean EXIT
HOOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/judge-health.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

FAKE_BIN="$(mktemp -d)"
cat > "$FAKE_BIN/judge" <<'EOF'
#!/usr/bin/env bash
[ "$1" = "health" ] && cat "$JUDGE_HEALTH_FIXTURE"
EOF
chmod +x "$FAKE_BIN/judge"

JUDGE_HEALTH_FIXTURE="$(mktemp)"; echo '{"status":"ok","failures24h":0}' > "$JUDGE_HEALTH_FIXTURE"
out=$(PATH="$FAKE_BIN:$PATH" JUDGE_HEALTH_FIXTURE="$JUDGE_HEALTH_FIXTURE" bash "$HOOK" </dev/null); code=$?
check "healthy: prints nothing" "$out" ""
check "healthy: exits 0" "$code" "0"

echo '{"status":"degraded","failures24h":7}' > "$JUDGE_HEALTH_FIXTURE"
out=$(PATH="$FAKE_BIN:$PATH" JUDGE_HEALTH_FIXTURE="$JUDGE_HEALTH_FIXTURE" bash "$HOOK" </dev/null); code=$?
check "degraded: warns" "$out" "judge: degraded — 7 failures in the last 24h. Run: judge health"
check "degraded: still exits 0 (fails open)" "$code" "0"

BASH_BIN="$(command -v bash)"
out=$(PATH="/nonexistent" "$BASH_BIN" "$HOOK" </dev/null); code=$?
check "judge missing: warns down" "$out" "judge: not found on PATH"
check "judge missing: still exits 0" "$code" "0"

out=$(PATH="/nonexistent" AW_JUDGE_CHILD=1 "$BASH_BIN" "$HOOK" </dev/null); code=$?
check "AW_JUDGE_CHILD set: prints nothing (recursion guard)" "$out" ""
check "AW_JUDGE_CHILD set: exits 0" "$code" "0"

exit $fail
