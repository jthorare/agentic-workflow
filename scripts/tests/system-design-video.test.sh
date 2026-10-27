#!/usr/bin/env bash
# skills/system-design-video/scripts: doctor.sh reports what is missing, playback-check.sh judges ffprobe output
# (level, B-frames, codec, decode errors), and deliver.sh / review.sh refuse bad arguments. ffmpeg, ffprobe and bun are
# stubs on a private PATH, so no media is read or encoded.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
S="$DIR/../../skills/system-design-video/scripts"
TMP="$(mktemp -d)"
trap 'find "$TMP" -delete 2>/dev/null || true' EXIT
BIN="$TMP/bin"; mkdir -p "$BIN"
for t in bash env sed awk head tr basename dirname cat mkdir printf xargs od du cut; do
  p="$(command -v "$t" || true)"; [ -n "$p" ] && ln -s "$p" "$BIN/$t"
done

fail() { echo "FAIL: $1"; exit 1; }
stub() { printf '#!/usr/bin/env bash\n%s\n' "$2" >"$BIN/$1"; chmod +x "$BIN/$1"; }
run() { env -i HOME="$TMP/home" AW_STATE_DIR="$TMP/state" PATH="$BIN" "$@"; }

probe_stub() { # level has_b_frames codec
  stub ffmpeg 'exit 0'
  stub ffprobe "printf 'codec_name=$3\nprofile=High\npix_fmt=yuv420p\nlevel=$1\nhas_b_frames=$2\n'"
}

test_doctor_lists_missing() {
  local out rc=0
  out="$(run bash "$S/doctor.sh" 2>&1)" || rc=$?
  [ "$rc" -eq 1 ] || fail "doctor with nothing installed should exit 1, got $rc"
  echo "$out" | has -F -e 'MISSING ffmpeg' || fail "ffmpeg not reported: $out"
  echo "$out" | has -F -e 'MISSING bun' || fail "bun not reported: $out"
  echo "$out" | has -F -e "$TMP/state/system-design-video" || fail "playwright-core hint ignores AW_STATE_DIR: $out"
  echo "PASS: test_doctor_lists_missing"
}

test_doctor_ready() {
  stub ffmpeg 'exit 0'; stub ffprobe 'exit 0'; stub bun 'exit 0'
  mkdir -p "$TMP/pw"; echo '{}' >"$TMP/pw/package.json"
  local out
  out="$(run env PLAYWRIGHT_CORE="$TMP/pw" bash "$S/doctor.sh" 2>&1)" || fail "doctor should exit 0 when all present: $out"
  echo "$out" | has -F -e "ok    playwright-core at $TMP/pw" || fail "PLAYWRIGHT_CORE not honored: $out"
  echo "PASS: test_doctor_ready"
}

test_playback_accepts_level_41_without_bframes() {
  probe_stub 41 0 h264
  local out
  out="$(run bash "$S/playback-check.sh" "$TMP/a.mp4" 2>&1)" || fail "level 41, no B-frames should pass: $out"
  echo "$out" | has -F -e 'playback ok' || fail "no ok line: $out"
  echo "PASS: test_playback_accepts_level_41_without_bframes"
}

test_playback_rejects_level_50_and_bframes() {
  local out rc=0
  probe_stub 50 2 h264
  out="$(run bash "$S/playback-check.sh" "$TMP/a.mp4" 2>&1)" || rc=$?
  [ "$rc" -eq 1 ] || fail "level 50 with B-frames should exit 1, got $rc"
  echo "$out" | has -F -e 'level 50' || fail "level not reported: $out"
  echo "$out" | has -F -e 'has_b_frames=2' || fail "B-frames not reported: $out"
  probe_stub 41 0 hevc; rc=0
  out="$(run bash "$S/playback-check.sh" "$TMP/a.mp4" 2>&1)" || rc=$?
  [ "$rc" -eq 1 ] || fail "non-h264 should exit 1, got $rc"
  echo "PASS: test_playback_rejects_level_50_and_bframes"
}

test_playback_reports_decode_errors() {
  stub ffmpeg 'echo "corrupt packet" >&2'
  stub ffprobe "printf 'codec_name=h264\nlevel=41\nhas_b_frames=0\n'"
  local out rc=0
  out="$(run bash "$S/playback-check.sh" "$TMP/a.mp4" 2>&1)" || rc=$?
  [ "$rc" -eq 1 ] || fail "decode errors should exit 1, got $rc"
  echo "$out" | has -F -e 'decode errors' || fail "decode error not reported: $out"
  echo "PASS: test_playback_reports_decode_errors"
}

test_usage_errors() {
  local rc=0
  run bash "$S/playback-check.sh" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || fail "playback-check.sh with no files should exit 1, got $rc"
  rc=0; run bash "$S/review.sh" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || fail "review.sh with no take dir should exit 1, got $rc"
  rc=0; run bash "$S/deliver.sh" a b >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 1 ] || fail "deliver.sh with two arguments should exit 1, got $rc"
  echo "PASS: test_usage_errors"
}

test_doctor_lists_missing
test_doctor_ready
test_playback_accepts_level_41_without_bframes
test_playback_rejects_level_50_and_bframes
test_playback_reports_decode_errors
test_usage_errors
