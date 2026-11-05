#!/usr/bin/env bash
# Sourced by bin/bun, bin/ffmpeg and bin/ffprobe (README.md, "system-design-video"). Set av_here to bin/ first.

# The answer file: the nearest ffprobe.json upward from $PWD, else the default ffprobe.json beside bin/.
av_answers() {
  local d="$PWD"
  while [ "$d" != "/" ]; do [ -f "$d/ffprobe.json" ] && { echo "$d/ffprobe.json"; return; }; d="$(dirname "$d")"; done
  [ -f "$av_here/../ffprobe.json" ] && echo "$av_here/../ffprobe.json"
}

# A file's answers on one line, or empty.
av_json() { [ -n "$1" ] && [ -f "$1" ] && tr -d '\n' < "$1" | tr -s ' '; }

# The answers an mp4 carries in its free box (written by av_write_mp4), or empty.
av_stamp_of() { [ -f "$1" ] && head -c 8192 "$1" | LC_ALL=C grep -ao 'free{[^}]*}' | head -1 | sed 's/^free//'; }

# av_write_mp4 <out> <random bytes> <answers json>: an ftyp box, a free box holding the answers, then the bytes.
av_write_mp4() {
  local out=$1 size=$2 json=$3 n
  n=$((${#json} + 8))
  {
    printf '\000\000\000\040ftypisom\000\000\002\000isomiso2avc1mp41'
    printf "\\x$(printf %02x $((n >> 24 & 255)))\\x$(printf %02x $((n >> 16 & 255)))\\x$(printf %02x $((n >> 8 & 255)))\\x$(printf %02x $((n & 255)))"
    printf 'free%s' "$json"
    head -c "$size" /dev/urandom
  } > "$out"
}

# av_val <json> <key>: one value, quotes stripped.
av_val() { printf '%s' "$1" | sed -n "s/.*\"$2\"[[:space:]]*:[[:space:]]*\"\{0,1\}\([^\",}]*\)\"\{0,1\}.*/\1/p" | head -1; }
