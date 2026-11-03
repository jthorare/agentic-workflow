#!/usr/bin/env bash
# aw:image-paths — which PNG/JPEG files a hook's text names, for the judge's `images` input (judge-images spec 5.2).
# Sourced, never executed. bash 3.2 compatible (no mapfile, no associative arrays; `wc -c` for sizes, as on macOS).
#
#   aw_image_paths <text> [hook]   one absolute path per line: the existing .png/.jpg/.jpeg files the text names, deduped,
#                                  newest (last named) first, kept only under the checkout of the current directory,
#                                  at most 4 files and 4 MiB in all. Nothing for a text that names none, or outside a checkout.
#   aw_image_files [hook] [root]   the same limits over candidate paths on stdin (newest first); with a root, only files under it.
#                                  done-gate feeds it the files its turn read.
#   aw_images_attach <json> <paths>   the hook's judge input with `images` (the paths, one per line) and `imagesBestEffort: true`
#                                  added; the input itself, byte for byte, when there are no paths.
#
# When the limits left files out, one line {"event":"image-paths","input":{"hook":…,"sent":N,"dropped":M}} is appended to
# ${AW_PROBE_DIR:-$AW_STATE_DIR/probe}/image-paths.jsonl, but only if that directory exists (the probe has been turned on).
# Never prints anything else and never fails: a hook that can't use it behaves as today.

AW_IMAGE_MAX_FILES=4
AW_IMAGE_MAX_BYTES=4194304

# The ordered candidates in <text>, as written; empty lines never appear.
_aw_image_candidates() {
  printf '%s\n' "$1" | grep -oiE '/[^[:space:]"`<>()|,;]+\.(png|jpe?g)' 2> /dev/null || true
}

aw_image_files() {
  local hook="${1:-hook}" root="${2:-}" p size total=0 sent=0 left=0 seen=$'\n'
  while IFS= read -r p; do
    [ -n "$p" ] || continue
    case "$seen" in *$'\n'"$p"$'\n'*) continue ;; esac
    seen="$seen$p"$'\n'
    [ -f "$p" ] || continue
    case "$p" in */../*|*/..) continue ;; esac
    if [ -n "$root" ]; then case "$p" in "$root"/*) ;; *) continue ;; esac; fi
    size="$(wc -c < "$p" 2> /dev/null | tr -d ' ')" || size=""
    case "$size" in ''|*[!0-9]*) continue ;; esac
    if [ "$sent" -ge "$AW_IMAGE_MAX_FILES" ] || [ $((total + size)) -gt "$AW_IMAGE_MAX_BYTES" ]; then
      left=$((left + 1)); continue
    fi
    total=$((total + size)); sent=$((sent + 1))
    printf '%s\n' "$p"
  done
  if [ "$left" -gt 0 ]; then
    _aw_image_probe "$hook" "$sent" "$left"
  fi
  return 0
}

_aw_image_probe() {
  local dir="${AW_PROBE_DIR:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/probe}"
  [ -d "$dir" ] || return 0
  printf '{"ts":"%s","event":"image-paths","input":{"hook":"%s","sent":%s,"dropped":%s}}\n' \
    "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$1" "$2" "$3" >> "$dir/image-paths.jsonl" 2> /dev/null || true
}

aw_image_paths() {
  local text="$1" hook="${2:-hook}" root
  root="$(git rev-parse --show-toplevel 2> /dev/null)" || return 0
  [ -n "$root" ] || return 0
  # Newest first: the last one named leads (awk reverses; tac is not on macOS).
  _aw_image_candidates "$text" | awk '{ a[NR] = $0 } END { for (i = NR; i >= 1; i--) print a[i] }' | aw_image_files "$hook" "$root"
  return 0
}

aw_images_attach() {
  if [ -z "${2:-}" ]; then printf '%s' "$1"; return 0; fi
  printf '%s' "$1" | jq -c --arg p "$2" '. + {images: ($p | split("\n") | map(select(length > 0))), imagesBestEffort: true}' 2> /dev/null \
    || printf '%s' "$1"
}
