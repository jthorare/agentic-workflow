#!/usr/bin/env bash
# Tests for figma-frame.sh against a fake curl, so nothing reaches Figma.
set -u

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local rc=$? p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; exit "$rc"; }
trap __aw_tmp_clean EXIT

FRAME="$(cd "$(dirname "$0")" && pwd)/figma-frame.sh"
FAILURES=0
URL="https://www.figma.com/design/PVmS7pZqcyyisBOM0OASW7/Components?node-id=698-9058&m=dev"

fail() {
  echo "FAIL: $1"
  FAILURES=$((FAILURES + 1))
}

make_fixture() {
  local dir
  dir=$(mktemp -d)
  mkdir -p "$dir/bin"
  cat >"$dir/bin/curl" <<'FAKE'
#!/usr/bin/env bash
echo "curl $*" >>"$FAKE_DIR/calls.log"
out=""
while [ $# -gt 0 ]; do
  case "$1" in -o) out="$2"; shift ;; esac
  shift
done
if [ -n "$out" ]; then echo png >"$out"; else echo '{"err":null,"images":{"698:9058":"https://figma-alpha.s3/frame.png"}}'; fi
FAKE
  chmod +x "$dir/bin/curl"
  echo "$dir"
}

test_downloads_the_node_with_the_token() {
  local dir out status
  dir=$(make_fixture)
  out=$(FAKE_DIR="$dir" PATH="$dir/bin:$PATH" FIGMA_TOKEN=secret bash "$FRAME" "$URL" "$dir/field.components.png" 2>&1)
  status=$?
  [ "$status" -eq 0 ] || fail "download: exit $status: $out"
  grep -q "api.figma.com/v1/images/PVmS7pZqcyyisBOM0OASW7?ids=698:9058" "$dir/calls.log" || fail "download: API call: $(cat "$dir/calls.log")"
  grep -q "X-Figma-Token: secret" "$dir/calls.log" || fail "download: token not sent"
  grep -q "https://figma-alpha.s3/frame.png" "$dir/calls.log" || fail "download: image url not fetched"
  [ "$(cat "$dir/field.components.png")" = png ] || fail "download: file not written"
}

test_without_a_token_says_how_to_make_one() {
  local dir out status
  dir=$(make_fixture)
  out=$(FAKE_DIR="$dir" PATH="$dir/bin:$PATH" FIGMA_TOKEN= UI_BEFORE_AFTER_FIGMA_KEYCHAIN=ui-before-after-test-missing bash "$FRAME" "$URL" "$dir/x.png" 2>&1)
  status=$?
  [ "$status" -eq 7 ] || fail "no token: exit $status, want 7"
  [[ "$out" == *"security add-generic-password"* ]] || fail "no token: output lacks setup: $out"
  [ ! -f "$dir/calls.log" ] || fail "no token: called Figma anyway"
}

test_refuses_a_link_without_a_node() {
  local dir status
  dir=$(make_fixture)
  FAKE_DIR="$dir" PATH="$dir/bin:$PATH" FIGMA_TOKEN=secret bash "$FRAME" "https://www.figma.com/design/PVmS7pZqcyyisBOM0OASW7/Components" "$dir/x.png" >/dev/null 2>&1
  status=$?
  [ "$status" -eq 64 ] || fail "no node: exit $status, want 64"
}

test_downloads_the_node_with_the_token
test_without_a_token_says_how_to_make_one
test_refuses_a_link_without_a_node

if [ "$FAILURES" -eq 0 ]; then
  echo "3 passed"
else
  echo "$FAILURES failed"
  exit 1
fi
