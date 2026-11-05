#!/usr/bin/env bash
# evals/stubs/bin/{bun,ffmpeg,ffprobe}, the system-design-video fakes: the planted probe answers travel with the mp4 (render
# outside the workspace, probe from there), frame grabs show the beat at -ss, a missing -i input exits 1, a *720p* file is 1280x720,
# and the skill's own review.sh and deliver.sh both report the planted B-frames.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STUB_DIR="$ROOT/evals/stubs/bin"
MEDIA="$ROOT/evals/stubs/media"
SK="$ROOT/skills/system-design-video"
TMP="$(mktemp -d)"
trap 'rm -r "$TMP"' EXIT
export HOME="$TMP/home"; mkdir -p "$HOME"
export GIT_CONFIG_GLOBAL="$TMP/gitconfig"
git config --file "$GIT_CONFIG_GLOBAL" user.name t
git config --file "$GIT_CONFIG_GLOBAL" user.email t@example.com

fails=0
ok() { echo "PASS: $1"; }
bad() { echo "FAIL: $1"; fails=$((fails + 1)); }

WS="$TMP/ws"; OUTSIDE="$TMP/outside"
mkdir -p "$WS" "$OUTSIDE"
(cd "$WS" && bash "$ROOT/evals/system-design-video/fixture.sh" >/dev/null 2>&1) || bad "fixture.sh failed"
export PATH="$STUB_DIR:$PATH"
probe() { ffprobe -v error -select_streams v:0 -show_entries stream=codec_name,profile,level,has_b_frames,pix_fmt -of default=nw=1 "$1"; }

# the cwd row: render outside the workspace, probe from there and from the workspace
(cd "$WS" && bun "$SK/whiteboard/render.ts" storyboard.json "$OUTSIDE/take" --tokens design-tokens.json >/dev/null) || bad "render failed"
[ -f "$OUTSIDE/take/footage.mp4" ] && [ -f "$OUTSIDE/take/layout.json" ] && ok "render writes footage.mp4 and layout.json" || bad "render outputs"
got="$(cd "$OUTSIDE/take" && probe footage.mp4 | grep has_b_frames)"
[ "$got" = "has_b_frames=2" ] && ok "probed from the take dir: has_b_frames=2" || bad "probed from the take dir: $got"
got="$(cd "$WS" && probe "$OUTSIDE/take/footage.mp4" | grep has_b_frames)"
[ "$got" = "has_b_frames=2" ] && ok "probed from the workspace: has_b_frames=2" || bad "probed from the workspace: $got"

# the encode carries the answers too, wherever it is probed from
(cd "$OUTSIDE/take" && ffmpeg -v error -y -i footage.mp4 -vf scale=1920:1080 -c:v libx264 "$OUTSIDE/v1/x-1080p.mp4") || bad "encode failed"
got="$(cd / && probe "$OUTSIDE/v1/x-1080p.mp4" | grep has_b_frames)"
[ "$got" = "has_b_frames=2" ] && ok "an encode keeps its input's answers" || bad "encode answers: $got"

# frame grabs at -ss show the beat on screen
(cd "$OUTSIDE/take" && ffmpeg -v error -ss 5 -i footage.mp4 -frames:v 1 g5.jpg && ffmpeg -v error -ss 30 -i footage.mp4 -frames:v 1 g30.jpg)
cmp -s "$OUTSIDE/take/g5.jpg" "$MEDIA/model.jpg" && ok "-ss 5 grabs model" || bad "-ss 5 is not model"
cmp -s "$OUTSIDE/take/g30.jpg" "$MEDIA/rollout.jpg" && ok "-ss 30 grabs rollout" || bad "-ss 30 is not rollout"

# a missing input exits 1
out="$(ffmpeg -v error -i "$TMP/nope.mp4" -f null - 2>&1)"; rc=$?
[ "$rc" = 1 ] && [[ "$out" == *"No such file or directory"* ]] && ok "ffmpeg: a missing -i exits 1" || bad "ffmpeg missing input rc=$rc: $out"
out="$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$TMP/nope.mp4" 2>&1)"; rc=$?
[ "$rc" = 1 ] && ok "ffprobe: a missing file exits 1" || bad "ffprobe missing file rc=$rc: $out"

# a *720p* file is 1280x720
cp "$OUTSIDE/v1/x-1080p.mp4" "$OUTSIDE/v1/x-720p-slack.mp4"
got="$(ffprobe -v error -show_entries stream=codec_name,width,height,r_frame_rate,color_space,color_primaries,color_transfer,color_range:format=duration -of compact=p=0 "$OUTSIDE/v1/x-720p-slack.mp4" | head -1)"
[[ "$got" == *"|width=1280|height=720|"* ]] && ok "*720p* probes 1280x720" || bad "720p: $got"

# the skill's own scripts, from the take dir outside the workspace
out="$(cd "$OUTSIDE/take" && "$SK/scripts/review.sh" . 2>&1)"
[[ "$out" == *"playback FAIL footage.mp4: has_b_frames=2"* ]] && ok "review.sh outside the workspace reports the B-frames" || bad "review.sh: $out"
ls "$OUTSIDE/take/check/"00-*.jpg "$OUTSIDE/take/check/sheet-0.jpg" >/dev/null 2>&1 && ok "review.sh writes the beat stills and the sheet" || bad "review.sh stills"
out="$(cd "$OUTSIDE/take" && "$SK/scripts/deliver.sh" footage.mp4 "$OUTSIDE/out" petsitter 0d1117 2>&1)"; rc=$?
[ "$rc" = 1 ] && [[ "$out" == *"playback FAIL petsitter-1080p.mp4: has_b_frames=2"* ]] && ok "deliver.sh outside the workspace fails playback" || bad "deliver.sh rc=$rc: $out"

# the fakes never call themselves fakes
if grep -qiE 'stub|stand-in|canned|fake' "$STUB_DIR/bun" "$STUB_DIR/ffmpeg" "$STUB_DIR/ffprobe" "$ROOT/evals/stubs/av-lib.sh"; then
  bad "a fake names itself: $(grep -liE 'stub|stand-in|canned|fake' "$STUB_DIR/bun" "$STUB_DIR/ffmpeg" "$STUB_DIR/ffprobe" "$ROOT/evals/stubs/av-lib.sh" | xargs)"
else ok "no fake names itself"; fi

[ "$fails" = 0 ] && echo "all passed" || { echo "$fails failed"; exit 1; }
