#!/usr/bin/env bash
# Playback safety check for the MP4s. Stills prove the frames exist; they do not prove a player can play them.
#
#   playback-check.sh <video.mp4>...     # exit 0 when every file passes, 1 otherwise
#
# Per file: decodes the whole stream (ffmpeg -v error -f null -), requires H.264 at level 4.1 or lower with no
# B-frames (has_b_frames=0). A High@5.0 encode with B-frames decoded fine in ffmpeg and froze mid-film in QuickTime.
# Passing is not the same as watching: the last line asks for one pass in the system player before delivery.
set -uo pipefail
[ $# -ge 1 ] || { echo "usage: $0 <video.mp4>..." >&2; exit 1; }
bad=0
for f in "$@"; do
  errors=$(ffmpeg -v error -i "$f" -f null - 2>&1 | head -5)
  info=$(ffprobe -v error -select_streams v:0 -show_entries stream=codec_name,profile,level,has_b_frames,pix_fmt -of default=nw=1 "$f")
  level=$(printf '%s\n' "$info" | sed -n 's/^level=//p')
  bframes=$(printf '%s\n' "$info" | sed -n 's/^has_b_frames=//p')
  codec=$(printf '%s\n' "$info" | sed -n 's/^codec_name=//p')
  problems=""
  [ -z "$errors" ] || problems="$problems decode errors: $errors;"
  [ "$codec" = h264 ] || problems="$problems codec $codec (want h264);"
  [ "${level:-99}" -le 41 ] 2>/dev/null || problems="$problems level $level (want <= 41, i.e. 4.1);"
  [ "${bframes:-1}" = 0 ] || problems="$problems has_b_frames=$bframes (want 0);"
  if [ -z "$problems" ]; then
    echo "playback ok   $(basename "$f"): decodes clean, $(printf '%s' "$info" | tr '\n' ' ')"
  else
    echo "playback FAIL $(basename "$f"):$problems"; bad=1
  fi
done
echo "WATCH IT: open each file once in the system player (QuickTime on macOS) and play it through before delivery; this check does not replace that."
exit $bad
