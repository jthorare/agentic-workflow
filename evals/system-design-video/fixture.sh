#!/usr/bin/env bash
# Workspace for the system-design-video cases: a short made-up design doc (a pet-sitter booking redesign), an approved
# two-beat storyboard, a tokens file, the browser-stack marker doctor.sh looks for, and ffprobe.json that plants the
# failure (B-frames in the encode). Writes only under $PWD (and the run's temp HOME via the shared shim).
set -eu
EVALS="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [ -f "$EVALS/_lib/shim.sh" ]; then
  AW_LIB="$EVALS/_lib"; . "$AW_LIB/shim.sh"; shim_toolkit
fi
mkdir -p docs
cat > docs/pet-sitter-booking-design.md <<'D'
# Pet-sitter booking: split sitters from availability

## 1. Model
Today `sitters` holds one `available_days` text column. New: `sitters` (id, name), `availability` (sitter_id, day, from_hour, to_hour),
`bookings` (id, sitter_id, owner_id, day, status). Backfill: each day listed in `available_days` becomes one `availability` row, 08:00 to 18:00.
Example: a sitter with `mon,wed` becomes two rows; 120 sitters become 240 rows.

## 2. Paths
Write: `POST /bookings` takes a row lock on the `availability` row, inserts the booking, in one transaction.
Read: the search page reads `availability` joined to `bookings`; the old column is read only while the flag `new_availability` is off.

## 3. Rollout
States: `shadow` (dual write, old column serves), `compare` (both read, mismatches logged), `serving` (new tables serve), `retired` (old column dropped).
Gate to leave `compare`: zero mismatches for 7 days. Way back at every step: flip the flag. Point of no return: `retired`.

## 4. PRs
1. Schema + backfill (no dependencies). 2. Dual write (needs 1). 3. Compare read path (needs 2). 4. Flip + drop (needs 3, stated in the doc).
D
cat > storyboard.json <<'D'
{
  "inks": { "board": "color.surface.board", "chalk": "color.ink.chalk", "primary": "color.accent.primary" },
  "strokeWidth": 6,
  "beats": [
    { "id": "model", "src": "design doc section 1", "cam": [0, 0, 1], "say": "Sitters keep their name; availability moves into its own table.",
      "items": [ { "type": "text", "text": "sitters", "x": -300, "y": 0, "size": 64, "color": "primary" },
                 { "type": "text", "text": "availability", "x": 300, "y": 0, "size": 64, "color": "chalk" } ] },
    { "id": "rollout", "src": "design doc section 3", "cam": [1200, 0, 1], "say": "Four states, and the way back is a flag until the old column is dropped.",
      "items": [ { "type": "text", "text": "shadow > compare > serving > retired", "x": 1200, "y": 0, "size": 56, "color": "chalk" } ] }
  ]
}
D
cat > design-tokens.json <<'D'
{ "color": { "surface": { "board": { "$value": "#0d1117", "$type": "color" } },
             "ink": { "chalk": { "$value": "#e6edf3", "$type": "color" } },
             "accent": { "primary": { "$value": "#58a6ff", "$type": "color" } } } }
D
# the browser stack doctor.sh checks: under $PWD/.aw-state, and under AW_STATE_DIR when that sits inside $PWD
for state in "$PWD/.aw-state" "${AW_STATE_DIR:-}"; do
  case "$state" in "$PWD"/*)
    mkdir -p "$state/system-design-video/node_modules/playwright-core"
    echo '{"name":"playwright-core"}' > "$state/system-design-video/node_modules/playwright-core/package.json" ;;
  esac
done
# the planted failure: a 1080p encode that decodes fine but carries B-frames (level and codec are fine)
cat > ffprobe.json <<'D'
{
  "codec_name": "h264",
  "profile": "High",
  "pix_fmt": "yuv420p",
  "level": 41,
  "has_b_frames": 2,
  "width": 1920,
  "height": 1080,
  "r_frame_rate": "30/1",
  "duration": "46.0",
  "color_space": "bt709",
  "color_primaries": "bt709",
  "color_transfer": "bt709",
  "color_range": "tv",
  "corner_hex": "0d1117",
  "decode_error": ""
}
D
# the skill's own scripts where the skill text looks for them
SK="$EVALS/../skills/system-design-video"
if [ -d "$SK" ] && [ -n "${HOME:-}" ]; then
  mkdir -p "$HOME/.agentic-workflow/toolkit/skills"
  cp -r "$SK" "$HOME/.agentic-workflow/toolkit/skills/system-design-video"
fi
git init -q -b main
git add -A
git -c user.email=eval@example.com -c user.name=eval commit -q -m "initial design doc"
