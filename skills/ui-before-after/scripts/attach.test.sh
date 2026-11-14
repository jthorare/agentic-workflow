#!/usr/bin/env bash
# Tests for attach.sh against a fake gh and curl, so no PR is edited.
set -u

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local rc=$? p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; exit "$rc"; }
trap __aw_tmp_clean EXIT

ATTACH="$(cd "$(dirname "$0")" && pwd)/attach.sh"
FAILURES=0
UPLOAD_A="https://github.com/user-attachments/assets/aaaa"
UPLOAD_B="https://github.com/user-attachments/assets/bbbb"

fail() {
  echo "FAIL: $1"
  FAILURES=$((FAILURES + 1))
}

# Prints a scratch dir holding the proof files, a fake bin/ and the body the PR would hold after upload.
make_fixture() {
  local dir
  dir=$(mktemp -d)
  mkdir -p "$dir/proof/main" "$dir/proof/pr" "$dir/bin" "$dir/sizes"
  printf 'png' >"$dir/proof/field.png"
  printf '12345' >"$dir/proof/main/main.webm"
  printf '1234567' >"$dir/proof/pr/pr.webm"
  printf '%s\n' '![field](./field.png)' '' 'Recording, main:' '' './main/main.webm' '' 'Recording, PR:' '' './pr/pr.webm' >"$dir/proof/body.md"
  printf '%s\n' '![field](https://github.com/user-attachments/assets/img1)' '' 'Recording, main:' '' './main/main.webm' '' 'Recording, PR:' '' './pr/pr.webm' '' "$UPLOAD_A" "$UPLOAD_B" >"$dir/uploaded.md"
  printf '5' >"$dir/sizes/aaaa"
  printf '7' >"$dir/sizes/bbbb"
  cat >"$dir/bin/gh" <<'EOF'
#!/usr/bin/env bash
case "$*" in
  "auth token") echo tok ;;
  "pr edit"*)
    echo "$*" >>"$FAKE_DIR/edits.log"
    [ -f "$FAKE_DIR/edit-fails" ] && exit 1
    while [ $# -gt 0 ]; do
      [ "$1" = "--body-file" ] && cp "$2" "$FAKE_DIR/last-body.md"
      shift
    done ;;
  *"--json body"*) cat "$FAKE_DIR/uploaded.md" ;;
  *"--json url"*) echo "https://github.com/vitalizecare/web-app/pull/$3" ;;
esac
EOF
  cat >"$dir/bin/curl" <<'EOF'
#!/usr/bin/env bash
for last; do :; done
cat "$FAKE_DIR/sizes/$(basename "$last")"
EOF
  chmod +x "$dir/bin/gh" "$dir/bin/curl"
  echo "$dir"
}

run_attach() {
  local dir="$1"
  FAKE_DIR="$dir" PATH="$dir/bin:$PATH" bash "$ATTACH" 20057 "$dir/proof" 2>&1
}

test_attaches_every_referenced_file_and_puts_videos_under_their_headings() {
  local dir out status
  dir=$(make_fixture)
  out=$(run_attach "$dir")
  status=$?
  [ "$status" -eq 0 ] || fail "happy path: exit $status: $out"
  grep -q -- '--attach ./field.png#field --attach ./main/main.webm --attach ./pr/pr.webm' "$dir/edits.log" || fail "happy path: first edit args: $(head -1 "$dir/edits.log")"
  grep -q -- '-R vitalizecare/web-app' "$dir/edits.log" || fail "happy path: edit lacks -R"
  [ "$(wc -l <"$dir/edits.log" | tr -d ' ')" -eq 2 ] || fail "happy path: want 2 edits"
  grep -qx "$UPLOAD_A" "$dir/last-body.md" || fail "happy path: main video url missing from saved body"
  [ "$(grep -n "$UPLOAD_A" "$dir/last-body.md" | cut -d: -f1)" -lt "$(grep -n 'Recording, PR:' "$dir/last-body.md" | cut -d: -f1)" ] || fail "happy path: main video not under the main heading"
  [[ "$out" == *"https://github.com/vitalizecare/web-app/pull/20057"* ]] || fail "happy path: output lacks the PR url: $out"
}

test_exits_6_when_a_video_does_not_match_its_local_size() {
  local dir out status
  dir=$(make_fixture)
  printf '7' >"$dir/sizes/aaaa"
  printf '5' >"$dir/sizes/bbbb"
  out=$(run_attach "$dir")
  status=$?
  [ "$status" -eq 6 ] || fail "swapped videos: exit $status, want 6"
  [[ "$out" == *"./main/main.webm"* ]] || fail "swapped videos: output does not name the video: $out"
}

test_stops_after_a_failed_upload() {
  local dir out status
  dir=$(make_fixture)
  touch "$dir/edit-fails"
  out=$(run_attach "$dir")
  status=$?
  [ "$status" -eq 1 ] || fail "failed upload: exit $status, want 1"
  [ "$(wc -l <"$dir/edits.log" | tr -d ' ')" -eq 1 ] || fail "failed upload: want 1 edit"
}

test_needs_a_body_file() {
  local dir status
  dir=$(make_fixture)
  rm "$dir/proof/body.md"
  run_attach "$dir" >/dev/null
  status=$?
  [ "$status" -eq 64 ] || fail "no body.md: exit $status, want 64"
}

test_attaches_every_referenced_file_and_puts_videos_under_their_headings
test_exits_6_when_a_video_does_not_match_its_local_size
test_stops_after_a_failed_upload
test_needs_a_body_file

if [ "$FAILURES" -eq 0 ]; then
  echo "4 passed"
else
  echo "$FAILURES failed"
  exit 1
fi
