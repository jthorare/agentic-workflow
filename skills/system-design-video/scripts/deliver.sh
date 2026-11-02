#!/usr/bin/env bash
# Encode the board footage into the deliverables and print the evidence.
#
#   deliver.sh <footage.mp4> <out-dir> <name> <board-hex>
#     -> <out-dir>/<name>-1080p.mp4        (BT.709 tagged, faststart, no audio)
#     -> <out-dir>/<name>-720p-slack.mp4   (smaller cut for chat)
#     then prints ffprobe and a corner pixel check against the board color.
#
# Players decode untagged 4:2:0 with a guessed matrix and the colors drift, so the
# container and the H.264 VUI both carry BT.709. The corner check passes within
# 2/255 (H.264's floor for flat colors); chroma is subsampled 2x2, so a 2x2 crop is
# averaged to one pixel.
set -euo pipefail
[ $# -eq 4 ] || { echo "usage: $0 <footage.mp4> <out-dir> <name> <board-hex RRGGBB>" >&2; exit 1; }
src=$1 out=$2 name=$3 hex=${4#\#}
mkdir -p "$out"
tags=(-colorspace bt709 -color_primaries bt709 -color_trc bt709 -color_range tv)
vui=h264_metadata=colour_primaries=1:transfer_characteristics=1:matrix_coefficients=1:video_full_range_flag=0
full=$out/$name-1080p.mp4 slack=$out/$name-720p-slack.mp4

ffmpeg -v error -y -i "$src" -vf "scale=1920:1080:out_color_matrix=bt709:out_range=tv,format=yuv420p" \
  -c:v libx264 -crf 14 -preset slow -profile:v high -level 4.1 -bf 0 "${tags[@]}" -bsf:v "$vui" -an -movflags +faststart "$full"
ffmpeg -v error -y -i "$full" -vf "scale=1280:720:flags=lanczos:out_color_matrix=bt709:out_range=tv,format=yuv420p" \
  -c:v libx264 -crf 26 -preset slow -profile:v high -level 4.1 -bf 0 "${tags[@]}" -bsf:v "$vui" -an -movflags +faststart "$slack"

check() {
  local file=$1 px=$2 at rgb want
  at=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$file" | awk '{ print $1 - 0.3 }')
  rgb=$(ffmpeg -v error -ss "$at" -i "$file" -frames:v 1 -vf "crop=2:2:$px:$px,scale=1:1:flags=area,format=rgb24" -f rawvideo - | od -An -tu1 | xargs)
  want="$((16#${hex:0:2})) $((16#${hex:2:2})) $((16#${hex:4:2}))"
  echo "== $(basename "$file") ($(du -h "$file" | cut -f1))"
  ffprobe -v error -show_entries stream=codec_name,width,height,r_frame_rate,color_space,color_primaries,color_transfer,color_range:format=duration -of compact=p=0 "$file"
  awk -v got="$rgb" -v want="$want" -v hex="$hex" 'BEGIN {
    split(got, g, " "); split(want, w, " "); ok = 1;
    for (i = 1; i <= 3; i++) { d = g[i] - w[i]; if (d < 0) d = -d; if (d > 2) ok = 0 }
    printf "corner pixel (%s) vs #%s: %s\n", got, hex, ok ? "PASS" : "FAIL" }'
}
check "$full" 24
check "$slack" 16
"$(dirname "$0")/playback-check.sh" "$full" "$slack"
echo "deliverables: $full $slack"
