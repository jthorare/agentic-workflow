#!/usr/bin/env bash
# Contact-sheet a rendered board at the end of every beat's drawing and camera move, so
# each beat can be read as a still before any scene work.
#
#   review.sh <take-dir>     # the dir render.ts wrote (footage.mp4, layout.json)
#
# Writes <take>/check/NN-<beat>.jpg and <take>/check/sheet-N.jpg (four beats per
# sheet, 2x2). Read every sheet against the storyboard: the beat's one idea is
# drawn, nothing is clipped, no label overlaps another, the caption matches.
set -euo pipefail
[ $# -ge 1 ] || { echo "usage: $0 <take-dir>" >&2; exit 1; }
here=$(cd "$(dirname "$0")" && pwd)
take=$(cd "$1" && pwd)
cd "$take"
mkdir -p check
bun -e '
  const l = JSON.parse(await Bun.file("layout.json").text());
  l.beats.forEach((b, i) => console.log(`${String(i).padStart(2, "0")} ${b.id} ${Math.min(b.end - 0.05, Math.max(b.land + 0.2, (b.settle ?? b.start) + 0.2)).toFixed(2)}`));
' >check/index.txt
# -nostdin: ffmpeg otherwise eats the index this loop reads from stdin.
while read -r i id at; do
  ffmpeg -nostdin -v error -y -ss "$at" -i footage.mp4 -frames:v 1 -vf scale=960:-1 -q:v 3 "check/$i-$id.jpg"
done <check/index.txt
files=(check/[0-9]*-*.jpg)
n=0
for ((k = 0; k < ${#files[@]}; k += 4)); do
  group=("${files[@]:k:4}")
  while [ ${#group[@]} -lt 4 ]; do group+=("${group[0]}"); done
  ffmpeg -nostdin -v error -y -i "${group[0]}" -i "${group[1]}" -i "${group[2]}" -i "${group[3]}" \
    -filter_complex "[0][1]hstack[a];[2][3]hstack[b];[a][b]vstack" "check/sheet-$n.jpg"
  n=$((n + 1))
done
cat check/index.txt
"$here/playback-check.sh" footage.mp4 || echo "FIX THE ENCODE before delivery (render.ts flags)"
echo "sheets: $n in $take/check (tiles run left to right, top to bottom, in index order)"
